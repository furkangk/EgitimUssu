# EğitimÜssü — Kapsamlı Mimari & Kod Kalitesi Denetimi

> **Tarih:** 2026-06-30
> **Kapsam:** Backend (.NET 9 modüler monolit, `src/`), Mobil (Flutter, `mobile/`), altyapı/CI (`render.yaml`, `Dockerfile`, `.github/`), testler (`tests/`).
> **Yöntem:** 6 paralel uzman denetim (mimari, güvenlik, DDD, persistence, mobil, operasyon) + en kritik bulguların kodu okuyarak/çalıştırarak birinci elden doğrulanması.
> **Çıta:** "Amazon production seviyesi". Bu rapor övgü değil, mühendislik gerçeği üzerine kuruludur.

---

## 0. Yönetici Özeti

İskelet **disiplinli**: modül başına Clean Architecture (API/Application/Domain/Infrastructure), modül başına ayrı PostgreSQL şeması, CQRS, transactional outbox'ın **yazma tarafı atomik ve doğru**, startup'ta fail-fast authorization, sağlam parola/token kriptosu, temiz hata sözleşmesi. Bu, küçük bir ekip için kayda değer bir tutarlılık.

Ancak production hazırlığı açısından **dört bağımsız blocker kümesi** var; ikisi tek istekle sömürülebilir veya sessiz veri kaybına yol açar.

### Olgunluk Skor Tablosu

| Alan | Skor | Özet |
|---|---|---|
| Yapısal mimari (katmanlama) | **6.0 / 10** | Sağlam temel; async tutarlılık omurgası prod'a hazır değil |
| Güvenlik | **4.0 / 10** | Tek istekle Admin + liste IDOR kabul edilemez |
| DDD / Domain modelleme | **4.5 / 10** | Value object yok, invariant'lar handler'a kaçmış, modüllerin ~%40'ı iskelet |
| Persistence / Performans | **5.5 / 10** | Migration drift, pagination/concurrency token yok |
| Mobil (Flutter) | **6.5 / 10** | En olgun katman; token düz-metin cache kritik |
| Operasyonel (test/CI/gözlem) | **4.0 / 10** | Backend CI yok, gerçek-DB testi yok, tracing yok |

**Genel production-hazırlık: ~5 / 10.** "Çalışan MVP iskeleti" seviyesinde; "Amazon prod"dan, çoğu **lokalize ve düzeltilebilir** birkaç temel açık nedeniyle uzak.

### Bulgu Dağılımı

| Önem | Adet |
|---|---|
| 🔴 Kritik | 5 |
| 🟠 Yüksek | 8 |
| 🟡 Orta | ~14 |
| 🟢 Düşük / Hijyen | ~10 |

---

## 1. 🔴 KRİTİK Bulgular (üretime çıkmadan kapatılmalı)

### K1 — Anonim kayıtla anında Admin yetki yükseltme *(birinci elden doğrulandı, exploit edilebilir)*
- **Dosya:** `src/Modules/Identity/Application/IdentityFeatures.cs:7,48,53` + `src/Modules/Identity/API/IdentityModule.cs:33`
- **Açıklama:** `RegisterUserCommand(... IReadOnlyCollection<UserRole> Roles) : IAllowAnonymous`. Handler yalnız `Roles.Count == 0` kontrol edip rolleri **filtresiz** ekliyor. `UserRole.Admin = 1`, hesap anında `Active`.
- **Saldırı senaryosu:** Kimliksiz biri `POST /api/identity/register` gövdesine `"roles":[1]` koyar → cevapta geçerli **Admin JWT** döner. Authorizer'lar `Roles.Contains("Admin")` görünce her sahiplik kontrolünü atlar → tek istekle tam platform ele geçirme.
- **Çözüm:** Rolü istemciden alma. Self-register'ı server tarafında allow-list ile `Student/Parent/Teacher`'a sınırla; elevated rol atamasını yalnız Admin'in çağırdığı ayrı endpoint'e taşı.

