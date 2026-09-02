# P13 — Operasyon, Gözlemlenebilirlik ve Hijyen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Üretim işletilebilirliğini kurmak: OpenTelemetry ile izleme/metrik/log korelasyonu, API sürümleme, staging ortamı + tohum verisi, yedekleme/geri yükleme prosedürü, KVKK (hesap silme + saklama), mobil hijyen (l10n, Türkçe karakterler, kullanılmayan paketler, test kapsamı, CI).

**Architecture:** `Shared/Infrastructure/Observability` altında `AddObservability()` uzantısı: OpenTelemetry trace (ASP.NET Core + HttpClient + EF Core), metrik (istek süresi, outbox kuyruğu, push başarı oranı) ve `ILogger` korelasyonu (`TraceId` her log satırında). Sağlayıcı OTLP; yerelde konsol exporter. KVKK için Identity'ye "hesabımı sil" akışı: kişisel veriler anonimleştirilir, iş kayıtları (ödeme/ders) anonim kimlikle korunur. Mobilde ARB tabanlı l10n devreye alınır (varsayılan `tr`).

**Tech Stack:** .NET 9, OpenTelemetry .NET, xUnit; Flutter `flutter_localizations` + ARB, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (kararlar **K-10**, **K-12**; açık soru **Q5**)

## Global Constraints

- **Paralel yürütülebilir:** Bu plan P01'den sonra herhangi bir zamanda, diğer planlarla paralel koşabilir. Başka planların dosyalarını değiştirmez; yalnız `Shared`, `API.Host`, CI ve mobil altyapıya dokunur.
- **PII log'a yazılmaz:** E-posta, telefon, token, dosya içeriği hiçbir log/trace alanına girmez. `RequestContextLoggingMiddleware` maskeleme uygular.
- **Metrik maliyeti:** Yüksek kardinaliteli etiket (kullanıcı kimliği, e-posta) metrik boyutu olarak kullanılmaz.
- **Geri alınabilirlik:** Migration'lar için `Down()` metodu boş bırakılmaz; geri alma prosedürü dokümante edilir.
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: OpenTelemetry — trace, metrik, log korelasyonu (C-04)

**Files:**
- Create: `src/Shared/Infrastructure/Observability/ObservabilityExtensions.cs`
- Create: `src/Shared/Infrastructure/Observability/EgitimUssuMetrics.cs`
- Modify: `src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj` (OpenTelemetry paketleri)
- Modify: `src/API.Host/Program.cs`, `appsettings.json`
- Modify: `src/Shared/Infrastructure/Middleware/RequestContextLoggingMiddleware.cs` (TraceId + PII maskesi)
- Modify: `src/Shared/Infrastructure/Messaging/EfOutboxStore.cs` (kuyruk metrikleri)
- Test: `tests/Unit/EgitimUssuMetricsTests.cs`

**Interfaces:**
- Produces:
  - `IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)`
  - ```csharp
    public sealed class EgitimUssuMetrics
    {
        public void RecordOutboxPending(int count);
        public void RecordOutboxDispatched(string eventName, bool success);
        public void RecordPushSent(string status);          // Delivered | TokenInvalid | TransientFailure
        public void RecordEmailSent(bool success);
    }
    ```
  - Konfigürasyon: `Observability:Enabled`, `Observability:OtlpEndpoint`, `Observability:ServiceName`.

