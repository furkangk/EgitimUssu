---
title: "Mimari İnceleme — Hatalar ve Eksikler"
summary: "Backend + mobil mimarisinin kritik açık/footgun envanteri (K/Y kodları); 2026-06-30 denetiminin Aşama 0-1 bulguları büyük ölçüde kapatıldı"
tags: [modul, mimari-inceleme, denetim, guvenlik]
authority: derived
updated: 2026-11-14
---

# 🔬 Mimari İnceleme — Hatalar ve Eksikler

> **İlk inceleme:** 2026-06-21 · **Güncelleme:** 2026-07-01 · **Kapsam:** Backend (.NET 9 modüler monolit) + Flutter mobil
> **Yöntem:** Çekirdek altyapı (CQRS, Outbox, DI, Auth) + tüm modüllerin domain/feature/API katmanları + mobil çekirdek incelendi.

> **Genel değerlendirme:** Mimari **sağlam temellere** oturuyor (temiz katman ayrımı, modül başına DbContext,
> Outbox envelope, CQRS, mimari testleri, doğru parola/JWT kullanımı). Ancak **birkaç kritik footgun ve güvenlik
> açığı** var; bunların bir kısmı sistemi "sessizce" çalışmaz/güvensiz hale getiriyor.

**Önem skalası:** 🔴 Kritik · 🟠 Yüksek · 🟡 Orta · ⚪ Düşük/Hijyen

> **Not:** Bu belge 2026-06-21 iç incelemesinin kendi numaralandırmasını kullanır. Ayrı ve daha kapsamlı
> **2026-06-30 denetimi** ([`../denetim/2026-06-30_kapsamli_kod_denetimi.md`](../denetim/2026-06-30_kapsamli_kod_denetimi.md))
> farklı kodlar (K1–K5, Y1–Y8, M/D) kullanır; ikisi karıştırılmamalıdır.