### K2 — Liste uçlarında IDOR: filtre verilmeyince tüm tablo dönüyor *(doğrulandı)*
- **Dosya:** `src/Modules/LessonSessions/Application/LessonSessionPolicies.cs:84-114` + `Infrastructure/LessonSessionRepository.cs`; benzer desen `src/Modules/Assignments/Application/AssignmentPolicies.cs`
- **Açıklama:** Liste authorizer'ı yalnız "filtre çağıranın id'sinden farklıysa" reddediyor; **filtre hiç verilmezse `Success`** dönüyor. Repo `ListAsync` null filtrede hiçbir `Where` uygulamadan tüm satırları getiriyor.
- **Saldırı senaryosu:** Sıradan öğrenci/veli `GET /api/lesson-sessions` (parametresiz) → platformdaki tüm derslerin öğretmen notları/öğrenci kimlikleri döner. Assignments'ta kurbanın id'si filtre olarak geçilerek başkasının ödev verisi okunur.
- **Çözüm:** Sahiplik filtresini server tarafında zorla (çağıranın id'sini server enjekte etsin); filtre yoksa/başkasına aitse reddet (varsayılan-deny). Liste sorgularına zorunlu-filtre validator'ı.

### K3 — Outbox dağıtımı entegrasyon event'lerini sessizce düşürüyor *(ampirik olarak kanıtlandı — 3 bağımsız kaynak)*
- **Dosya:** Yazım Web/camelCase: `src/Shared/Infrastructure/Persistence/ModuleDbContext.cs:83`, `Messaging/JsonDomainEventMapper.cs:9` — Okuma varsayılan/PascalCase: `src/Shared/Infrastructure/Messaging/OutboxProcessor.cs:20`
- **Kanıt (çalıştırılan test):**
  ```
  SERIALIZED (Web):  {"name":"LessonScheduledDomainEvent","sourceModule":"Scheduling",...}
  DESERIALIZED:      Name=<null>  SourceModule=<null>  Payload=<null>   → CanHandle = False
  ```
- **Açıklama:** `IntegrationEvent` positional record; camelCase alanlar case-sensitive okumada ctor'a bağlanamaz → event tüm alanları boş publish edilir, hiçbir handler eşleşmez, ama mesaj yine "processed" işaretlenir (`OutboxProcessor.cs:29`). Prod'da (`render.yaml: DispatchEnabled=true`) tüm modüller-arası entegrasyon sessizce kaybolur.
- **Çözüm:** Okumada da statik tek `JsonSerializerOptions(JsonSerializerDefaults.Web)` kullan; bir serialize→deserialize round-trip testi ekle.

### K4 — Migration drift: 7 modül tablosuz → prod'da çöküş *(doğrulandı: 15 modül DbContext kaydediyor, yalnız 8'inde migration var)*
- **Dosya:** Migration yok → `src/Modules/Notifications/Infrastructure/Migrations/` (boş, aktif `LessonReminder` entity'li!), ayrıca `Matching, Parents, ProgressTracking, Reporting, Reviews, Study`. Tetikleyen: `src/Shared/Infrastructure/Persistence/EfOutboxStore.cs:16-31`
- **Açıklama:** Prod'da (`ApplyMigrationsOnStartup=true` + `DispatchEnabled=true`):
  - **Notifications** ilk sorguda patlar — `relation "notifications.lesson_reminders" does not exist`.
  - `EfOutboxStore.FetchPendingAsync` tablosuz 7 context'i dolaşırken exception fırlatır → **tüm outbox dispatch kalıcı durur**.
  - Dev'de gizli (InMemory provider tabloları otomatik kurar + dispatch kapalı).
- **Çözüm:** Notifications migration'ı üret; entity'si olmayan modüller için DbContext/descriptor kaydetme; `FetchPendingAsync`'te context başına hatayı izole et (try/catch + log).

### K5 — Outbox işleyici: çoklu-instance yarışı + zehirli-mesaj kilitlenmesi *(doğrulandı)*
- **Dosya:** `src/Shared/Infrastructure/Persistence/EfOutboxStore.cs:19-31`, `Messaging/OutboxProcessor.cs:18-29`
- **Açıklama:** Satır kilidi/`FOR UPDATE SKIP LOCKED`/claim yok → 2+ instance aynı satırları çeker (mükerrer publish). Batch tek tek publish edilip *sonra* topluca işaretlendiğinden, ortadaki bir publish exception'ı `MarkProcessedAsync`'i hiç çalıştırmaz → **en eski zehirli mesaj sırayı sonsuza dek bloklar** (head-of-line). `Error` kolonu var ama hiç yazılmıyor; retry/dead-letter/tüketici idempotency tablosu yok.
- **Çözüm:** `SELECT ... FOR UPDATE SKIP LOCKED` ile satır sahiplenme; per-mesaj try/catch + `Error`/retry sayacı + max-retry sonrası dead-letter; tüketici idempotent olmalı.

---

## 2. 🟠 YÜKSEK Bulgular

### Y1 — Modül izolasyon ihlali + atomik olmayan cross-module yazım *(2 ajan doğruladı)*
- `src/Modules/Scheduling/Infrastructure/LessonScheduleNotificationService.cs:25-48` → doğrudan `Notifications.Application/Domain` referansı; ders commit'inden **sonra** ayrı transaction'da Notifications'a yazıyor (`LessonScheduleFeatures.cs:139-140`). Ders kalıcı, reminder yazımı patlarsa tutarsızlık + 500.
- `src/Modules/Assignments/Application/*` → doğrudan `LessonSessions.Application` referansı (`ILessonSessionAccessService` ile başka modülün DB'sinden okuma).
- Aynı reminder işi zaten outbox event yoluyla da yapılıyor → iki paralel yol; senkron olan outbox garantisini geçersiz kılıyor.
- **Çözüm:** Senkron çağrıyı kaldır, yalnız domain event → outbox → handler yolunu kullan; cross-module veriyi event payload'ında taşı.

### Y2 — Mimari testler bu ihlalleri yakalamıyor (yanlış güven)
- `tests/Architecture/ProjectArchitectureTests.cs` yalnız 3 kuralı test ediyor; "Modül X başka Modül Y'ye referans veremez (Shared hariç)" kuralı yok → Y1'deki gerçek ihlaller yeşil geçiyor.
- **Çözüm:** Cross-module referans yasağı kuralını ekle.

### Y3 — Kaynağa gömülü zayıf JWT signing key *(3 ajan; severity nüanslı)*
- `src/API.Host/appsettings.json:11` = `"change-this-development-signing-key"`, `src/Shared/Infrastructure/Configuration/JwtOptions.cs:11` varsayılanı da placeholder.
- Prod'da `render.yaml:37 generateValue:true` ile güvenli üretiliyor (**Render üzerinden risk düşük**) — ama env set edilmeden çalışan herhangi bir ortam herkesçe bilinen anahtarı kullanır → istediği `sub`+`Admin` ile token sahteciliği. `ConfigurationHealthCheck` yalnız `Length < 16` bakıyor.
- **Çözüm:** Sabit anahtarı repodan çıkar; startup'ta varsayılan/kısa anahtarı reddet (fail-fast, min 32 byte); RS256/asimetrik değerlendir.

### Y4 — Brute-force koruması yok + rate limiter partition'sız
- `src/Modules/Identity/Application/IdentityFeatures.cs:72` login'de başarısız-deneme sayacı/kilit/CAPTCHA yok. `src/API.Host/Program.cs:85` `auth` limiter'ı **partition'sız global** 10/dk → dağıtık brute-force'a anlamsız + **self-DoS** (saldırgan kotayı doldurup herkesin login'ini engeller). `default` limiter tanımlı ama hiçbir iş ucuna uygulanmıyor.
- **Çözüm:** `PartitionedRateLimiter` ile IP+hesap bazlı; ardışık hatada üstel gecikme/kilit; iş uçlarına `default` limit.

### Y5 — Optimistic concurrency token yok — lost update
- Hiçbir entity'de `xmin`/rowversion yok. `UpdatePaymentRecordCommandHandler` (`PaymentFeatures.cs:150`) read-modify-write; iki eşzamanlı istek → biri kaybolur. Scheduling çift-rezervasyon yarışı (`HasTeacherConflictAsync` check-then-act, DB constraint yok).
- **Çözüm:** Para/durum aggregate'lerine `Property<uint>("xmin").IsRowVersion()`; `DbUpdateConcurrencyException → 409`. Scheduling için `EXCLUDE USING gist` (tstzrange overlap).

### Y6 — Pagination yok + in-memory aggregation
- `src/Shared/Kernel/PagedResult.cs` tanımlı ama **hiç kullanılmıyor** (kod tabanında tek `Skip/Take` yok). `GetTeacherPaymentSummaryQuery` (`PaymentFeatures.cs:210-316`) ve dashboard tüm kayıtları belleğe çekip C#'ta `GroupBy/Sum/Where`. Ölçekte lineer büyüyen latency+bellek.
- **Çözüm:** Liste uçlarını DB-side `Skip/Take` + tek `CountAsync` → `PagedResult`; özetleri DB-side `GroupBy` projeksiyonuna çevir.

### Y7 — Mobil: access+refresh token düz metin SharedPreferences'a kopyalanıyor
- `mobile/lib/.../auth_repository_impl.dart:159` + `user_session_model.dart:36` — `flutter_secure_storage` doğru kurulmuş ama `_persistSession` aynı oturumu `toCache()` ile (token'lar dahil) şifresiz `SharedPreferences`'a da yazıyor → root'lu cihaz/yedekle refresh token okunabilir.
- **Çözüm:** Cache JSON'undan token alanlarını çıkar; token yalnız secure storage'da; Android `allowBackup=false` + `EncryptedSharedPreferences`.

### Y8 — Backend için hiç CI yok + güvenlik taraması yok
- `.github/workflows/` yalnız `build-android.yml`. `dotnet build/test` hiçbir yerde koşmuyor; 6 backend + 14 mobil testi (+`flutter analyze`) CI'da çalışmıyor. CodeQL/Dependabot/`dotnet list package --vulnerable` yok. Kırık/zafiyetli kod doğrudan Render auto-deploy'a gidebilir.
- **Çözüm:** `dotnet restore/build -warnaserror/test` + migration drift + `--vulnerable` çalıştıran backend workflow; mobilde `flutter analyze --fatal-infos` + `flutter test --coverage`; PR status check zorunlu.

---

## 3. 🟡 ORTA Bulgular

### Domain / DDD
- **M1 — Hiç Value Object yok (sistemik primitive obsession):** Kernel'de `ValueObject` var, kullanan modül yok. `Money` yok → `decimal Amount + string Currency = "TRY"` ikilisi `Teachers/Domain:70`, `Payments/Domain:66`'ya kopyalanmış; para özetleri string currency'ye göre grupланıyor (`PaymentFeatures.cs:241`), yuvarlama/precision koruması domain'de yok. Email/telefon/DateRange de primitive.
- **M2 — İş kuralları handler'a kaçmış; kurucular "setter" gibi:** Aralık/çakışma kuralı `LessonScheduleFeatures.cs:105-120`'de; `Teachers/Domain:11-48` ctor'u negatif rate/experience/boş ad serbest kabul ediyor; `Result<T>` döndüren fabrika metodu hiçbir aggregate'te yok.
- **M3 — Payments güvenlik-kritik invariant zorlamıyor:** `PaymentStatus` istemci komutundan geliyor (`PaymentFeatures.cs:18`), tutarlardan türetilmiyor — müşteri "Paid" yazabilir. Hesap mantığı Application extension'larında (`PaymentFeatures.cs:319-364`).
- **M4 — Kapsülleme `public List<T>` ile deliniyor:** `IdentityDomainModel.cs:62-66`, `Teachers:82`, `Students:70` — dışarıdan `user.RoleMemberships.Add(...)`; Students'ta child değişimi Infrastructure'a kadar inmiş (`ReplaceSubjectsAsync`). `IReadOnlyCollection` + aggregate davranışı olmalı.
- **M5 — Anemik / iskelet modüller:** `Settings.UserSetting` kalıcılaştırılmış ama davranışsız/event'siz; 6 modül (Matching/Parents/ProgressTracking/Reporting/Reviews/Study) tamamen iskelet (`AssemblyReference` dışında Domain boş).
- **M6 — Event isim çakışması:** `LessonSessionCompletedDomainEvent` iki modülde (`SchedulingDomainModel.cs:169`, `LessonSessionsDomainModel.cs:126`); Scheduling'inki yanlış adlandırılmış (olması gereken `LessonScheduleCompletedDomainEvent`). Mapper namespace'siz `GetType().Name` kullanıyor → yalnız `SourceModule` ile ayrışıyor (kırılgan).
- **M7 — Güçlü-tipli ID yok:** Çıplak `Guid` (örn. `PaymentRecord` ctor'unda ardışık iki `Guid` — yer değiştirme derleyiciden kaçar).

### Persistence / Performans
- **M8 — AsNoTracking neredeyse hiç yok:** Tüm `Get*`/`List*` tracked çekiyor (yalnız `LessonSessionAccessService.cs:19` + outbox istisna). Salt-okunur uçlarda ChangeTracker şişiyor.
- **M9 — EnableRetryOnFailure / CommandTimeout yok:** `ServiceCollectionExtensions.cs:65` — RDS/Aurora failover'da komutlar retry'sız düşer.
- **M10 — Outbox index zayıf + retention yok:** Tek kolon `ProcessedOnUtc` index'i (`ModuleDbContext.cs:39`); sorgu `WHERE ProcessedOnUtc IS NULL ORDER BY OccurredOnUtc`. İşlenen mesajlar hiç silinmiyor → sınırsız büyüme. Partial index + temizlik job'ı gerekli.
- **M11 — RefreshTokenHash index'siz (auth hot-path seq scan):** `IdentityRepositoryAndSecurity.cs:42-49` + `IdentityDbContext.cs:72`. Ayrıca her token doğrulamada tüm RefreshSessions/SecurityTokens/RoleMemberships eager `Include` ediliyor.
- **M12 — Notifications due-tarama index uyumsuz:** `LessonReminderRepository.cs:30` `Status==Pending && RemindAtUtc<=now` filtreler ama index `(TeacherUserId, Status, RemindAtUtc)` — öncü kolon predicate'te yok → 30 sn'de bir tam tarama.

### Mimari / Operasyon
- **M13 — `dynamic` tabanlı dispatch:** Sıra doğru (Validation→Authorization→Handler) ama derleme-zamanı güvenliği yok; eksik handler'da opak 500 (`CommandDispatcher.cs:43`). Source generator / tipli kayıt değerlendir.
- **M14 — Integration testler EF InMemory (sahte güven):** `tests/Integration/*` FK/unique/transaction/concurrency/Npgsql davranışını ve migration'ları doğrulamaz. Domain unit testi yok (ters piramit; ~1.2k test satırı / ~14k kaynak). Testcontainers gerekli.

---

## 4. 🟢 DÜŞÜK / Hijyen

- **D1 — Platforma özgü dosyalar `main`'e commit'li (CLAUDE.md ihlali):** `.vscode/launch.json`, `.vscode/tasks.json`, `mobile/.metadata`, `mobile/.vscode/*`, `mobile/pubspec.lock`. `.gitignore` `.vscode/` ve `mobile/.metadata`'yı kapsamıyor. (`bin/obj` git'te değil — doğru.)
- **D2 — Repo kökü kirliliği:** `tmp_api_stdout.log`/`tmp_api_stderr.log` çalışma ağacında (commit'li değil); disk'te `net10.0` stale build artefaktları.
- **D3 — Merkezi paket yönetimi yok:** `Directory.Packages.props` yok; ~60 csproj sürümü tek tek taşıyor.
- **D4 — Build hijyeni:** `Directory.Build.props` `TreatWarningsAsErrors=false`, `LangVersion=preview`.
- **D5 — Dockerfile:** root user (`USER` yok), `HEALTHCHECK` yok, floating image tag (`sdk:9.0`/`aspnet:9.0`).
- **D6 — `DbContext` çift kaydı:** `ServiceCollectionExtensions.cs:57-68` (`AddDbContext` + `AddScoped(typeof(TContext))`).
- **D7 — `DateTime.UtcNow` `IClock` yerine:** `EfOutboxStore.cs:64`, `JwtTokenIssuer` (`IdentityRepositoryAndSecurity.cs:89`).
- **D8 — `Entity.Equals` tür kontrolü yok:** `Entity.cs:7-15` — aynı `Guid`'li `TeacherProfile` ile `StudentProfile` eşit sayılır; `==`/`!=` operatörü yok.
- **D9 — `Result<T>.Value` korumasız:** `Result.cs:30` — failure'da bile erişilebilir; çağıranlar `Value!` kullanıyor.
- **D10 — Mobil:** 8 "tanrı widget" (1000-2190 satır, örn. `scheduling_page.dart`), presentation→state sızıntısı (`Color` state'te, `dashboard_state.dart`), güvensiz-varsayılan config (mock fallback açık + cleartext URL, `app_config.dart:26`), i18n fiilen yok (ARB/gen-l10n yok), gömülü demo credential'lar (`login_page.dart:24`), kullanılmayan ağır bağımlılıklar (freezed/json_serializable), register'da rol sabit `[2]`.

---

## 5. ✅ Doğru Yapılmış (denge için)

- **Katman bağımlılıkları temiz** — Domain projeleri sıfır altyapı referanslı; Application yalnız Domain+Shared (2 cross-module istisna hariç); EF yalnız Infrastructure'da.
- **Outbox YAZIM tarafı atomik** — `ModuleDbContext.SaveChanges` override'ı domain event'leri aynı transaction'da outbox satırına çevirip ekliyor ve `ClearDomainEvents` ile temizliyor.
- **Güvenlik temelleri sağlam** — ASP.NET `PasswordHasher` (PBKDF2-HMAC-SHA256, ~100k iter, per-parola salt); refresh token'lar 48-byte CSPRNG + SHA256 hash'li + rotate; reset/verify token'ları tek-kullanımlık+süreli; kullanıcı enumerasyonu reset akışında engelli; parola sıfırlamada tüm oturumlar revoke. JWT doğrulama tam (Issuer/Audience/Lifetime, ClockSkew 1dk).
- **Startup fail-fast authorization** (`AuthorizationCoverageValidator`) + entegrasyon testi — korumasız handler build'i patlatır.
- **Temiz hata sözleşmesi** — `ProblemDetailsExceptionMiddleware` stack trace sızdırmıyor; loglarda PII/parola/token yok; `ApiErrorResponse`'ta `TraceId`. Raw SQL yok (parametreli EF).
- **Mobil network katmanı örnek seviyede** — tek merkezî Dio, single-flight token refresh (`QueuedInterceptorsWrapper`), `DioException→ApiException` eşlemesi, go_router redirect-tabanlı auth guard, feature-first temiz katmanlama, get_it DI.
- **Entity mapping disiplinli** — ayrı `IEntityTypeConfiguration`, şema ayrımı, enum→string, para `HasPrecision(18,2)`, isabetli composite index/unique constraint'ler (`NormalizedEmail`, `(UserAccountId,Role)`, `(City,Subject)`, `(TeacherUserId,StartAtUtc)`).

---

## 6. DDD Modül Olgunluk Tablosu

| Modül | Durum | Not |
|---|---|---|
| Scheduling | 🟡+ | En zengin davranış + yaşam döngüsü + event'ler; aralık/çakışma kuralı handler'da |
| Notifications | 🟡+ | Domain-içi guard'lı durum geçişleri (en iyi invariant örneği); migration yok |
| Identity | 🟡 | Session/token davranışları; public mutable koleksiyonlar, child'lar handler'da |
| Teachers | 🟡 | Update+event; mutable slot listesi, Money primitive |
| Students | 🟡 | Update var; child yaşam döngüsü Infra'ya kaymış |
| LessonSessions | 🟡 | Complete+süre hesabı; negatif süre/geçiş guard'ı yok |
| Assignments | 🟡 | Davranış+event; cross-module bağımlılık (LessonSessions) |
| Payments | 🔴 | Para invariant'ı yok, durum istemciden, hesap mantığı Application'da |
| Settings | 🔴 | Davranışsız/event'siz veri çantası; handler/endpoint yok |
| Matching / Parents / ProgressTracking / Reporting / Reviews / Study | 🔴 | Saf iskelet — domain modeli yok |

---

## 7. Önceliklendirilmiş Düzeltme Yol Haritası

**Aşama 0 — Üretim blocker'ları (saatler/gün):** K1, K2, K3, K4
**Aşama 1 — Async omurga + güvenlik sertleştirme (günler):** K5, Y1, Y3, Y4, Y7
**Aşama 2 — Kalite kapıları (günler):** Y8, Y2, M14 (Testcontainers)
**Aşama 3 — Ölçek & olgunluk:** Y5, Y6, M8–M12, gözlemlenebilirlik (OTel), DDD zenginleştirme (M1–M7), hijyen (D1–D10)

---

## 8. Sonuç

Bu, "yanlış kurulmuş" değil, **"tamamlanmamış" bir production sistemi.** Mimari niyet doğru, iskelet temiz; ama asenkron tutarlılık omurgası, yetkilendirme mantığı ve operasyonel boru hattı henüz prod-grade değil. Kritik açıkların neredeyse tamamı lokalize ve düzeltilebilir — K1–K4 birkaç saatte kapatılıp skoru 6–7 bandına taşıyabilir. "Amazon seviyesi" için asıl fark, kapatılan açıklar değil, **onları bir daha açılmaktan alıkoyan kapılar** (CI + mimari testler + gerçek-DB testleri + tracing) olacaktır.

---

*Bu rapor salt-okunur denetimle üretilmiştir; kod/davranış değiştirilmemiştir. Bulgular kabul edilip düzeltilmeye başlandığında, CLAUDE.md doküman kuralı gereği ilgili `doc/modules/mimari_inceleme.md`, `doc/modules/mNN_*.md` (Identity, LessonSessions, Scheduling, Notifications, Payments) ve `doc/roles/*.md` aynı turda güncellenmelidir.*