- [ ] **Step 1: Metrik testini yaz (kırmızı)** — `MeterListener` ile `RecordPushSent("Delivered")` sayacının arttığını doğrula; yüksek kardinaliteli etiket (`userId`) kabul edilmediğini doğrula (API'de böyle bir parametre yok).
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EgitimUssuMetricsTests"`
- [ ] **Step 3: Paketleri ekle**
```bash
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Extensions.Hosting
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.Http
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Exporter.Console
```
- [ ] **Step 4: `AddObservability`'yi yaz** — `Observability:Enabled=false` iken hiçbir şey kaydetmez (test/CI maliyeti sıfır). OTLP endpoint boşsa konsol exporter.
- [ ] **Step 5: Metrikleri yerleştir** — outbox bekleyen sayısı (her dispatch turunda), push sonucu (P03), e-posta sonucu (P02).
- [ ] **Step 6: Log korelasyonu + PII maskesi** — her log satırına `TraceId`; `email`, `phone`, `token`, `password` anahtarlarını içeren alanlar `***` ile maskelenir.
- [ ] **Step 7: Yerelde doğrula** — `docker run -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one` + `Observability__Enabled=true Observability__OtlpEndpoint=http://localhost:4317 dotnet run --project src/API.Host` → bir istek at → Jaeger UI'da trace görünüyor (HTTP → dispatcher → EF sorgusu zinciri).
- [ ] **Step 8: Doküman + commit**

`doc/architecture/backend.md` "Gözlemlenebilirlik" başlığı (ne toplanıyor, nasıl açılıyor, hangi metrikler alarm adayı).
```bash
git add src/Shared/Infrastructure/Observability src/API.Host tests doc
git commit -m "feat(observability): OpenTelemetry trace/metrik/log korelasyonu (C-04)"
```

---

### Task 2: API sürümleme ve sözleşme kararlılığı (C-10)

**Files:**
- Modify: `src/Shared/Infrastructure/Modules/ModuleDefinition.cs` (grup üzerinde sürüm etiketi)
- Modify: `src/API.Host/Program.cs` (OpenAPI belgesi + sürüm başlığı)
- Create: `doc/architecture/api_versioning.md`
- Test: `tests/Integration/ApiVersionHeaderTests.cs`

**Interfaces:**
- Yanıtlara `X-Api-Version` başlığı eklenir (`/api/meta/version` ile aynı değer).
- Kural: `v1` yolları **kırılmaz**; kırıcı değişiklik `"/api/v2/..."` altında yeni grupla açılır. Mevcut yollar `v1` kabul edilir (yol değişmez, belge bunu açıkça yazar).

- [ ] **Step 1: Testi yaz (kırmızı)** — herhangi bir `/api/*` yanıtında `X-Api-Version` başlığı var ve `/api/meta/version` ile aynı.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Integration/EgitimUssu.Tests.Integration.csproj --filter "FullyQualifiedName~ApiVersionHeaderTests"`
- [ ] **Step 3: Başlığı ekleyen middleware'i yaz.**
- [ ] **Step 4: Sürümleme politikasını yaz** — `doc/architecture/api_versioning.md`: kırıcı değişiklik tanımı, uyarı süresi (mobil sürümler için en az 2 sürüm), kaldırma prosedürü.
- [ ] **Step 5: Yeşil gör + commit**

```bash
dotnet test EgitimUssu.slnx
git add src/Shared src/API.Host tests doc
git commit -m "feat(api): surum basligi + surumleme politikasi (C-10)"
```

---

### Task 3: Staging ortamı ve tohum verisi (E-06)

**Files:**
- Create: `scripts/seed-demo-data.sh`
- Create: `src/API.Host/Seeding/DemoDataSeeder.cs`
- Modify: `src/API.Host/Program.cs` (yalnız `Seeding:Enabled=true` iken)
- Modify: `render.yaml` (staging servisi)
- Create: `doc/architecture/environments.md`

**Interfaces:**
- `DemoDataSeeder.SeedAsync()` — idempotent: 2 öğretmen, 5 öğrenci, 2 veli, 3 hafta ders geçmişi, ödevler, ödemeler, çalışma seansları. Var olan veriyi **asla** değiştirmez (yalnız yoksa oluşturur).
- `Seeding:Enabled` prod'da **daima false**; guard bunu zorlar (`ASPNETCORE_ENVIRONMENT=Production` ise `true` olsa bile çalışmaz + `LogWarning`).

- [ ] **Step 1: Testi yaz (kırmızı)** — seeder iki kez çalıştırıldığında kayıt sayısı değişmiyor; Production ortamında hiç çalışmıyor.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~DemoDataSeeder"`
- [ ] **Step 3: Seeder'ı yaz** (modül command'lerini dispatch ederek — doğrudan DbContext yazımı yok, iş kuralları atlanmasın).
- [ ] **Step 4: Staging servisini tanımla** — `render.yaml`'a ayrı servis + ayrı DB + `Email__Provider=Logging`, `Push__Provider=Logging`, `Seeding__Enabled=true`.
- [ ] **Step 5: Ortam dokümanı** — `doc/architecture/environments.md`: development / staging / production farkları, hangi sağlayıcılar açık, kim erişebilir.
- [ ] **Step 6: Commit**

```bash
git add src/API.Host scripts render.yaml tests doc
git commit -m "feat(ops): staging ortami + idempotent demo veri tohumlama (E-06)"
```

---

### Task 4: Yedekleme, geri yükleme ve migration geri alma (E-05)