> ### ✅ 2026-06-30 denetimi — Aşama 0 (prod blocker'ları) uygulandı — 2026-07-01
> O denetimin kritik bulguları kapatıldı (kodlar o rapora aittir):
> - **K1** — Anonim register'da Admin yükseltmesi: self-register allow-list (`Teacher/Student/Parent`) + yalnız Admin'e açık `POST /api/identity/users/{id}/roles`. Bkz. [`m01_identity.md`](m01_identity.md).
> - **K2** — Liste IDOR'u: LessonSessions & Assignments liste handler'larında **server-enjekte sahiplik filtresi** (varsayılan-deny). Bkz. [`m05_lesson_sessions.md`](m05_lesson_sessions.md), [`m06_assignments.md`](m06_assignments.md).
> - **K3** — Outbox okuma serileştirmesi: yazım/okuma tek `IntegrationEventSerialization.Options` (Web) kaynağına bağlandı + round-trip birim testi.
> - **K4** — Migration drift'i: Notifications migration'ı üretildi; entity'siz 6 iskelet modülün `AddModuleDbContext` kaydı kaldırıldı; `EfOutboxStore.FetchPendingAsync`'e context-başına hata izolasyonu (+log) eklendi.
> - **Y8** — Backend CI: `.github/workflows/backend-ci.yml` (build `-warnaserror` + test + zafiyet taraması + migration drift).
>
> **Aşama 1 ilerleme (2026-07-01):**
> - **Y1** (comp. denetim) — Scheduling'in Notifications'a **senkron yazımı kaldırıldı**: `LessonScheduleNotificationService` + interface + DI + Scheduling→Notifications proje referansları silindi. Hatırlatma artık yalnız `LessonScheduledDomainEvent`/`LessonScheduleCancelledDomainEvent` → outbox → Notifications handler yoluyla; `ReminderOffsetMinutes` event payload'ında taşınıyor. Bkz. [`m04_scheduling.md`](m04_scheduling.md), [`m11_notifications.md`](m11_notifications.md).
> - **Y3** (comp. denetim, JWT) — aşağıdaki Y2 maddesine bkz: imzalama anahtarı repodan çıkarıldı + startup fail-fast (`JwtSigningKeyGuard`, min 32 bayt).
> - **K5** (comp. denetim) — outbox mesaj-başına retry/backoff/dead-letter + Npgsql `FOR UPDATE SKIP LOCKED` (lease) eklendi; aşağıdaki K2 maddesine bkz. 9 modül context'inde `AddOutboxRetryFields` migration'ı üretildi.
>
> - **Y4** (comp. denetim) — **TAMAMLANDI.** Redis fiilen kullanılıyor (ADR-0004 Seçenek B), tümü fail-open:
>   - **Dağıtık rate limiting:** `DistributedRateLimitMiddleware` (Redis, IP-partition, yol tabanlı). Yerleşik `AddRateLimiter` kaldırıldı. (O1 kapandı.)
>   - **Login brute-force kilidi:** `RedisLoginAttemptThrottle` (5 başarısızlıkta 15 dk hesap kilidi, `identity.too_many_attempts` → 429).
>   - **Token blacklist:** `RedisTokenBlacklist` (logout'ta erişim token'ı `jti` ile kalan ömrü boyunca kara liste; JWT `OnTokenValidated`'da kontrol → anlık iptal).
>   - **Idempotency:** `IdempotencyMiddleware` (`Idempotency-Key` header'lı mutasyon uçları; tamamlanmış yanıtı tekrar oynatır, işlenen istekte 409). Ortak dayanıklı altyapı: `ResilientRedisExecutor`.
>
> - **Y7** (comp. denetim, mobil) — **TAMAMLANDI.** Token'lar artık düz-metin `SharedPreferences`'a yazılmıyor: `UserSessionModel.toCache()` yalnız gizli-olmayan profil taşır, `restoreSession` access/refresh token'ı **secure storage**'dan okur; Android'de `EncryptedSharedPreferences` (aOptions) + `allowBackup="false"`. Mobil test: `toCache` token içermez + `restoreSession` secure storage'dan yeniden kurar (21/21 flutter testi yeşil).
>
> **✅ Aşama 1 TAMAMLANDI (2026-07-02):** K5, Y1, Y3, Y4 (rate limit + login kilidi + token blacklist + idempotency), Y7.
>
> **✅ Aşama 2 TAMAMLANDI (2026-07-06):**
> - **Y2** (comp. denetim, mimari testler) — **YAPILDI.** (1) **Cross-module referans yasağı** mimari testi eklendi (`Modules_Should_Not_Reference_Other_Modules`); tetikleyici olarak son kalan ihlal (`Assignments.Application → LessonSessions.Application`) giderildi: `ILessonSessionAccessService`+`LessonSessionDetails` **`Shared/Contracts`**'a taşındı (paylaşılan read kontratı), Assignments artık LessonSessions'a referans vermiyor. (2) **Liste IDOR/varsayılan-deny** davranışsal koruması: `LessonSessionListAuthorizationTests` (server sahiplik filtresini zorlar; başka öğretmenin id'si yok sayılır; unauth reddedilir).
> - **`AuthorizationCoverageTests` blind-spot düzeltmesi (2026-07-06):** Test, **Study**'nin 14 command/query'sini "authorizer'sız" sanıyordu. Aslında Study **korumalı** — açık-generik `StudyOwnershipCommandAuthorizer<T>`/`StudyOwnershipQueryAuthorizer<T>` (`T : IStudentScopedRequest`, `StudyOwnershipGuard` ile) DI'da her somut tip için kapalı olarak kayıtlı; startup validator geçiyor, app başlıyor. Reflection-tabanlı test yalnız **kapalı** authorizer'ları görüyordu. Test, açık-generik authorizer'ları kısıt (constraint) arayüzü üzerinden tanıyacak şekilde genişletildi (gerçek bir güvenlik açığı yoktu; redundant authorizer eklenmedi).
> - ✅ **`AuthorizationCoverageTests` sessiz atlamaları kapandı (P15 T9, 2026-11-14 — rapor §1.6):** yükleyici yol listesi alan bir dikişe (`ModuleAssemblyScan`) ayrıldı; eksik dosya, yüklenemeyen assembly ve kısmen yüklenen tipler (`ReflectionTypeLoadException`) artık **test hatası**; ayrıca `ModuleAssemblies.All`'daki her modülün `Application` assembly'si taranmış olmalı (boş tarama yeşil geçmez). Kırmızı: geçici dizinde bozuk `.dll`. **Sınır bilinçli:** test authorizer'ın *varlığına* bakar; sahiplik kuralı `RealCrossTenantAccessTests` (iki öğretmen/öğrenci/veli, gerçek Postgres — yazamaz, okuyamaz, iptal edilen veli göremez), authorizer'a tabi olmayan event handler/port kuralları kendi senaryo testlerinde (ör. otomatik ders ücreti yalnız yaşayan bağda — m07 kural 11).
> - **M14** (Testcontainers gerçek-DB testleri) — **YAPILDI.** `tests/Integration/` altında gerçek Postgres + Redis container'larıyla 3 test: (1) migration'lar gerçek Postgres'e uygulanıyor + unique constraint zorlanıyor, (2) **K5** outbox `FOR UPDATE SKIP LOCKED` + retry/dead-letter gerçek Postgres'te (Aşama 1'de yalnız derlemede doğrulanmıştı), (3) **Y4** dağıtık rate limiter gerçek Redis'e karşı 429 (fail-open değil). Docker yoksa `Skip.IfNot` ile zarifçe atlanır. Paket: 45 test (Docker'lı) / 42 + 3 skip (Docker'sız). CI'da (GitHub runner) Docker mevcut → koşar.

---

## 🔴 KRİTİK

### ✅ K1 — Outbox varsayılan olarak KAPALI → integration event'ler hiç yayınlanmıyor — **Düzeltildi 2026-08-25**
`src/API.Host/appsettings.json` **ve** `appsettings.Development.json`:
```json
"Outbox": { "DispatchEnabled": false }
```
`OutboxDispatcher.ExecuteAsync` ilk satırda bunu görüp **çıkıyor** (`return`). Sonuç:
- `LessonSessionCompletedDomainEvent` → Assignments takip akışı **tetiklenmez**.
- `LessonScheduledDomainEvent` → Notifications hatırlatma kaydı **oluşmaz**.
- Modüller arası tüm event tabanlı akış **ölü**.

Olay mesajları `outbox_messages` tablosunda birikir ama asla işlenmez. Demo/dev dahil her ortamda kapalı.

**Öneri:** En azından Development'ta `true` yap; prod için bilinçli bir karar + izleme ekle. Açık unutulmasın diye startup'ta bir uyarı log'u bas.

✅ _Çözüm (2026-08-25):_ `DispatchEnabled` her iki appsettings'te (`appsettings.json` + `appsettings.Development.json`) `true` yapıldı → dispatcher artık poll döngüsünü çalıştırıp bekleyen mesajları event bus'a yayınlıyor. `OutboxDispatcher.ExecuteAsync` startup gözlemlenebilirliği için düzeltildi: **kapalıyken `LogWarning`** ("Integration events will accumulate … never be published" — açık unutulmasın diye Information değil Warning), **açıkken `LogInformation`** (poll aralığı + batch boyutu). Doğrulama: Development'ta uygulama başlatıldı → `"Outbox dispatcher enabled; polling every 15s (batch size 20)."` log'u çıktı, `outbox_messages` poll sorgusu koştu, `Now listening on: http://localhost:5296` + hatasız (`Outbox dispatch cycle failed` yok).

---

### ✅ K2 — Outbox: mesaj başına hata izolasyonu / retry / dead-letter yok — **Düzeltildi 2026-07-01 (comp. denetim K5)**
`OutboxProcessor.DispatchPendingAsync`: bir handler **exception fırlatırsa**, `MarkProcessedAsync` hiç çalışmaz → **tüm batch** (zaten başarıyla yayınlanmış mesajlar dahil) yeniden işlenir → **çift teslimat**. Tek bir "zehirli mesaj" tüm kuyruğu bloklar. `OutboxMessage.Error` kolonu tanımlı ama doldurulmuyor; retry/max-deneme/dead-letter yok.

**Öneri:** Mesaj başına try/catch; başarılıyı işaretle, başarısıza `Error` + `RetryCount` yaz; eşik aşılınca dead-letter. Handler'ları **idempotent** yap (bkz. Y4).

✅ _Çözüm (2026-07-01):_ `IOutboxStore` tek `ProcessPendingAsync(publish)` sözleşmesine indirildi; `EfOutboxStore` artık **mesaj-başına** işliyor: başarı → `ProcessedOnUtc`; başarısızlık → `RetryCount++` + `Error` + üstel `NextAttemptUtc` backoff; `RetryCount >= MaxRetryCount` → `DeadLetteredOnUtc` (kuyruktan çıkar). Zehirli mesaj artık sıradaki sağlıklı mesajı bloklamıyor (tüm sonuçlar tek `SaveChanges`). Deserialize başarısızlığı da sessizce düşürülmüyor, hata olarak işleniyor. Çoklu-instance: Npgsql'de `FOR UPDATE SKIP LOCKED` + lease (`NextAttemptUtc`) ile satır sahiplenme; InMemory'de sıralı seçim. Yeni alanlar için 9 context'te migration. Testler: `tests/Unit/OutboxRetryAndDeadLetterTests.cs`. (K4 context-başına izolasyon korunuyor.)
✅ _Doğrulama (2026-07-06, M14):_ SKIP LOCKED + retry/dead-letter artık gerçek Postgres'e karşı test ediliyor (`tests/Integration/RealOutboxIntegrationTests.cs`, Testcontainers). ✅ _Tüketici inbox/dedup idempotency'si (2026-08-26):_ Artık kapalı — bkz. aşağıdaki **Y4** (ortak `inbox_messages` + `IdempotentIntegrationEventHandler`). HTTP idempotency (comp. denetim Y4, `Idempotency-Key`) ayrı bir mekanizma olarak kalmaya devam ediyor.

---

### ✅ K3 — Yetkilendirme "opt-in" ve emniyet ağı yok → eksik kayıt = sessiz açık erişim — **Düzeltildi 2026-06-26**
`AuthorizationCoverageValidator` startup guard eklendi: her `ICommandHandler`/`IQueryHandler` için ya authorizer kaydı ya `IAllowAnonymous` işareti zorunlu — eksik kayıt uygulama başlatılırken hata fırlatır.
`TeacherProfileQueryAuthorizer` (`GetTeacherProfileByUserIdQuery`) eklendi — öğretmen profili artık yalnızca kimlik doğrulanmış kullanıcılara açık.
`CompleteLessonScheduleCommand` için `LessonScheduleCommandAuthorizer` kaydı eklendi; handler DI'a bağlandı.

---

## 🟠 YÜKSEK

### ✅ Y1 — Öğretmen kendi profilini "doğrulanmış" yapabiliyor (yetki yükseltme) — **Düzeltildi 2026-06-24 (backend) / 2026-06-26 (client)**
`IsVerified`, `UpsertTeacherProfileRequest`, `UpdateTeacherProfileCommand` ve `TeacherProfile.Update()` metodundan kaldırıldı. Client `toUpdatePayload()` artık `isVerified` göndermez; regresyon testi eklendi.
> **Kalan:** Admin-only `PUT /profiles/{userId}/verification` endpoint + `TeacherVerifiedDomainEvent` henüz eklenmedi.

### ✅ Y2 — JWT imza anahtarı ve DB parolası repoda + zayıf varsayılanlar — **JWT 2026-07-01 · DB 2026-09-02 (P01)**
`appsettings.json`: `Jwt:SigningKey = "change-this-development-signing-key"`, Postgres `Password=postgres`; kod-içi varsayılan `"replace-with-a-long-development-key"`. Prod'da override edilmezse token **taklit edilebilir**.

**Öneri:** Sırları environment/secret manager'dan al; varsayılanı boş bırak + **prod'da yoksa fail-fast**.

✅ _JWT düzeltmesi (comp. denetim Y3, 2026-07-01):_ Gömülü anahtar `appsettings.json`'dan çıkarıldı (`SigningKey: ""`), `JwtOptions` varsayılanı da boş. Yeni `JwtSigningKeyGuard` startup'ta fail-fast doğrular: boş/yer-tutucu/`< 32 bayt` anahtar reddedilir (`Program.cs` + `ConfigurationHealthCheck` aynı guard'ı kullanır). Dev anahtarı yalnız `appsettings.Development.json`'da (prod'da yüklenmez). Birim testi: `tests/Unit/JwtSigningKeyGuardTests.cs`.
✅ _DB düzeltmesi (A-06, 2026-09-02, P01):_ Bağlantı dizesi `appsettings.json`'dan çıkarıldı (`"Postgres": ""`); üretimde
`ConnectionStrings__Postgres` ortam değişkeninden gelir (Render'da managed DB'ye bağlı). Yeni `ConnectionStringGuard`
startup'ta (`Program.cs`) ve `/health/ready`'de (`ConfigurationHealthCheck`) doğrular: boş dize her ortamda,
varsayılan/zayıf parola (`postgres`, `changeme`, boş parola vb.) yalnız üretimde reddedilir; `InMemory:` önekli dizeler muaftır.
Geliştirme dizesi yalnız `appsettings.Development.json`'da (`InMemory:development`). Birim testi: `tests/Unit/ConnectionStringGuardTests.cs`.

### ✅ Y3 — Mobil: token yenileme (refresh) akışı yok → kullanıcı 60 dk'da bir atılıyor — **Düzeltildi 2026-06-25**
`TokenStorage`'a `readRefreshToken`/`writeRefreshToken` eklendi. `TokenRefreshInterceptor` (`QueuedInterceptorsWrapper`) 401'de `POST /api/identity/refresh` ile sessiz yenileme yapar; yenileme de başarısız olursa `_onUnauthorized` callback'i tetikler. `ApiClient` lazy closure ile `AuthRepository.refreshSession()`'ı çağırır (döngüsel bağımlılık önlendi).

✅ _Sertleştirme (P15 T7, 2026-11-14 — N-63, N-67):_ interceptor düz `Interceptor` + **tek uçuş** (paralel 401'ler tek yenilemeyi bekler; token değiştiyse yenilemeden tekrar); çıkış **yalnız** refresh 400/401 dönerse — ağ hatası/zaman aşımı/5xx'te oturum korunur. `AuthRepositoryImpl.refreshSession` de tekilleştirir. Bkz. [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §7.

### ✅ Y4 — Handler'lar idempotent değil (at-least-once teslimat varsayımıyla çelişir) — **Düzeltildi 2026-08-26**
Mevcut integration event handler'ları (örn. `LessonSessionCompletedIntegrationEventHandler`) tekrarı engellemiyor → çift kayıt riski.

**Öneri (eski):** İşlenen `EventId`'leri modül bazında "inbox/processed" tablosunda tut; tekrarı atla.

✅ _Çözüm (2026-08-26):_ Paylaşılan `InboxMessage` entity'si + `inbox_messages` tablosu (composite PK `(EventId, Handler)`) her modül `DbContext`'ine eklendi, ve ortak `IdempotentIntegrationEventHandler` taban sınıfı yazıldı: handler önce `(EventId, Handler)` ile inbox'ta bir kayıt olup olmadığına bakar (guard), yoksa `ApplyAsync` iş yazımlarını **staged** (henüz kaydedilmemiş) olarak hazırlar, taban sınıf inbox-işaretleme + iş yazımını **tek transaction'da atomik commit** eder — ya ikisi de yazılır ya hiçbiri (at-least-once teslimat artık güvenli, çift kayıt riski yok). 11 mantıksal tüketici handler (15 somut sınıf) bu taban sınıfa geçirildi: ProgressTracking (×2), Parents (×4 projeksiyon), Notifications (×2), Assignments (×1), Students (×1), ve 5 `StudentMerged` handler (Payments/Study/Assignments/Scheduling/LessonSessions). Eski modül-bazlı dedup tabloları — ProgressTracking'in `processed_events`'i ve Parents'ın `processed_integration_events`'i — kaldırıldı; Notifications'ın `processed_integration_events`'i **korundu** (yalnızca `ParentWeeklySummaryService` haftalık-özet dedup'ı için, event idempotency'siyle ilgisiz). 12 modül context'i için migration üretildi. Bkz. [`veri_modeli.md`](veri_modeli.md) (`inbox_messages` ER) ve [`00_genel_bakis.md`](00_genel_bakis.md) (handler envanteri).

### ✅ Y5 — Bildirimler gerçekten gönderilmiyor (yalnızca "gönderildi" işaretleniyor) — **Düzeltildi 2026-09-03 (P03 Task 1-3)**
`NotificationDispatchProcessor.DispatchDueRemindersAsync` `reminder.MarkSent(...)` diyor ama **FCM/APNs'e hiçbir şey göndermiyor**du. Push altyapısı (PRD Faz 0.7) bağlı değildi (bkz. [`m11_notifications.md`](m11_notifications.md)).

**Öneri (eski):** Gerçek push entegrasyonu (FCM) + cihaz token kaydı.

✅ _Çözüm (2026-09-03, P03):_ (1) `DeviceToken` aggregate + `POST`/`DELETE /api/notifications/device-tokens` ile cihaz token kaydı (Task 1). (2) `Shared/Infrastructure/Push`: `IPushSender` soyutlaması, `FcmPushSender` (FCM HTTP v1 + service-account OAuth2) ve `LoggingPushSender`; sağlayıcı `Push:Provider` ile seçilir, `PushOptionsGuard` startup'ta fail-fast eder (K-02, Task 2). (3) Dispatcher artık alıcının aktif token'larına **gerçekten gönderir**: en az biri teslim edilirse `MarkSent`, hiçbiri edilemezse `MarkFailed` — durum `Pending` kalır ve bir sonraki turda tekrar denenir, 5. denemede `Failed`; FCM `UNREGISTERED`/`INVALID_ARGUMENT` yanıtında token `Deactivate` edilir (Task 3). Kalan alt madde: kendi-ders (Ç-06) hatırlatmalarında `TeacherUserId` boş olduğu için öğrenciye teslim yok — `StudentId → UserId` eşlemesi P07 read-model'e bağlı.

---

## 🟡 ORTA

### ✅ O1 — Rate limiting yalnızca Identity'de uygulanıyor — **Düzeltildi 2026-07-01 (comp. denetim Y4)**
`"auth"` ve `"default"` limiter tanımlı ama yalnızca Identity `.RequireRateLimiting("auth")` kullanıyor; `"default"` (120/dk) hiçbir endpoint'e bağlı değil. **Öneri:** `CreateModuleGroup`'ta varsayılan limiter veya global limiter.

✅ _Çözüm (2026-07-01):_ Yerleşik `AddRateLimiter` yerine `DistributedRateLimitMiddleware` — Redis destekli, IP-partition'lı, **yol tabanlı** politika: `/api/identity/*` → `auth` (10/dk), diğer `/api/*` → `default` (120/dk), gerisi limitsiz. Böylece **tüm iş uçları** otomatik `default` limitine tabi. Redis erişilemezse **fail-open** (ADR-0004 kararı). Ayarlar `appsettings.json:RateLimiting`. Testler: `tests/Unit/DistributedRateLimitMiddlewareTests.cs` + ✅ gerçek Redis'e karşı `tests/Integration/RealRedisIntegrationTests.cs` (M14, 2026-07-06 — limit aşımında 429, fail-open değil).

### ✅ O2 — Sorgu authorizer'ları varlığı iki kez yüklüyor (çift DB sorgusu) — **Düzeltildi 2026-09-04 (P07)**
Örn. `GetStudentProfileByIdQuery`: authorizer profili yükler, handler **aynı** profili tekrar yükler. **Öneri:** request-context cache veya erişim kontrolünü handler'a entegre et.

✅ _Düzeltildi (2026-09-04, P07 Task 3):_ İki mekanizma, bkz. [`../architecture/backend.md`](../architecture/backend.md) §7.2.
(a) **Birincil anahtar aramaları** repository katmanında `FindAsync`'e çevrildi (19 metot, 14 dosya) — varlık aynı
`DbContext`'te izleniyorsa ikinci `SELECT` hiç üretilmez. (b) **Anahtar dışı / `Include`'lu** yüklemeler için
`IRequestEntityCache` (scoped) + `GetOrLoadAsync` eklendi; Students profil ve Payments kayıt yolları bağlandı.
Ölçüm gerçek Postgres'te EF `Executed DbCommand` günlüğü sayılarak yapıldı (`tests/Integration/RealSingleEntityLoadTests.cs`):
`student_profiles` → 1 `SELECT`, `lesson_schedules` → 2'den 1'e düştü.

### ✅ O3 — `MarkProcessedAsync` `DateTime.UtcNow` kullanıyor (IClock değil) — **Düzeltildi 2026-07-01**
Kod tabanı test edilebilirlik için `IClock` kullanırken `EfOutboxStore` doğrudan `DateTime.UtcNow` çağırıyor — tutarsız.

✅ K5 refactor'ünde `MarkProcessedAsync` kaldırıldı; `EfOutboxStore` artık `IClock` enjekte ediyor (`clock.UtcNow`), böylece işleme zaman damgaları test edilebilir (`FixedClock` ile).

### ✅ O4 — `CommandDispatcher`/`QueryDispatcher` ağır `dynamic` + reflection — **Düzeltildi 2026-09-04 (P07)**
Her dispatch'te `MakeGenericType` + `dynamic` + `GetMethod(...).Invoke`. Cache/pipeline yok. **Öneri:** tip başına derlenmiş delegate cache veya MediatR benzeri pipeline.

✅ _Düzeltildi (2026-09-04, P07 Task 4):_ İstek tipi başına **derlenmiş delegate önbelleği** eklendi
(`DispatcherInvokerCache`, `DispatcherHandlerInvokerCache<TResponse>`, `DispatcherFailureFactory<TResponse>` —
`Shared/Infrastructure/Application`), bkz. [`../architecture/backend.md`](../architecture/backend.md) §4.2.
`MakeGenericType` + `Expression.Compile` maliyeti tip başına **bir kez** ödenir; sonrası `ConcurrentDictionary` araması.
`dynamic` üç yerden kalktı: validator/authorizer çağrısı, handler çağrısı ve hata sonucu üretimi
(`GetMethod("Failure").Invoke(...)` → derlenmiş fabrika). Dış sözleşme (`ICommandDispatcher`/`IQueryDispatcher`) değişmedi;
`grep -rn "dynamic" src/Shared/Infrastructure/Application` **boş**. Testler: `tests/Unit/DispatcherCacheTests.cs` (7 test —
önbellek tip başına bir kez kuruluyor, validator → authorizer → handler sırası, hata durumunda kısa devre).
**Hâlâ yok:** genel bir pipeline kancası (logging/transaction/behavior zinciri) — öneri parçası olarak açık.

### ✅ O5 — Modüller arası okuma (read-model) mekanizması yok — **Düzeltildi 2026-09-04 (P07)**
Payments özeti, veli paneli (M09), eşleştirme arama (M12), raporlama (M14) birden çok modülün verisini birleştirecek. Şu an yalnız integration event var; **senkron sorgu/kontrat** yok. **Öneri:** `Shared/Contracts` read kontratları veya ACL/projeksiyon. (Bu, M09/M12/M14 için **kritik önkoşul**.)

✅ _Düzeltildi (2026-09-04, P07):_ Kanonik **projeksiyon deseni** kuruldu — `ProjectionEntity` (Shared.Kernel) + `ProjectionHandler<TEvent>` + `IProjectionRebuilder` (Shared.Infrastructure/Projections), bkz. [`../architecture/backend.md`](../architecture/backend.md) §7.1. İlk gerçek tüketici: **M14 öğretmen dashboard projeksiyonu** (`teacher_dashboard_projections`, event-beslemeli, idempotent, canlı doğrulandı). Senkron çapraz-modül okuma **hâlâ yasak**.

### O6 — `LessonSchedule` durum yaşam döngüsü kısmen
✅ _Düzeltme (2026-06-24):_ **Ders çakışması kontrolü ASLINDA mevcut** — `HasTeacherConflictAsync` → `scheduling.teacher_conflict` (409) ve `scheduling.invalid_range` (400) koddan doğrulandı; teacher-lesson liste endpoint'i `startAtUtc`/`endAtUtc` tarih filtresi alıyor.
✅ _Düzeltme (2026-06-26):_ **`Planned → Completed` geçişi eklendi** — `Complete(updatedOnUtc)` domain metodu, `CompleteLessonScheduleCommand`/handler, `POST /lessons/{id}/complete` endpoint ve mobil `completeLesson` cubit metodu + UI butonu eklendi.
✅ _Düzeltme (Dilim A, 2026-07-18):_ **`Reschedule` geçişi** (`Reschedule()` + `POST /lessons/{id}/reschedule`, erteleme geçmişi), **online ders linki** (`MeetingUrl` ayrı alan), **tatil/blackout** (`TimeOffBlock` + endpoint'ler), **tekrar occurrence yönetimi** (`LessonOccurrenceException` + `RecurrenceExpander` istisnaları + `Scope`), **iptal nedeni + ücretlendirme + sil** (B-08/B-09) koddan doğrulandı. **Öğrenci tarafı çakışma önceliği** zaten mevcuttu (`StudyScheduleConflict`).
**Hâlâ eksik:** Öğretmen `LessonSchedule` listesinin kendi tarafında tekrar açılımı (öğrenci birleşik takviminde açılıyor).

### O7 — Test kapsamı çok düşük
Yalnız ~5 test dosyası. Handler/authorizer/outbox/domain davranışları test edilmiyor; Y1, K3 bir test olsa yakalanırdı.

### ✅ O8 — Dosya yükleme/depolama servisi — **Düzeltildi 2026-09-03 (P04)**
**Öneri (eski):** `Shared/`'da `IFileStorage` soyutlaması + sağlayıcı (yerel/S3/Azure Blob).

✅ _Çözüm (2026-09-03, P04/K-03):_ Port **`Shared.Kernel/Storage`**'da (`IFileStorage`, `StoredFile`, `FileValidation` — `IClock`/`ICurrentUser` deseni; Application katmanı Infrastructure'ı referanslamadığı için port Kernel'de durur), implementasyonlar `Shared.Infrastructure/Storage`'da: `LocalFileStorage` (dev, **path traversal** korumalı) ve `S3FileStorage` (AWSSDK.S3; AWS S3 / Cloudflare R2 / MinIO — `ServiceUrl` doluysa path-style). Sağlayıcı `Storage:Provider` ile seçilir; `StorageOptionsGuard` eksik S3 ayarında startup'ı fail-fast durdurur. Ortak kural: 10 MB + 13 uzantılık beyaz liste (`storage.file_too_large`/`storage.unsupported_type`/`storage.empty_file`). **Public bucket yok** — her indirme uygulama üzerinden yetkiyle akıtılır.

Tüketiciler: (1) ödev teslimi `IAssignmentFileStorage`'dan ortak soyutlamaya taşındı, eski sınıflar silindi, göç script'i yazıldı (`scripts/migrate-assignment-files.sh`); (2) `TeacherProfile.ProfilePhotoUrl` → `ProfilePhotoKey` + `POST`/`GET /api/teachers/profiles/{userId}/photo`; (3) **`LessonResource`** aggregate'i eklendi (dosya veya bağlantı, 4 uç, yumuşak silme).

**Canlı doğrulama (2026-09-03):** Yerel **MinIO** (S3-uyumlu) bucket'ına karşı uçtan uca tur — profil fotoğrafı ve ders kaynağı yüklendi, bucket'ta nesne oluştu, indirme aynı içeriği döndürdü; yabancı kullanıcı **403**, kimliksiz **401**, `.exe` **400**, 11 MB **400**. Bu turda gerçek iki hata yakalandı ve düzeltildi: `DisablePayloadSigning` yalnız HTTPS'te geçerli, ve AWS SDK `PutObjectAsync` sonrası akışı kapattığı için boyut çağrıdan önce okunmalı (ikisi de regresyon testine bağlandı).

---

## ⚪ DÜŞÜK / HİJYEN
- **D1** — Stale build artefaktları: `src/**/bin/Debug/net10.0/` klasörleri (hedef `net9.0` olmalı). `bin/obj` `.gitignore`'da olmalı; csproj hedef framework tutarlılığı kontrol edilmeli.
- **D2** — Kök dizinde `tmp_api_stdout.log`, `tmp_api_stderr.log` — temizlenmeli.
- **D3** — Placeholder dosyalar: `Shared/*/Class1.cs` ve boş `AssemblyReference.cs` kaldırılmalı.
- **D4** — ✅ _Düzeltildi._ Doküman tutarsızlığı (.NET 8/10) gerçek hedef **.NET 9** ile hizalandı.
- **D5** — `StudentProfileQueryAuthorizer` 3 arayüzle 3 kez `AddScoped` → 3 instance (gereksiz).
- **D7** — ✅ _Düzeltildi (2026-09-02, P01)._ CI derleme adımı (`dotnet build EgitimUssu.slnx -warnaserror`) 13 hata veriyordu:
  3× `CS1573` (eksik XML `<param>` — `StudySession.Pause/Complete`), 1× `CS8604` (`LessonSchedule.LessonFormat` nullable enum
  → `LessonScheduleResponse`), 9× `CS8602` (`Result.Value` dereference, 4 birim test dosyası). Hepsi kapatıldı; davranış değişmedi.
- **D8** — `src/Shared/Infrastructure/Design/DesignTimeDbContextFactoryBase.cs` hâlâ `Password=postgres` içeren bir varsayılan
  taşıyor. Yalnız `dotnet ef` tasarım zamanını besliyor (çalışma zamanına girmiyor) — yine de temizlenmeli (**P13**).
- **D6** — İskelet modüller (`Matching`, `Reviews`, `Reporting`) yalnız `/status` döndürüyor — beklenen (yol haritası), tasarımları [`m12`](m12_matching.md)/[`m13`](m13_reviews.md)/[`m14`](m14_reporting.md)'te. (`Study` 🟢, `Parents` 🟢, `ProgressTracking` 🟡 artık gerçek endpoint'ler.)

---

## ➕ Yeni Modüller İçin Mimari Notlar (promp.txt genişlemesi)
- **Mesajlaşma (M16):** Kendi şeması + DbContext; gerçek-zamanlı için (ileride) SignalR/WebSocket düşünülebilir, ama ilk sürüm poll/REST + bildirim yeterli. Modül sınırı: katılımcı doğrulama (öğretmen↔öğrenci/veli) Identity/ilişki verisiyle.
- **Üyelik (M17):** Entitlement (premium/limit) bilgisi integration event ile diğer modüllere yayılmalı (modül sınırı — doğrudan DB okuma yok). Reklam istemci tarafı + config.
- **Geri Bildirim/Şikayet (M18):** M13 `ReviewFlag` + M16 mesaj şikayeti ile **ortak moderasyon kuyruğu**; karar `ModerationAction` event'iyle yayılır.
- **O5 önkoşulu:** M09/M14/M12 read-model gerektirir → modüller arası okuma mekanizması bu modüllerden önce çözülmeli.

---

## 🔍 2026-09-05 Denetimi — A Serisi (hepsi ✅ kapatıldı)

> Bu tur, yukarıdaki K/Y/O maddelerinden **bağımsız** bir incelemedir: kapalı görünen ya da hiç yer almayan
> bulgular arandı. Ayrıntılı gerekçe ve kanıtlar: [`../denetim/2026-09-05_mimari_aciklik_denetimi.md`](../denetim/2026-09-05_mimari_aciklik_denetimi.md).
> Dal: `fix/mimari-aciklar`. Kapsam dışı bırakılan tek madde **A-19** (e-posta doğrulamasının zorunlu
> kılınması) — mevcut kullanıcıların akışını değiştiren bir **ürün kararı**, karar bekliyor.

| Kod | Bulgu | Çözüm |
|-----|-------|-------|
| 🔴 **A-01** | `RedisOptions` varsayılanı `"localhost:6379"` idi; `Redis__Configuration` verilmeyen üretim dağıtımı sorunsuz açılıyor, hız sınırı + token kara listesi + giriş kilidi + idempotency **birlikte** fail-open'a düşüyordu. `ConfigurationHealthCheck` yalnız "boş mu" baktığı için `/health/ready` yine `Healthy` diyordu. | `RedisOptionsGuard` (üretimde boş/loopback reddedilir) + `RedisConnectionHealthCheck` (gerçek `PING`, sonuç `Degraded`) + `egitimussu.redis.unavailable` sayacı (koruma adına göre etiketli). Varsayılan boş; geliştirme adresi `appsettings.Development.json`'da. |
| 🔴 **A-02** | İstemci IP'si `X-Forwarded-For`'un **ilk** girdisinden okunuyordu; istemci her istekte uydurma değer gönderip taze kova açabiliyor, `/api/identity/*` 10/dk sınırı tümüyle atlanabiliyordu. | `ClientIpResolver` — IP `RateLimiting:TrustedProxyHopCount` kadar **sondan** okunur (Render 1, yerel 0). Hız sınırı ve giriş kilidi aynı kaynağı kullanır. |
| 🟠 **A-03** | `DispatchingEventBus` her integration event'i **payload'ıyla** Information seviyesinde logluyordu; `UserRegisteredDomainEvent` e-postayı, `MessageSentDomainEvent` özel mesaj önizlemesini log'a düşürüyordu. | Seviye `Debug`, payload çıkarıldı (`EventId` + `Name` + `Module` korelasyona yeter). |
| 🟠 **A-04** | E-posta `SaveChanges`'ten sonra, istek içinden SMTP'ye gidiyor ve hatayı yeniden fırlatıyordu: SMTP kapalıyken kullanıcı **oluşmuş** ama istek 500; tekrar deneme `duplicate_email` → çıkmaz sokak. | `outgoing_emails` kuyruğu + `IEmailQueue` (iş yazımıyla **aynı transaction'da** stage) + `OutgoingEmailDispatcher` (retry/backoff/dead-letter). Push'un çoktan aldığı tedavi. |
| 🟠 **A-05** | `ClaimParentInvite` önce bağı (Parents), **sonra** daveti (Students) yazıyordu; ikinci yazım düşerse **aynı davet kodu tekrar kullanılabiliyordu**. | `MarkClaimedAsync` → `TryConsumeAsync` (atomik koşullu güncelleme) + `ReleaseAsync` (telafi). Davet **önce** tüketilir; bağ yazılamazsa geri verilir. |
| 🟠 **A-06** | Parola sıfırlama ve rol değişimi dolaşımdaki **erişim token'ını** iptal etmiyordu (60 dk geçerli kalıyordu). | JWT'ye `iat`; `ITokenBlacklist`'e kullanıcı bazlı iptal damgası (`jti` kara listesi + damga tek Redis turunda); Application portu `ITokenRevocationService`. **✅ Genişletildi (P15 T7, 2026-11-14 — N-31, N-32):** cihaz iptali / "diğerlerini kapat" / çıkış artık `sid` iptal kümesine yazar, KVKK kapanışı damgayı yazar, `OnTokenValidated` hesap durumunu da sorar; damga ms çözünürlüğünde (`iat_ms`). |
| 🟠 **A-07** | Beş modül arka plan servisi düz `while + Task.Delay` idi; kilit/leader election yoktu → ikinci instance = **çift push bildirimi** (farklı `EventId` ürettiği için inbox dedup'ı da yakalamıyordu). | `job_leases` + `JobLeaseStore` (`ON CONFLICT DO UPDATE … WHERE süresi dolmuş`) + `LeasedBackgroundService` ortak tabanı. **✅ Düzeltildi (P15 T8, 2026-11-14 — N-41):** koşulda sahip kontrolü yoktu, sahip kendi kirasına takılıyor ve 15–30 sn'lik işler ~5 dk'da bir koşuyordu → `WHERE süresi dolmuş OR OwnerId = EXCLUDED.OwnerId` (InMemory dalında da); kirasız kalan `AccountDeletionService` ve `DataRetentionService` `LeasedBackgroundService`'e alındı. Ayrıntı: `../architecture/backend.md` §7.3. |
| 🟡 **A-08** | Kullanıcı deposu her çağrıda `RefreshSessions` + `SecurityTokens` koleksiyonlarının **tamamını** yüklüyordu (30 gün ömürlü, hiç silinmeyen satırlar; üç `Include` → kartezyen büyüme). | Filtreli `Include` (yalnız etkin oturum / kullanılabilir token) + girişte kullanıcıya özel budama. |
| 🟡 **A-09** | `outbox_messages` / `inbox_messages` hiç budanmıyordu; tek `HasIndex(ProcessedOnUtc)` claim sorgusunu karşılamıyordu. | Kısmi indeks `ix_outbox_messages_pending` + `Outbox:RetentionDays` (14) budaması. Dead-letter satırlarına dokunulmaz. |
| 🟡 **A-10** | `outbox.pending` göstergesi etiketsizdi; 15 modül aynı zaman serisine yazdığı için dışa aktarılan değer **son modülünki** oluyordu. | `RecordOutboxPending(module, count)` — `module` etiketi (kardinalite 15). |
| 🟡 **A-11** | `EntitlementDirectory` her hatayı yutup Free dönüyordu: ödeme yapmış kullanıcı geçici bir DB sorununda **sessizce** premium'unu kaybediyordu. | `egitimussu.entitlement.fallback` sayacı; `OperationCanceledException` artık yutulmuyor. |
| 🟡 **A-12** | Idempotency kilidi 30 sn'ydi ve yenilenmiyordu; 30 sn'yi aşan istek sürerken işaret düşüyor, tekrar `Proceed` alıp iş **iki kez** koşuyordu. | TTL 5 dk + `IIdempotencyStore.ReleaseAsync` (2xx dışı yanıt ya da istisna işareti hemen bırakır). |
| 🟡 **A-13** | `CreateModuleGroup` korumasız grup döndürüyordu; koruma her modülün bir satırı hatırlamasına bağlıydı (`Matching`/`Reviews` hiç çağırmamıştı). | Grup **varsayılan-deny**; anonim uçlar istisnayı `AllowAnonymous` ile açıkça bildirir. |
| 🟡 **A-14** | `MapOpenApi()` ortam koşulsuzdu — `/openapi/v1.json` kimliksiz erişilebilir durumda tüm API yüzeyini yayınlıyordu. | Yalnız `Development`. |
| 🟡 **A-15** | Rotasyondan sonra iptal edilmiş refresh token tekrar sunulduğunda yalnız 401 dönülüyordu; ele geçirme normal bir oturum sonu gibi görünüyordu. | Yeniden-kullanım tespiti: tüm oturumlar kapatılır + erişim token'ları iptal edilir. Süresi dolmuş token ihlal sayılmaz. **✅ Daraltıldı (P15 T7, 2026-11-14 — N-29, N-30):** meşru iptalleri de hırsızlık sayıyordu. İptal nedeni saklanır; tespit yalnız `Rotated` token **30 sn'den sonra** gelirse. Rotasyon koşullu güncellemeyle tek kazanana indirildi (eşzamanlı kaybeden düz 401). |
| 🟡 **A-16** | Giriş kilidi yalnız e-posta bazlıydı: **hesap kilitleme DoS'u** (5 hatalı denemeyle istediğini 15 dk kilitle) + spraying'e açık. | İki boyutlu: (hesap + kaynak IP) 5/15dk, (kaynak IP) 20/15dk. Küresel hesap kilidi bilinçli olarak kaldırıldı. |
| ⚪ **A-17** | HTTPS/HSTS/CSP/`nosniff`/`X-Frame-Options` yok, CORS hiç yapılandırılmamış. | `SecurityHeadersMiddleware` + yapılandırmayla açılan adlandırılmış CORS politikası (varsayılan boş). |
| ⚪ **A-18** | `UseNpgsql` çağrısında retry/komut zaman aşımı yok; `AddScoped(typeof(TContext))` EF'in kaydını gölgeliyordu. | `EnableRetryOnFailure` + `CommandTimeout(60)`; yinelenen kayıt kaldırıldı. Retry stratejisi kullanıcı-başlatımlı transaction'ı reddettiği için `IdempotentIntegrationEventHandler`'ın iş birimi `CreateExecutionStrategy` içine alındı. |
| ⚪ **A-19** | E-posta doğrulaması hiçbir kapıda zorlanmıyor (kayıtta token verilir, `PendingActivation` girişe kabul edilir, `IsEmailVerified` okunmaz). | **AÇIK — ürün kararı bekliyor.** Bilinçliyse dokümana yazılmalı; değilse kapı authorizer katmanında kurulmalı. |

> **Not (doküman-kod çelişkisi, düzeltilecek):** Yukarıdaki **O5** maddesi "senkron çapraz-modül okuma **hâlâ yasak**"
> diyor; kodda `Shared/Contracts` altında **21** senkron çapraz-modül kontratı var ve ikisi (`IParentInviteDirectory`,
> `IParentChildLinkRequestService`) **yazma** portu. Desen modüler monolit için makul; yanlış olan kuralın yazılışı.
> Önerilen ifade: *"Doğrudan DbContext erişimi yasak; çapraz-modül okuma yalnız `Shared/Contracts` portları üzerinden.
> Çapraz-modül **yazımı** event ile yapılır."*

---

## ✅ İyi Yapılmış Olanlar (Korunmalı)
- Temiz katman ayrımı + **mimari testleriyle** zorlanıyor (Application → Infrastructure yasak vb.).
- Modül başına ayrı `DbContext` + şema + migration → gerçek veri izolasyonu.
- Parola **PBKDF2**, JWT **HMAC-SHA256**, refresh token **hash'li**, kriptografik rastgele token.
- Outbox **envelope** deseni round-trip tutarlı; `DispatchingEventBus` kendi scope'unu açıyor.
- `Result<T>` + `Error` + merkezi HTTP hata eşleme (ProblemDetails).
- Mobil: token `FlutterSecureStorage`'da; feature bazlı temiz katman.

---

## 📋 Öncelikli Düzeltme Sırası (Önerilen)
1. **Y1** — Öğretmen self-verify açığını kapat (hızlı + güvenlik).
2. **K1** — Outbox'ı aç + startup uyarısı.
3. **K3** — Eksik authorizer'lar + startup guard.
4. **K2 + Y4** — Outbox hata izolasyonu/retry + idempotent handler.
5. ✅ **Y2** — Sırları config'ten çıkar, prod fail-fast. *(JWT 2026-07-01, DB 2026-09-02)*
6. **Y3** — Mobil refresh token akışı.
7. ~~**O5** — Modüller arası okuma (yeni modüllerin önkoşulu).~~ ✅ **(P07, 2026-09-04)** *(O8 dosya depolama 2026-09-03'te kapatıldı.)*
8. **O7** — Regresyon testleri. *(O2 ve O4 performans maddeleri 2026-09-04'te P07 ile kapatıldı.)*

---

*Mimari İnceleme | Güncelleme: 2026-11-14 (P15 T9: `AuthorizationCoverageTests` yüklenemeyen/taranmayan assembly'de başarısız olur; sahiplik çapraz-kiracı paketinde) · 2026-11-14 (P15 T8: A-07 düzeltildi — kira sahibince yenilenir (N-41), hesap silme ve saklama servisleri kiraya alındı) · 2026-11-14 (P15 T7: A-06 genişletildi — `sid` iptal kümesi + hesap durumu + ms damga; A-15 daraltıldı — iptal nedeni + 30 sn pencere + koşullu rotasyon; Y3 sertleştirildi — mobil tek uçuş + yalnız 400/401'de çıkış) · 2026-09-05 (**A serisi**: bağımsız açıklık denetiminin 18 maddesi kapatıldı — Redis fail-open guard/health, XFF sahteciliği, PII log sızıntısı, e-posta outbox deseni, davet tüketimi, token iptali, arka plan işi kirası, kimlik eager-loading, outbox indeks+retention, metrik etiketi, entitlement alarmı, idempotency kilidi, varsayılan-deny uç grupları, OpenAPI, refresh yeniden-kullanım tespiti, iki boyutlu giriş kilidi, güvenlik başlıkları/CORS, DB retry; **A-19 açık** — ürün kararı) · 2026-09-04 (P07 kapanışı: **O5** read-model deseni + M14 ilk projeksiyon, **O2** authorizer/handler çift sorgusu, **O4** dispatcher delegate önbelleği — üçü de kapatıldı) · 2026-09-04 (O8 kapatıldı: ortak `IFileStorage` + Local/S3 sağlayıcıları, MinIO'ya karşı canlı doğrulandı — P04) · 2026-09-03 (Y5 kapatıldı: gerçek FCM push + cihaz token + teslimat izleme — P03 Task 1-3) · 2026-09-02 (Y2 tamamen kapatıldı: `ConnectionStringGuard` ile DB sırrı repodan çıktı; hijyen D7 — CI `-warnaserror` derlemesi yeşile alındı, D8 — tasarım-zamanı varsayılan parola notu) · 2026-08-26 (Y4 kapatıldı: ortak `inbox_messages` + `IdempotentIntegrationEventHandler` ile tüketici idempotency'si) · 2026-08-25 (K1 kapatıldı — Outbox dispatcher açıldı + startup uyarı log'u; artık tüm 🔴 KRİTİK maddeler ✅). Önceki: 2026-07-18 (Dilim A takvim çekirdeği: O6 takvim boşlukları büyük ölçüde kapatıldı) — Düzeltmeler yapıldıkça güncellenmeli.*