**Files:**
- Create: `scripts/backup-db.sh`, `scripts/restore-db.sh`
- Create: `doc/architecture/backup_restore.md`
- Modify: `src/API.Host/Program.cs` (`ApplyMigrationsOnStartup` davranışı)
- Modify: `.github/workflows/backend-ci.yml` (migration geri alma kontrolü)

**Interfaces:**
- `scripts/backup-db.sh` — `pg_dump` ile tarihli yedek, opsiyonel S3'e yükleme.
- `scripts/restore-db.sh` — belirtilen yedeği hedef veritabanına geri yükler; **onay ister** (`--yes` olmadan çalışmaz).
- Prod'da `Database:ApplyMigrationsOnStartup = false`; migration ayrı bir deploy adımıdır (`dotnet ef database update`).

- [ ] **Step 1: Script'leri yaz ve yerelde dene** — Docker Postgres'e demo veri yükle → yedek al → veritabanını düşür → geri yükle → veri aynı.
- [ ] **Step 2: Prod'da otomatik migration'ı kapat** — `appsettings.json` `false` (zaten öyle); `Program.cs`'te Production'da `true` olsa bile `LogWarning` + çalıştırma.
- [ ] **Step 3: CI'ya migration geri alma kontrolü ekle** — her yeni migration için `Down()` metodunun boş olmadığını doğrulayan basit bir script (`grep`-tabanlı: `protected override void Down` gövdesinde en az bir ifade).
- [ ] **Step 4: Prosedürü yaz** — `doc/architecture/backup_restore.md`: yedek sıklığı (günlük), saklama (30 gün), geri yükleme adımları, RTO/RPO hedefleri, kim yetkili.
- [ ] **Step 5: Commit**

```bash
git add scripts .github doc src/API.Host
git commit -m "feat(ops): yedekleme/geri yukleme prosedürü + migration geri alma kontrolu (E-05)"
```

---

### Task 5: KVKK — hesap silme ve saklama (E-07)

> **Bloklayıcı:** Q5 (KVKK metinleri) hukuki metinler için gerekli; **teknik akış** metinler olmadan da yazılabilir.

**Files:**
- Modify: `src/Modules/Identity/Application/IdentityFeatures.cs` (`RequestAccountDeletionCommand`)
- Create: `src/Modules/Identity/Infrastructure/AccountDeletionService.cs` (BackgroundService)
- Create: `src/Shared/Contracts/PersonalDataContract.cs`
- Create her modülde: `PersonalDataAnonymizer` implementasyonu (Students, Teachers, Parents, Study, Messaging, Feedback)
- Modify: `src/API.Host/Admin/AdminEndpoints.cs` (silme taleplerini görme)
- Test: `tests/Unit/AccountDeletionTests.cs`

**Interfaces:**
- Produces:
  - ```csharp
    namespace EgitimUssu.Shared.Contracts;

    /// <summary>Bir modülün, silinen kullanıcıya ait kişisel verisini anonimleştirmesi.</summary>
    public interface IPersonalDataAnonymizer
    {
        string ModuleName { get; }
        Task<int> AnonymizeAsync(Guid userId, CancellationToken cancellationToken);
    }
    ```
  - `POST /api/identity/account/deletion-request` (auth) — **30 gün** bekleme süresi başlatır (fikir değiştirme hakkı), `DELETE /api/identity/account/deletion-request` iptal eder.
  - `AccountDeletionService` — süresi dolan talepler için tüm `IPersonalDataAnonymizer` implementasyonlarını çağırır, sonra hesabı `Closed` + kişisel alanları anonimleştirir.
- **Anonimleştirme kuralı:** Ad → "Silinmiş Kullanıcı", e-posta → `deleted-{hash}@egitimussu.local`, telefon → `null`, foto/dosya → `IFileStorage.DeleteAsync`. Ders/ödeme kayıtları **silinmez** (ticari kayıt), yalnız kimliksizleşir.

- [ ] **Step 1: Testleri yaz (kırmızı)** — talep 30 gün dolmadan uygulanmıyor; iptal edilebiliyor; anonimleştirme sonrası e-posta ile giriş yapılamıyor; ödeme kayıtlarının tutarı korunuyor; iki kez çalıştırma güvenli (idempotent).
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~AccountDeletionTests"`
- [ ] **Step 3: Sözleşme + 6 modül implementasyonu + servis + uçları yaz.**
- [ ] **Step 4: Saklama süreleri** — `FeedbackRetentionService` (2 yıl), `AdminAuditLog` (5 yıl), `outbox_messages` işlenmişleri (90 gün) temizleyen bakım servisi.
- [ ] **Step 5: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 6: Doküman + commit**

`doc/architecture/kvkk.md`: hangi veri nerede, saklama süreleri, silme akışı, veri sahibi hakları.
```bash
git add src doc tests
git commit -m "feat(kvkk): hesap silme + anonimlestirme + saklama sureleri (E-07)"
```

---

### Task 6: Mobil hijyen — l10n, metin kalitesi, paketler, test, CI

**Files:**
- Create: `mobile/l10n.yaml`, `mobile/lib/l10n/app_tr.arb`, `mobile/lib/l10n/app_en.arb`
- Modify: `mobile/lib/app/app.dart` (`AppLocalizations.delegate`)
- Modify: `mobile/pubspec.yaml` (kullanılmayan paket temizliği)
- Modify: tüm `mobile/lib/**` (hardcoded metinler → `AppLocalizations`)
- Create: `mobile/test/features/parent/parent_home_cubit_test.dart` ve eksik feature testleri
- Modify: `.github/workflows/build-android.yml`
- Test: `mobile/test/l10n/localization_test.dart`

**Interfaces:**
- `AppLocalizations.of(context)!.<key>` — tüm kullanıcıya görünen metinler ARB'den gelir.
- Anahtar adlandırma: `<ekran>_<eleman>` (`login_submitButton`, `more_logoutTitle`).

- [ ] **Step 1: l10n testini yaz (kırmızı)** — `app_tr.arb` ve `app_en.arb` **aynı anahtar kümesine** sahip; hiçbir anahtarın Türkçe değeri Türkçe karakter içermeyen bozuk yazım değil (ör. "Cikis" gibi yazımları yakalayan bir kontrol listesi).
- [ ] **Step 2: Kırmızı gör** — Run: `cd mobile && flutter test test/l10n/localization_test.dart`
- [ ] **Step 3: l10n altyapısını kur (D-19)**
`mobile/l10n.yaml`:
```yaml
arb-dir: lib/l10n
template-arb-file: app_tr.arb
output-localization-file: app_localizations.dart
nullable-getter: false
```
`pubspec.yaml` → `flutter: generate: true`.
- [ ] **Step 4: Metinleri taşı** — ekran ekran; **öncelik sırası**: auth → more → dashboard → student → parent. Her ekran taşındıktan sonra `flutter test` koşulur (bozulma erken yakalanır).
- [ ] **Step 5: Türkçe karakter düzeltmesi (D-20)** — `more_page.dart` başta olmak üzere "Cikis yap" → "Çıkış yap", "Odeme hatirlatmalari" → "Ödeme hatırlatmaları", "Abonelik ayarlari" → "Abonelik ayarları" vb. ARB'ye doğru yazımla girilir.
- [ ] **Step 6: Kullanılmayan paketleri temizle (D-18)** — P03 ve P10 sonrası `firebase_messaging`, `flutter_local_notifications`, `flutter_chat_ui` **kullanılıyor**; kullanılmayan kalan varsa (`dart pub deps` + `grep`) kaldır. Kaldırılan her paket `doc/architecture/mobile_flutter.md`'de not edilir.
- [ ] **Step 7: Eksik testleri yaz (D-21)** — parent feature'ı için en az 3 cubit testi; study için eksik senaryolar; hedef: `flutter test --coverage` ile `lib/features/**` satır kapsamı **%50+**.
- [ ] **Step 8: CI'yı sıkılaştır (D-22)** — `build-android.yml` içine `flutter analyze --fatal-infos` + `flutter test --coverage` adımları; kapsam eşiği altındaysa uyarı (ilk aşamada hata değil).
- [ ] **Step 9: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 10: Doküman + commit**

`doc/architecture/mobile_flutter.md` (l10n bölümü + paket listesi), `doc/architecture/ux_rules.md` (metin dili kuralı: tam Türkçe karakter, "EğitimÜssü" yazımı).
```bash
git add mobile .github doc
git commit -m "chore(mobile): l10n altyapisi + metin kalitesi + test kapsami + CI (D-18..D-22/D-12)"
```

---

### Task 7: Cihaz oturumları ve telefon doğrulama (M01-4/M01-5/D-03)

**Files:**
- Modify: `src/Modules/Identity/Application/IdentityFeatures.cs`, `IdentityPolicies.cs`
- Modify: `src/Modules/Identity/API/IdentityModule.cs`
- Modify: `src/Modules/Identity/Infrastructure/{IdentityRepositoryAndSecurity,DependencyInjection}.cs`
- Create: `mobile/lib/features/more/presentation/pages/device_sessions_page.dart`
- Modify: `mobile/lib/core/routing/app_router.dart`
- Test: `tests/Unit/DeviceSessionTests.cs`, `mobile/test/features/more/device_sessions_test.dart`

**Interfaces:**
- Produces:
  - `GET /api/identity/sessions` (auth) → `[{ sessionId, deviceName, createdOnUtc, lastUsedOnUtc, isCurrent }]` (M01-4)
  - `POST /api/identity/sessions/{sessionId}/revoke` (auth, yalnız kendi oturumu)
  - `POST /api/identity/sessions/revoke-all` (auth) — mevcut oturum hariç hepsini iptal eder
  - Mobil rota `/account/sessions` (D-03)

> **M01-5 (telefon/SMS OTP) bu planda kapsam dışıdır.** Gerekçe: SMS sağlayıcı sözleşmesi + maliyet kararı gerektirir ve hiçbir akış buna bağlı değil (kimlik doğrulama e-posta üzerinden çalışıyor). Karar verildiğinde ayrı bir plan açılır; `doc/modules/m01_identity.md`'de madde "ertelendi (karar bekliyor)" olarak işaretlenir.

- [ ] **Step 1: Testleri yaz (kırmızı)** — kullanıcı yalnız kendi oturumlarını görüyor; başka kullanıcının oturumunu iptal edemiyor (403); `revoke-all` mevcut oturumu **korumalı**; iptal edilen refresh token ile yenileme 401.
- [ ] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~DeviceSessionTests"`
- [ ] **Step 3: Query/command + authorizer + uçları yaz** — mevcut oturumu ayırt etmek için token'daki `jti`/oturum kimliği kullanılır.
- [ ] **Step 4: Yeşil gör** — Run: `dotnet test EgitimUssu.slnx`
- [ ] **Step 5: Mobil ekran** — cihaz adı, son kullanım, "Bu cihaz" rozeti, tekil iptal + "Diğer tüm oturumları kapat"; onay diyaloğu.
- [ ] **Step 6: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 7: Doküman + commit**

`doc/modules/m01_identity.md` (§3.3 + mobil listesi), `doc/pages/device_sessions.md` + `doc/pages/00_pages_index.md`.
```bash
git add src/Modules/Identity mobile tests doc
git commit -m "feat(identity): cihaz oturumlari listesi + iptal (M01-4/D-03)"
```

---

### Task 8: Kapanış

- [ ] **Step 1: Tam testler** — Run: `./scripts/test-with-docker.sh && cd mobile && flutter test --coverage` → yeşil.
- [ ] **Step 2: Gözlemlenebilirlik doğrulaması** — staging'de bir uçtan uca akış koş, Jaeger/OTLP toplayıcısında tam zinciri gör (HTTP → command → EF → outbox → handler).
- [ ] **Step 3: Yedekten geri yükleme tatbikatı** — staging'i yedekten geri yükle, uygulama açılıyor mu doğrula.
- [ ] **Step 4: Dokümanlar** — `doc/INDEX.md`'ye yeni mimari dokümanları ekle (`api_versioning.md`, `environments.md`, `backup_restore.md`, `kvkk.md`); `doc/denetim/2026-09-02_eksik_analizi.md` C-04, C-10, E-05, E-06, E-07, D-18..D-22 → `✅ (P13)`.
- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: P13 operasyon ve hijyen kapanisi"
```

---

## Kabul Kriterleri

- [ ] Trace zinciri uçtan uca görünüyor; her log satırında `TraceId` var
- [ ] PII hiçbir log/trace alanında yok (örnek istek incelenerek doğrulandı)
- [ ] Outbox kuyruk derinliği ve push/e-posta başarı oranı metrik olarak yayınlanıyor
- [ ] Her `/api/*` yanıtında `X-Api-Version` başlığı var
- [ ] Staging ortamı ayrı DB ile çalışıyor, demo veri idempotent
- [ ] Yedek alma + geri yükleme tatbikatı başarılı
- [ ] Hesap silme talebi 30 gün sonra kişisel veriyi anonimleştiriyor, ticari kaydı koruyor
- [ ] Mobilde kullanıcıya görünen tüm metinler ARB'den geliyor, Türkçe karakterler doğru
- [ ] Mobil satır kapsamı %50+; CI analiz + test koşuyor
