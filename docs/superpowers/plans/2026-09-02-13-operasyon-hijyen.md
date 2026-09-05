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
- Create: `src/Shared/Infrastructure/Observability/SensitiveDataMask.cs`, `MeteredSenders.cs` *(plan dışı — sapma notu)*
- Create: `src/Shared/Infrastructure/Configuration/ObservabilityOptions.cs`
- Modify: `src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj` (OpenTelemetry paketleri)
- Modify: `src/API.Host/Program.cs`, `appsettings.json`
- Modify: `src/Shared/Infrastructure/Middleware/RequestContextLoggingMiddleware.cs` (TraceId + PII maskesi)
- Modify: `src/Shared/Infrastructure/**Persistence**/EfOutboxStore.cs` (kuyruk metrikleri) *(plan yolu `Messaging/` diyordu — dosya `Persistence/` altında)*
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs` (ölçüm sarmalayıcıları)
- Test: `tests/Unit/EgitimUssuMetricsTests.cs`, `tests/Unit/RequestContextLoggingTests.cs`

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **EF Core enstrümantasyonu alınmadı.** `OpenTelemetry.Instrumentation.EntityFrameworkCore`'un kararlı sürümü yok
>    (yalnız `1.18.0-beta.1`); üretim koduna beta bağımlılık girmesin diye Npgsql'in **kararlı** `Npgsql.OpenTelemetry`
>    köprüsü kullanıldı — SQL komutları zaten span olarak yayılıyor. Paket **`9.0.4`'e sabitlendi**: serbest bırakılınca
>    Npgsql'i 10'a çekiyor ve EF sağlayıcısı (`Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4`) desteklenmeyen bir sürücüyle kalıyordu.
> 2. **`outbox.pending` claim edilen parti değil, gerçek kuyruk derinliğidir.** İlk yazımda `messages.Count`
>    (claim edilen parti) ölçülüyordu; parti `BatchSize` ile sınırlı olduğu için 10.000 mesaj biriktiğinde bile 20'de
>    tavan yapar ve **alarm hiç ötmezdi**. Tur başına ayrı bir `COUNT` eklendi.
> 3. **Push/e-posta ölçümü sarmalayıcıya konuldu** (`MeteredPushSender`/`MeteredEmailSender`), sağlayıcıların içine
>    değil: her sağlayıcıya ayrı yazılsaydı yarın eklenecek üçüncü sağlayıcı ölçümü sessizce atlayabilirdi.
> 4. **PII maskesi ayrı bir tipe alındı** (`SensitiveDataMask`) — middleware'in içinde gömülü kalsaydı birim testi
>    yazmak için sahte `HttpContext` kurmak gerekirdi; kural saf bir fonksiyon olarak doğrudan test edilebiliyor.
> 5. **`EgitimUssuMetrics` `Enabled=false` iken de kaydedilir.** İş kodu (outbox/push/e-posta) ona bağımlıdır ve her
>    ortamda çağırabilmelidir; dinleyici yokken ölçüm zaten ücretsizdir. Kapalıyken kurulmayan şey exporter ve
>    enstrümantasyondur.

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
  - Konfigürasyon: `Observability:Enabled`, `Observability:OtlpEndpoint`, `Observability:ServiceName`, `Observability:ServiceVersion`.
  - `static string SensitiveDataMask.MaskQueryString(string? pathAndQuery)` + `bool IsSensitiveKey(string key)`.

- [x] **Step 1: Metrik testini yaz (kırmızı)** — `MeterListener` ile `RecordPushSent("Delivered")` sayacının arttığını doğrula; yüksek kardinaliteli etiket (`userId`) kabul edilmediğini doğrula (API'de böyle bir parametre yok).
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~EgitimUssuMetricsTests"`
- [x] **Step 3: Paketleri ekle**
```bash
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Extensions.Hosting
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.Http
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package OpenTelemetry.Exporter.Console
```
- [x] **Step 4: `AddObservability`'yi yaz** — `Observability:Enabled=false` iken hiçbir şey kaydetmez (test/CI maliyeti sıfır). OTLP endpoint boşsa konsol exporter.
- [x] **Step 5: Metrikleri yerleştir** — outbox bekleyen sayısı (her dispatch turunda), push sonucu (P03), e-posta sonucu (P02).
- [x] **Step 6: Log korelasyonu + PII maskesi** — her log satırına `TraceId`; `email`, `phone`, `token`, `password` anahtarlarını içeren alanlar `***` ile maskelenir.
- [x] **Step 7: Yerelde doğrula** — Jaeger + gerçek koşum yapıldı (2026-09-05):
  `GET /api/meta/version` ve `/api/messaging/status` Jaeger'da göründü (servis `egitimussu-api`, 5 trace),
  `/health/live` çağrıları **filtrelendiği için trace'e girmedi**; her log satırında `TraceId` dolu;
  `?token=SECRET123` log'a `token=***` olarak düştü ve `SECRET123` metni log'da **0 kez** geçti;
  OTLP adresi boşken konsol exporter'ından `egitimussu.outbox.pending` (LongGauge, meter `EgitimUssu`)
  ve `http.server.request.duration` okundu.
- [x] **Step 8: Doküman + commit**

`doc/architecture/backend.md` "Gözlemlenebilirlik" başlığı (ne toplanıyor, nasıl açılıyor, hangi metrikler alarm adayı).
```bash
git add src/Shared/Infrastructure/Observability src/API.Host tests doc
git commit -m "feat(observability): OpenTelemetry trace/metrik/log korelasyonu (C-04)"
```

---

### Task 2: API sürümleme ve sözleşme kararlılığı (C-10)

**Files:**
- Modify: `src/Shared/Infrastructure/Modules/ModuleDefinition.cs` (grup üzerinde sürüm etiketi + sürümlü grup overload'ı)
- Create: `src/Shared/Infrastructure/Modules/ApiVersioning.cs` *(plan dışı — sapma notu #3)*
- Create: `src/Shared/Infrastructure/Middleware/ApiVersionHeaderMiddleware.cs` *(plan dışı — sapma notu #2)*
- Modify: `src/API.Host/Program.cs` (OpenAPI belgesi + sürüm başlığı + `apiVersion` alanı)
- Create: `doc/architecture/api_versioning.md`
- Modify: `doc/architecture/backend.md` (§3 "API sürümleme" + "Meta" + "OpenAPI" satırları)
- Test: `tests/Integration/ApiVersionHeaderTests.cs`, `tests/Unit/ApiVersionHeaderMiddlewareTests.cs` *(ikincisi plan dışı — sapma notu #4)*

**Interfaces:**
- Yanıtlara `X-Api-Version` başlığı eklenir (`/api/meta/version` ile aynı değer).
- Kural: `v1` yolları **kırılmaz**; kırıcı değişiklik `"/api/v2/..."` altında yeni grupla açılır. Mevcut yollar `v1` kabul edilir (yol değişmez, belge bunu açıkça yazar).

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **Başlık sözleşme sürümünü taşır, derleme sürümünü değil.** Plan "`/api/meta/version` ile aynı değer" diyordu,
>    ama o uç yalnız **assembly** sürümünü (`1.0.0.0`) döndürüyordu; başlık ona bağlansaydı her yayında değişir ve
>    istemci "sözleşme kırıldı mı?" sorusunu **cevaplayamazdı** — üstelik planın kendi `v1`/`v2` yol kuralıyla da
>    ilgisiz kalırdı. Uca `apiVersion` (`"v1"`) alanı eklendi, başlık onunla birebir aynı; `version` (derleme)
>    alanı olduğu gibi korundu. Test iki kaynağın **eşitliğini** doğruluyor.
> 2. **Başlık middleware'i ayrı dosyaya alındı** (`Middleware/ApiVersionHeaderMiddleware.cs`) — plan Files listesinde
>    yoktu ve Step 3 "middleware yaz" diyordu; repoda tüm middleware'ler bu klasörde ayrı sınıflar.
> 3. **Sürüm sözlüğü ayrı dosyada** (`Modules/ApiVersioning.cs`: `ApiVersions` + `ApiVersionMetadata` +
>    `WithApiVersion`) — `ModuleDefinition.cs`'e gömülseydi hem modül taban sınıfını hem sürüm sözleşmesini
>    barındıran iki işli bir dosya olurdu ve middleware onu `Modules` üzerinden çağırmak zorunda kalırdı.
> 4. **Birim testi eklendi.** Plan yalnız integration testi öngörüyordu; ama asıl kural **çözümleme sırası**
>    (metadata > yol varsayılanı) ve "başlık `next`'ten **önce** yazılır" — ikisi de saf birim testiyle doğrudan
>    kanıtlanır. v2 etiketi de ancak böyle test edilebiliyor: canlı uygulamada henüz v2 grubu yok.
> 5. **`/health/*` ve `/openapi/*` bilerek sürümlenmiyor.** Plan "her `/api/*` yanıtı" diyordu; bu uçlar zaten
>    `/api` altında değil ve yayınlanan sözleşmenin parçası değil — sürüm başlığı taşımaları yanıltıcı olurdu.
>    Test bunu açıkça doğruluyor.
> 6. **Etiketsiz `/api` ucu için yol tabanlı yedek kural var.** `CreateModuleGroup` kullanmayan uçlar
>    (`/api/meta/version`, `MapTeacherDashboard`) metadata taşımıyor; yedek kural olmasa sürümsüz yanıt dönerlerdi.
>    Bu uçları tek tek etiketlemek yerine yedek kural seçildi: yarın eklenecek bir uç da unutulmuş olmaz.
> 7. **`doc/INDEX.md` güncellemesi Task 8'e bırakıldı** — planın kendi kapanış görevi (Task 8 Step 4) dört yeni
>    mimari dokümanını topluca ekliyor. `doc/architecture/backend.md` bu görevde güncellendi.

- [x] **Step 1: Testi yaz (kırmızı)** — herhangi bir `/api/*` yanıtında `X-Api-Version` başlığı var ve `/api/meta/version` ile aynı.
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Integration/EgitimUssu.Tests.Integration.csproj --filter "FullyQualifiedName~ApiVersionHeaderTests"` → **5 başarısız / 1 geçti** (geçen tek test "health sürümlenmez"; başlık hiç yokken doğal olarak yeşildi).
- [x] **Step 3: Başlığı ekleyen middleware'i yaz.**
- [x] **Step 4: Sürümleme politikasını yaz** — `doc/architecture/api_versioning.md`: kırıcı değişiklik tanımı, uyarı süresi (mobil sürümler için en az 2 sürüm **ve** en az 90 gün — ikisinin büyüğü), kaldırma prosedürü.
- [x] **Step 5: Yeşil gör + commit** — 4 mimari / 691 birim / 50 integration, başarısız 0, atlanan 0; `-warnaserror` 0/0.

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
- Modify: `src/API.Host/Program.cs` (koşulsuz çağrı — karar guard'ın; sapma notu #2)
- Modify: `src/API.Host/appsettings.json` (`Seeding:Enabled` varsayılan `false`) *(plan dışı — sapma notu #6)*
- Modify: `render.yaml` (staging servisi + staging veritabanı)
- Create: `doc/architecture/environments.md`
- Modify: `doc/architecture/backend.md` (§3 "Ortamlar / tohumlama" satırı)
- Test: `tests/Integration/DemoDataSeederTests.cs` *(plan `tests/Unit` diyordu — sapma notu #1)*

**Interfaces:**
- `DemoDataSeeder.SeedAsync()` — idempotent: 2 öğretmen, 5 öğrenci, 2 veli, 3 hafta ders geçmişi, ödevler, ödemeler, çalışma seansları. Var olan veriyi **asla** değiştirmez (yalnız yoksa oluşturur).
- `Seeding:Enabled` prod'da **daima false**; guard bunu zorlar (`ASPNETCORE_ENVIRONMENT=Production` ise `true` olsa bile çalışmaz + `LogWarning`).

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **Test `tests/Integration`'a yazıldı, `tests/Unit`'e değil.** Seeder 15 modülün DI'ını, dispatcher'ını ve
>    DbContext'lerini kullanır; `tests/Unit` `API.Host`'a referanslı bile değil (Öğrenilenler #72) ve olsaydı da bu
>    bir birim testi olmazdı. **Guard** yine de saf bir fonksiyon olarak ayrıldı (`DemoDataSeedGuard.BlockReason`),
>    böylece "üretimde asla" kuralı ortam kurmadan, tablo halinde test edilebiliyor.
> 2. **`Program.cs` bayrağı sınamıyor; kararın tamamı guard'ın.** Plan "yalnız `Seeding:Enabled=true` iken" diyordu;
>    bayrak iki yerde sınansaydı kural ikiye bölünür ve biri unutulduğunda üretimde demo veri açılabilirdi.
>    Çağrı koşulsuz, karar tek yerde.
> 3. **5 öğrencinin 2'si kendi hesabıyla açılıyor.** Plan öğrenci türünü söylemiyordu. Gerekçe iki yönlü:
>    (a) veli-çocuk bağını **yalnız öğrenci ya da Admin** onaylayabiliyor (veli kendi bağını onaylayamaz) ve demo
>    sette Admin yok — hesapsız çocuğa bağlanan veli onaysız kalırdı; (b) çalışma seansı öğrencinin kendi kaydıdır,
>    hesapsız öğrenci için tohumlanamaz. Kalan 3 öğrenci öğretmen-yönetimli: iki akış da temsil ediliyor.
> 4. **Seeder outbox kuyruğunu elle boşaltıyor.** Parents, bir öğrenciyi ancak olay işlendikten sonra tanıyor ve
>    tanımadan bağ onayını fail-closed reddediyor (`shared.forbidden` — gerçekten görüldü). Tohumlama tek turda
>    aktığı için arka plan dispatcher'ının periyodu beklenemez; kuyruk veli adımından önce ve tur sonunda boşaltılır.
> 5. **Her öğrenciye ayrı ders saati verildi.** İlk yazımda saat öğretmen indeksine göre kayıyordu; aynı öğretmenin
>    iki öğrencisi aynı saate düşünce Scheduling `scheduling.teacher_conflict` ile **haklı olarak** reddetti.
> 6. **`appsettings.json`'a `Seeding:Enabled=false` eklendi** — ayar yalnız env değişkeninde yaşasaydı keşfedilemezdi.
> 7. **Kimliğe bürünme mekanizması:** yetkilendiriciler `ICurrentUser` → `IHttpContextAccessor` üzerinden çalışıyor;
>    tohumlama HTTP isteği içinde koşmadığı için `SeedSession.ActAs` bağlamı elle kurar. `ClaimsIdentity`'ye
>    `authenticationType` verilmezse `IsAuthenticated` false kalır ve **her** komut reddedilir.

- [x] **Step 1: Testi yaz (kırmızı)** — seeder iki kez çalıştırıldığında kayıt sayısı değişmiyor; Production ortamında hiç çalışmıyor. Ek olarak tam veri seti gerçek tablolardan doğrulanıyor (özet nesnesinden değil).
- [x] **Step 2: Kırmızı gör** — Run: `dotnet test tests/Integration/... --filter "FullyQualifiedName~DemoDataSeederTests"` → **CS0234** (`Seeding` ad alanı yok).
- [x] **Step 3: Seeder'ı yaz** (modül command'lerini dispatch ederek — doğrudan DbContext yazımı yok, iş kuralları atlanmasın).
- [x] **Step 4: Staging servisini tanımla** — `render.yaml`'a ayrı servis + ayrı DB + `Email__Provider=Logging`, `Push__Provider=Logging`, `Seeding__Enabled=true`.
- [x] **Step 5: Ortam dokümanı** — `doc/architecture/environments.md`: development / staging / production farkları, hangi sağlayıcılar açık, kim erişebilir.
- [x] **Step 6: Commit**

```bash
git add src/API.Host scripts render.yaml tests doc
git commit -m "feat(ops): staging ortami + idempotent demo veri tohumlama (E-06)"
```

---

### Task 4: Yedekleme, geri yükleme ve migration geri alma (E-05)

**Files:**
- Create: `scripts/backup-db.sh`, `scripts/restore-db.sh`
- Create: `scripts/lib/pg-connection.sh` *(plan dışı — sapma notu #1)*
- Create: `scripts/check-migration-down.sh` *(plan dışı — sapma notu #4)*
- Create: `doc/architecture/backup_restore.md`
- Create: `src/Shared/Infrastructure/Configuration/MigrationStartupGuard.cs` *(plan dışı — sapma notu #2)*
- Modify: `src/Shared/Infrastructure/Extensions/ModuleMigrationExtensions.cs` *(plan dışı — sapma notu #2)*
- Modify: `src/API.Host/Program.cs` (`ApplyMigrationsOnStartup` davranışı)
- Modify: `render.yaml` (üretimde `Database__ApplyMigrationsOnStartup=false`) *(plan dışı — sapma notu #3)*
- Modify: `.github/workflows/backend-ci.yml` (migration geri alma kontrolü)
- Modify: `doc/architecture/environments.md`, `doc/architecture/backend.md` *(plan dışı — sapma notu #7)*
- Test: `tests/Unit/MigrationStartupGuardTests.cs` *(plan dışı — sapma notu #2)*

**Interfaces:**
- `scripts/backup-db.sh` — `pg_dump` ile tarihli yedek, opsiyonel S3'e yükleme.
- `scripts/restore-db.sh` — belirtilen yedeği hedef veritabanına geri yükler; **onay ister** (`--yes` olmadan çalışmaz).
- Prod'da `Database:ApplyMigrationsOnStartup = false`; migration ayrı bir deploy adımıdır (`dotnet ef database update`).
- `static string? MigrationStartupGuard.BlockReason(string environmentName, bool enabled)`
- `Task<bool> ApplyModuleMigrationsAsync(this IServiceProvider, CancellationToken)` — uygulandıysa `true`.

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **Bağlantı/çalıştırma yardımcıları ortak dosyada** (`scripts/lib/pg-connection.sh`). İki neden: (a) repo'nun
>    bağlantı dizesi **Npgsql anahtar-değer** biçimindedir (`seed-demo-data.sh` de öyle kullanıyor), `pg_dump` ise
>    libpq biçimini bekler — çeviriyi iki script'e kopyalamak bir ayrıştırıcıyı çoğaltmak olurdu; (b) parola
>    conninfo'dan çıkarılıp `PGPASSWORD`'e alınıyor, aksi halde `ps` çıktısında görünürdü.
> 2. **Karar saf bir guard'a alındı** (`MigrationStartupGuard`) ve `ApplyModuleMigrationsAsync` **kararın sahibi**
>    oldu (`bool` döndürür). Plan yalnız `Program.cs`'i değiştirmeyi öngörüyordu; bayrak hem host'ta hem uzantıda
>    sınansaydı kural iki yere dağılırdı — Task 3'teki `DemoDataSeedGuard` gerekçesinin aynısı. Guard saf olduğu için
>    host ayağa kaldırmadan test edilebiliyor. **Plan bu göreve test dosyası koymamıştı**; Step 2 davranış
>    değiştirdiği için TDD zorunluydu (master tasarım §1 "testsiz kod yok").
> 3. **`render.yaml`'da üretim bayrağı `false`'a çekildi.** Blueprint `true` diyordu; guard eklenince bu, her üretim
>    açılışında ötecek bir uyarıya ve blueprint'ten okunduğunda **yanlış** bir niyete dönüşürdü ("üretim kendi
>    migrate ediyor" izlenimi). Staging `true` kaldı — Staging, Production değildir ve provanın yeri orasıdır.
> 4. **Kontrol CI'ya gömülmedi, `scripts/check-migration-down.sh` oldu.** Yalnız workflow içinde yaşasaydı yerelde
>    çalıştırılamaz, PR açılmadan denenemezdi. (Plan zaten "script" diyordu.)
> 5. **Kural "Down() boş değil" değil, "`Up()` doluysa `Down()` dolu".** Repoda `Up()`'ı da boş olan gerçek bir
>    no-op migration var (`StudentSubjectValueGeneratedNever` — yalnız model metadata'sı değişiyor); planın düz
>    kuralı bu meşru dosyada CI'ı kırardı. Ek olarak **yalnız `throw` içeren** `Down()` de yakalanıyor: gövde boş
>    değildir ama migration yine geri alınamaz — aynı kusurun kılık değiştirmiş hali.
> 6. **Arşiv konteynere bind mount ile değil, stdin/stdout ile akıyor.** İlk yazımda `-v` kullanıldı; Docker Desktop
>    paylaşılmayan host dizinine **sessizce boş** bir bağlama yaptı, `pg_dump` konteyner içinde başarıyla yazdı,
>    host'ta dosya oluşmadı ve script "yedek alındı" dedi — **gerçekten görüldü**. Akış biçimi hem bu tuzağı hem de
>    dosyanın `root`'a ait olması sorununu ortadan kaldırır. Yedek yazıldıktan sonra host'ta varlığı + okunabilirliği
>    (`pg_restore --list`) ayrıca doğrulanıyor.
> 7. **`environments.md` ve `backend.md` bu görevde güncellendi** — ikisi de "E-05'te ayrı adıma taşınacak" diye
>    **ileriye referans** veriyordu ve `backend.md`'deki `Program.cs` kod örneği artık gerçeğe uymuyordu.
>    `doc/INDEX.md` girdisi Task 8'e bırakıldı (planın kendi kapanış görevi dört yeni mimari dokümanını topluca ekliyor).

- [x] **Step 1: Script'leri yaz ve yerelde dene** — Docker Postgres 17'ye `seed-demo-data.sh` ile demo veri yüklendi (15 modül şeması + 122 tablo) → yedek alındı → **tüm şemalar düşürüldü** (0 tablo) → geri yüklendi → **122 tablonun satır sayısı birebir aynı** (`diff` farksız). Geri yüklenen veritabanına karşı uygulama açıldı: `/health/ready` = `Healthy`, demo öğretmen girişi **HTTP 200**. `--yes` olmadan çalıştırma reddedildi (çıkış 1).
- [x] **Step 2: Prod'da otomatik migration'ı kapat** — `MigrationStartupGuard` + `ApplyModuleMigrationsAsync` (bkz. sapma #2). Canlı doğrulandı: `Production` + bayrak açık → uyarı düştü, **0 migration** koştu, boş veritabanında **0 tablo**; aynı veritabanı `Staging` ile açılınca **15 modül** migrate oldu, **122 tablo**.
- [x] **Step 3: CI'ya migration geri alma kontrolü ekle** — `scripts/check-migration-down.sh` + Backend CI adımı. Repoda 80 migration incelendi, hepsi geri alınabilir (1 no-op muaf). Dört sahte migration'la doğrulandı: boş `Down()` → yakalandı, yalnız `throw` içeren `Down()` → yakalandı, sağlam migration → geçti, no-op → geçti.
- [x] **Step 4: Prosedürü yaz** — `doc/architecture/backup_restore.md`: RPO ≤ 24 sa / RTO ≤ 1 sa, günlük yedek, 30 gün saklama, geri yükleme adımları, üretimde migration prosedürü + geri alma tablosu, `Down()` kuralı, yetki tablosu, **tatbikat kaydı**.
- [x] **Step 5: Commit**

```bash
git add scripts .github doc src/API.Host
git commit -m "feat(ops): yedekleme/geri yukleme prosedürü + migration geri alma kontrolu (E-05)"
```

---

### Task 5: KVKK — hesap silme ve saklama (E-07)

> **Bloklayıcı — ✅ çözüldü (2026-09-05):** Q5 için taslak metin seti yazıldı → **`doc/legal/`**
> ([indeks](../../../doc/legal/00_legal_index.md) · [veri envanteri](../../../doc/legal/kvkk_veri_envanteri.md) ·
> [aydınlatma](../../../doc/legal/kvkk_aydinlatma_metni.md) · [açık rıza](../../../doc/legal/kvkk_acik_riza_metni.md) ·
> [saklama/imha](../../../doc/legal/kvkk_saklama_imha_politikasi.md)). Bu görev artık yürütülebilir.
>
> **Metinlerden gelen ve bu göreve giren üç düzeltme:**
> 1. **Anonimleştirme hash ile yapılmaz.** Plan `deleted-{hash}@egitimussu.local` diyor; e-posta adresleri sınırlı
>    ve tahmin edilebilir bir küme olduğu için aynı hash bilinen bir listeye uygulanıp **eşleştirilebilir** — bu
>    anonimleştirme değil takma adlaştırmadır ve takma adlı veri hâlâ kişisel veridir. **Rastgele** bir vekil
>    değer kullanılacak (ya da tuz gizli tutulup imha edilecek). Bkz. saklama politikası §4.
> 2. **Saklama süreleri politikadan alınacak** (saklama politikası §2), plandaki üç sayı bunun alt kümesidir:
>    oturum/jeton 90/30 gün, mesaj 3 yıl, rapor PDF 1 yıl, ders/ödev 10 yıl (anonim), ödeme 10 yıl (TTK m.82),
>    log 1 yıl, trace 30 gün, inbox 90 gün — outbox 90 gün / feedback 2 yıl / audit 5 yıl planla aynı.
> 3. **Rıza kaydı tablosu gerekiyor.** `user_settings` boolean'ı rızanın *şu anki durumunu* tutuyor; hangi metin
>    sürümüne ne zaman onay verildiğini tutmuyor ve ispat yükü veri sorumlusundadır. Bkz. açık rıza metni §7.
>
> ⚠️ **Hukuk onayı ayrı bir iştir ve bu görevi bloke etmez:** metinler taslaktır, `«…»` alanları boştur.
> Yayına alma koşulları ve **R-1…R-6** riskleri `doc/legal/00_legal_index.md` §3–§4'tedir.

**Files:**
- Create: `src/Modules/Identity/Application/AccountDeletionFeatures.cs` *(plan `IdentityFeatures.cs` diyordu — sapma notu #6)*
- Modify: `src/Modules/Identity/Application/IdentityPolicies.cs` (3 authorizer), `API/IdentityModule.cs` (3 uç), `Infrastructure/{IdentityDbContext,IdentityRepositoryAndSecurity,DependencyInjection}.cs`
- Modify: `src/Modules/Identity/Domain/IdentityDomainModel.cs` (`AccountDeletionRequest`, `AccountDeletionPolicy`, `UserAccount.Anonymize`)
- Create: `src/Modules/Identity/Infrastructure/AccountDeletionService.cs` (processor + BackgroundService)
- Create: `src/Modules/Identity/Infrastructure/Migrations/*_AddAccountDeletionRequests.cs`
- Create: `src/Shared/Contracts/PersonalDataContract.cs` + `src/Shared/Kernel/PersonalDataPlaceholder.cs` *(sapma notu #4)*
- Modify: `src/Shared/Contracts/StudentDirectoryContract.cs` (`GetStudentIdsForUserAsync`) *(sapma notu #2)*
- Create: **11 modülde** `*PersonalDataAnonymizer.cs` + domain `AnonymizePersonalData` metotları — Teachers, Students, Parents, Study, Messaging, **Notifications, Settings, Assignments, LessonSessions, Payments, Reporting** *(sapma notu #1)*
- Create: `src/Shared/Infrastructure/Persistence/DataRetentionService.cs` *(sapma notu #8)*
- Create: `doc/architecture/kvkk.md` · Modify: `doc/modules/{m01_identity,00_genel_bakis,veri_modeli}.md`, `doc/legal/kvkk_saklama_imha_politikasi.md` §7
- Test: `tests/Unit/AccountDeletionTests.cs`, `tests/Unit/DataRetentionTests.cs` *(ikincisi plan dışı)*, `tests/Integration/RealAccountDeletionTests.cs` *(sapma notu #7)*, 5 mevcut test sahtesi *(sapma notu #9)*
- **Yazılmadı:** `src/API.Host/Admin/AdminEndpoints.cs` *(sapma notu #5)*

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **6 modül değil, 11 modül.** Plan Students/Teachers/Parents/Study/Messaging/**Feedback** diyordu. Feedback
>    (M18) **modülü yok**. Buna karşılık, [veri envanteri](../../../doc/legal/kvkk_veri_envanteri.md) §2'ye göre
>    kişisel veri **altı modülde daha** duruyordu: Notifications (cihaz push jetonu — silinmezse silinmiş hesabın
>    telefonuna push gitmeye devam eder), Settings, Assignments (ödev/ders notu serbest metinleri + ek dosyalar),
>    LessonSessions (öğretmen notları), Payments (açıklama/not) ve Reporting (PDF raporlar). Yalnız planın altı
>    modülü yazılsaydı silme talebi **sessizce eksik** kalırdı — ki bu, teknik bir eksik değil, yerine
>    getirilmemiş bir hukuki talep olurdu.
> 2. **Sözleşme `Guid userId` değil `PersonalDataSubject(UserId, StudentIds)` alıyor.** Study, Assignments,
>    LessonSessions ve Reporting kayıtlarını **`StudentId`** ile tutar ve modül sınırı yüzünden bunu `UserId`'den
>    kendisi çözemez (bilinen asimetri, `veri_modeli.md` §2). Çözüm `IStudentDirectory`'ye ters yön eşlemesi
>    (`GetStudentIdsForUserAsync`) eklemek oldu — o arayüz zaten `GetOwnerUserIdAsync` ile düz yönü tutuyor.
> 3. **Anonimleştirme hash ile yapılmıyor** (yukarıdaki bloklayıcı notu): vekil e-posta `IIdGenerator`'dan gelen
>    **rastgele** bir kimlikten üretiliyor, silinen adresten türetilmiyor.
> 4. **`PersonalDataPlaceholder` `Shared/Kernel`'e kondu**, `Shared/Contracts`'a değil. Yer tutucuları domain
>    sınıfları kullanıyor (`AnonymizePersonalData`), domain projeleri ise **yalnız Kernel'e** referans veriyor;
>    Contracts'ta kalsaydı 11 domain projesine yeni bir referans eklemek gerekirdi. Arayüz (`IPersonalDataAnonymizer`)
>    modüller-arası sözleşme olduğu için Contracts'ta kaldı.
> 5. **Admin ucu yazılmadı.** Plan `src/API.Host/Admin/AdminEndpoints.cs`'i "modify" diyor ama **o dosya yok**:
>    `/api/admin/*` uç grubu K-08 kararıyla **P12**'nin işi. Silme taleplerinin admin görünümü oraya bırakıldı;
>    `doc/architecture/kvkk.md` §5 bunu açıkça yazıyor.
> 6. **Yeni dosya `AccountDeletionFeatures.cs`.** Plan `IdentityFeatures.cs`'i genişletmeyi öngörüyordu; o dosya
>    zaten 310 satır ve 11 command/query barındırıyor. Ayrıca **`GET /account/deletion-request` eklendi** (plan
>    yalnız POST/DELETE diyordu): bu uç olmadan mobil uygulama "silme talebin var, X tarihinde uygulanacak"
>    bilgisini gösteremez ve kullanıcı iptal edeceğini bilemez.
> 7. **Integration testi eklendi** (`RealAccountDeletionTests`). Plan yalnız `tests/Unit` öngörüyordu, ama birim
>    testi burada **yanlış güvence** verir: sahte anonimleştiricilerle koşar ve bir modülün DI kaydı unutulmuşsa
>    ya da yanlış kimliğe bakıyorsa yeşil kalır. Gerçek Postgres'te on şemanın gerçekten temizlendiği okunuyor.
>    (`tests/Unit` 10 modülün Infrastructure projesine referans vermiyor ve verseydi de bu bir birim testi olmazdı.)
> 8. **`FeedbackRetentionService`/`AdminAuditLog` yazılamadı; yerine `DataRetentionService`.** Her ikisinin
>    modülü de yok (M18 ve admin API'si → P12). Yazılabilen kural — işlenmiş outbox + inbox **90 gün** — tüm modül
>    şemalarında koşan ortak bir servise alındı. Yazılamayanlar `kvkk_saklama_imha_politikasi.md` §7'de
>    "🔴 modülü yok" olarak **tablolandı**, sessizce atlanmadı.
> 9. **5 mevcut test sahtesi güncellendi.** `IStudentDirectory`'yi genişletmek onu uygulayan sahteleri kırdı
>    (`MessagingSupport`, `ProgressAccessTests`, `SelfLessonPolicyTests`, `StudentReportPdfGeneratorTests`,
>    `LessonSessionCompletedPaymentHandlerTests`). Ayrı bir dar arayüz açmak bu kırılmayı önlerdi ama aynı
>    eşlemeyi iki sözleşmeye bölerdi — arayüz zaten bu eşlemenin sahibi.
> 10. **`Message.SoftDelete` yetmiyor.** Mevcut yumuşak silme `Body`'yi veritabanında **bırakıyor** (yalnız
>    `VisibleBody`'yi gizliyor). Silme talebi için ayrı bir `AnonymizePersonalData` yazıldı: gövde gerçekten boşalır.

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

- [x] **Step 1: Testleri yaz (kırmızı)** — 13 test: 30 gün kuralı (0/29/30/45 gün tablosu), iptal, giriş yapılamaması, ödeme tutarının korunması, idempotency, **bir modül patlarsa talebin Pending kalması** ve vekil e-postanın türetilmemesi.
- [x] **Step 2: Kırmızı gör** — **CS0246** (`AccountDeletionProcessor` yok). Yeşile geçerken bir beklenti hatası da çıktı: `AnonymizedRecordCount` 5 değil **6** — Identity'nin kendi kaydı da sayılıyor; test gerçeğe göre düzeltildi.
- [x] **Step 3: Sözleşme + 11 modül + servis + uçlar** (bkz. sapma #1, #2, #5, #6). Migration: `AddAccountDeletionRequests`; `scripts/check-migration-down.sh` (Task 4) yeni migration'ı doğruladı.
- [x] **Step 4: Saklama süreleri** — `DataRetentionService` (günde bir): işlenmiş outbox + inbox **90 gün**, tüm modül şemalarında. **İşlenmemiş outbox'a dokunulmaz** (test bunu ayrıca doğruluyor). Feedback/AdminAuditLog modülleri yok → sapma #8.
- [x] **Step 5: Yeşil gör** — `dotnet test EgitimUssu.slnx`: 4 mimari / **716 birim** / **62 integration**, başarısız 0, atlanan 0.
- [x] **Step 6: Doküman + commit**

`doc/architecture/kvkk.md`: hangi veri nerede, saklama süreleri, silme akışı, veri sahibi hakları.
```bash
git add src doc tests
git commit -m "feat(kvkk): hesap silme + anonimlestirme + saklama sureleri (E-07)"
```

---

### Task 6: Mobil hijyen — l10n altyapısı, metin kalitesi, paketler, test, CI

> **Plan değişikliği (2026-09-05, yürütmeden önce):** Bu görev **ikiye bölündü**.
>
> **Ölçüm:** `mobile/lib` altında 52 sayfa + 23 widget ve **~770 kullanıcıya görünür dize** var
> (toplam 5553 tırnaklı dize, 2712 benzersiz). Step 4'ün "tüm `mobile/lib/**`" kapsamı tek bir
> görevde dürüstçe yapılıp doğrulanamaz: birkaç bin satırlık bir diff olur ve her ekranın
> bozulmadığını kanıtlamanın yolu kalmaz.
>
> **Bölme:**
> - **Task 6 (bu görev):** l10n **altyapısı** (D-19) + **tüm** Türkçe karakter düzeltmeleri (D-20)
>   + planın kendi öncelik sırasının başı olan **auth ve more** ekranlarının ARB'ye taşınması
>   (deseni uçtan uca kanıtlar) + paket temizliği (D-18) + test kapsamı (D-21) + CI (D-22).
> - **Task 6b (yeni):** kalan ekranların ARB'ye taşınması — dosya listesiyle, aşağıda.
>
> **Gerekçe:** K-12'nin çözmek istediği iki sorundan **marka kalitesi** olan (Türkçe karaktersiz
> yazımlar) bu görevde **tamamen** kapanıyor; **teknik borç** olan (hardcoded metin) ise altyapı
> kurulup desen kanıtlandıktan sonra ekran ekran kapanıyor. Kullanıcının gördüğü kusur bugün gidiyor.

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

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **Kapsam ikiye bölündü** — yukarıdaki "Plan değişikliği" notu. Step 4'ün "tüm `mobile/lib/**`"
>    hedefi ölçülünce ~770 dize / 52 ekran çıktı; Task 6b açıldı.
> 2. **Test yalnız ARB'yi değil, `lib/**` kaynak kodunu da denetliyor.** Plan ARB anahtar paritesi +
>    ARB değerlerinde bozuk yazım kontrolü istiyordu; ama ekranların çoğu henüz ARB'de değil ve asıl
>    kusur orada. Kaynak taraması olmasaydı D-20 düzeltmeleri **bir sonraki commit'te geri gelebilirdi**.
>    Tarama kelime **başı** sınırıyla eşleşiyor — ilk yazımda "rozet" içindeki "ozet" yanlış alarm verdi.
> 3. **`flutter_local_notifications` kullanılmıyordu.** Plan onu "kullanılıyor" sayıyordu; `lib/` ve
>    `test/` altında tek import'u yok — push tamamen `firebase_messaging` üzerinden çalışıyor. Kaldırıldı.
>    Toplam **11 paket** kaldırıldı (kod üretimi zinciri dahil: repoda tek bir `*.freezed.dart`/`*.g.dart` yok).
> 4. **Kapsam hedefi %50'ye ulaşılmadı: %15.1 → %15.3.** Ölçüm: `lib/features/**` 18.962 satır, kapsanan
>    2.906. Hedefe çıkmak ~6.600 satırlık ekran (widget) testi demek — bu görevin değil, ayrı bir işin
>    boyutu. Planın **somut** olarak istediği yazıldı (veli cubit'i için 3 değil **6** test). Eşik CI'da
>    **uyarı** olarak duruyor (planın kendi ifadesi: "ilk aşamada hata değil").
> 5. **`flutter analyze` zaten 4 info ile 1 dönüyordu** — yani `--fatal-infos` olmadan da CI kırılırdı.
>    Dördü de kapatıldı (import sırası; `Radio` → `RadioGroup`; `activeColor` → `activeThumbColor`) ve
>    analiz **tamamen temiz** hâle geldi; `--fatal-infos` ancak bu yüzden açılabildi.
> 6. **6 mevcut test güncellendi.** Metin doğru yazıma geçince eski yazımı arayan beklentiler kırıldı
>    (`scheduling_page_test`, `widget_test`, `app_router_test`); ayrıca sayfaları doğrudan pump eden iki
>    yönlendirme testine `AppLocalizations.delegate` **ve `locale: tr`** eklendi — yerel ayar verilmezse
>    test ortamı `en_US`'a düşüp İngilizce değeri render ediyordu.
> 7. **Karşılama ekranındaki uygulama adı düzeltildi**: `EgitimUssu` → **EğitimÜssü** (kök `CLAUDE.md`
>    kuralı: görünen metin Türkçe karakterli). `MaterialApp` başlığı da ARB'den geliyor.

- [x] **Step 1: l10n testini yaz (kırmızı)** — 4 test: ARB anahtar paritesi, ARB değerlerinde bozuk yazım, anahtar adı biçimi ve **`lib/**` kaynak taraması** (sapma #2).
- [x] **Step 2: Kırmızı gör** — 4/4 başarısız; kaynak taraması **188 bozuk yazım** buldu.
- [x] **Step 3: l10n altyapısını kur (D-19)** — `l10n.yaml`, `lib/l10n/app_{tr,en}.arb`, `generate: true`, `app.dart`'ta `AppLocalizations.delegate` + `onGenerateTitle`. Üretilen dosyalar `.gitignore`'da.
- [x] **Step 4: Metinleri taşı** — `more` (`more_page`, 44 dize) + `auth` (7 ekran). **116 ARB anahtarı**, tr/en birebir. Kalanlar Task 6b.
- [x] **Step 5: Türkçe karakter düzeltmesi (D-20)** — **188 → 0**. 22 dosyada 107 benzersiz dize; taşınan ekranlarda ARB'ye, kalanlarda yerinde düzeltildi. Test bunu kalıcı kılıyor.
- [x] **Step 6: Kullanılmayan paketleri temizle (D-18)** — **11 paket** (sapma #3); `doc/architecture/mobile_flutter.md` §10 gerçeğe göre yeniden yazıldı.
- [x] **Step 7: Eksik testleri yaz (D-21)** — `parent_cubit_test.dart`: 6 test (onaysız bağın panel açmaması dahil). **Kapsam hedefe ulaşmadı** — sapma #4.
- [x] **Step 8: CI'yı sıkılaştır (D-22)** — `flutter analyze --fatal-infos` + `flutter test --coverage` + kapsam eşiği uyarısı; iş akışı PR'larda da koşuyor, APK yalnız `main` push'unda üretiliyor.
- [x] **Step 9: Testler** — `flutter test`: **203 başarılı** (öncesi 193) · `flutter analyze --fatal-infos`: **0 sorun** (öncesi 4 info).
- [x] **Step 10: Doküman + commit**

`doc/architecture/mobile_flutter.md` (l10n bölümü + paket listesi), `doc/architecture/ux_rules.md` (metin dili kuralı: tam Türkçe karakter, "EğitimÜssü" yazımı).
```bash
git add mobile .github doc
git commit -m "chore(mobile): l10n altyapisi + metin kalitesi + test kapsami + CI (D-18..D-22/D-12)"
```

---

### Task 6b: Kalan ekranların ARB'ye taşınması (D-19 devamı)

> Task 6'da altyapı kuruldu ve desen `auth` + `more` üzerinde kanıtlandı. Burada kalan ekranlar
> aynı desenle taşınır. **Bu görev bölünebilir** — her feature ayrı commit olabilir.

**Files:** `mobile/lib/features/**` (aşağıdaki sıra), `mobile/lib/l10n/app_{tr,en}.arb`

**Sıra (metin yoğunluğuna göre, en çok kullanılandan):**
1. `dashboard` · 2. `scheduling` (en büyük: `scheduling_page`, `lesson_form_sheet`) ·
3. `lesson_sessions` · 4. `students` · 5. `study` · 6. `parent` · 7. `payments` ·
8. `assignments` · 9. `settings` · 10. `teacher_profile` · 11. `membership` · 12. `messaging` ·
13. `notifications` · 14. `reporting` · 15. `progress` · 16. `shared/widgets`

- [ ] **Step 1:** Feature'ı taşı → `flutter test` → commit (her feature ayrı tur)
- [ ] **Step 2:** Tümü bitince `localization_test.dart`'a "hardcoded metin kalmadı" kontrolü ekle
- [ ] **Step 3:** `doc/architecture/mobile_flutter.md` l10n bölümünü "tamamlandı" olarak güncelle

---

### Task 7: Cihaz oturumları ve telefon doğrulama (M01-4/M01-5/D-03)

**Files:**
- Create: `src/Modules/Identity/Application/DeviceSessionFeatures.cs` *(plan `IdentityFeatures.cs` diyordu — sapma #1)*
- Modify: `src/Modules/Identity/Application/{IdentityFeatures,IdentityPolicies}.cs`
- Modify: `src/Modules/Identity/Domain/IdentityDomainModel.cs` (`RefreshTokenSession.LastUsedOnUtc`) *(sapma #3)*
- Modify: `src/Modules/Identity/API/IdentityModule.cs`
- Modify: `src/Modules/Identity/Infrastructure/{IdentityRepositoryAndSecurity,IdentityDbContext,DependencyInjection}.cs`
- Create: `src/Modules/Identity/Infrastructure/Migrations/*_AddSessionLastUsedAt.cs`
- Modify: `src/Shared/Kernel/ICurrentUser.cs` (`SessionId` — **varsayılan üye**), `src/Shared/Infrastructure/Authentication/HttpContextCurrentUser.cs` *(sapma #2)*
- Create: `mobile/lib/features/more/{domain/device_session_contracts.dart, data/repositories/device_session_repository_impl.dart, presentation/cubit/device_sessions_cubit.dart, presentation/pages/device_sessions_page.dart}` *(sapma #4)*
- Modify: `mobile/lib/core/routing/app_router.dart`, `core/di/injector.dart`, `features/more/presentation/pages/more_page.dart`, `lib/l10n/app_{tr,en}.arb`
- Modify: `tests/Unit/TerminateOtherSessionsTests.cs` (`FakeTokenIssuer` imzası)
- Test: `tests/Unit/DeviceSessionTests.cs`, `mobile/test/features/more/device_sessions_test.dart`
- Doküman: `doc/modules/{m01_identity,00_genel_bakis}.md`, `doc/pages/device_sessions.md` + `00_pages_index.md`

> **Plandan sapma (2026-09-05, uygulanırken düzeltildi):**
> 1. **Yeni dosya `DeviceSessionFeatures.cs`.** `IdentityFeatures.cs` zaten 300+ satır ve 11 command/query
>    barındırıyor; Task 5'te de aynı gerekçeyle ayrı dosya açılmıştı.
> 2. **"Mevcut oturum" jetondaki `jti` ile ayırt edilemiyordu.** Plan `jti`'yi öneriyordu ama o **her
>    jetonda değişen rastgele bir değer** — oturumu göstermiyor. Jetona `sid` talebi eklendi
>    (`ITokenIssuer.Issue(user, sessionId)`) ve `ICurrentUser`'a `SessionId` geldi. `ICurrentUser`'ı
>    **27 test sahtesi** uyguluyor; bu yüzden üye **varsayılan uygulamalı** (`=> null`) tanımlandı —
>    Öğrenilenler #159'un dersi: yaygın uygulanan bir arayüzü tek bir çağıran için genişletme.
> 3. **`LastUsedOnUtc` eklendi ve rotasyonda `CreatedOnUtc` devralınıyor.** Plan `lastUsedOnUtc` alanını
>    şart koşuyordu ama şemada yoktu. Asıl mesele şu: refresh akışı jetonu **döndürüyor** (eski satır
>    iptal, yeni satır açılır). Başlangıç anı devralınmasaydı 30 gün boyunca her yenilemede oturum
>    "az önce açılmış" görünürdü. Migration mevcut satırlara `CreatedOnUtc`'yi kopyalar — varsayılan
>    `0001-01-01` bırakılsaydı ekranda "son kullanım: yıl 1" yazardı.
> 4. **Mobilde tam dilim açıldı** (`domain` + `data` + `cubit` + `page`), plan yalnız sayfayı listeliyordu.
>    Master tasarım §4 mobil katmanı `features/<ad>/{data,domain,presentation}` olarak tanımlıyor;
>    sayfanın içine `ApiClient` çağrısı gömmek o kuralı bozardı.
> 5. **`revoke-all` çıplak sayı değil nesne döndürüyor** (`{ revokedCount }`): çıplak skaler gövde
>    istemcide JSON nesnesi bekleyen çözümleyiciyle çalışmaz ve ileride alan eklenemez.
> 6. **`sid` yoksa toplu iptal reddediliyor** (`409 identity.unknown_session`). Fail-closed: hangi
>    oturumun korunacağı bilinmeden "diğerlerini kapat" sessizce "hepsini kapat"a dönüşür ve kullanıcıyı
>    kendi cihazından da atardı.
> 7. **Listede yalnız aktif oturumlar var.** Rotasyon her yenilemede bir "ölü" satır bırakıyor; hepsi
>    gösterilseydi kullanıcı tek cihazını onlarca oturum olarak görürdü.
> 8. **Mevcut cihazın oturumu ekrandan kapatılamıyor.** Bunun adı "çıkış yap"tır ve o eylem Diğer
>    ekranında zaten var; aynı işi iki ayrı adla sunmak kullanıcıyı yanıltır.

**Interfaces:**
- Produces:
  - `GET /api/identity/sessions` (auth) → `[{ sessionId, deviceName, createdOnUtc, lastUsedOnUtc, isCurrent }]` (M01-4)
  - `POST /api/identity/sessions/{sessionId}/revoke` (auth, yalnız kendi oturumu)
  - `POST /api/identity/sessions/revoke-all` (auth) — mevcut oturum hariç hepsini iptal eder
  - Mobil rota `/account/sessions` (D-03)

> **M01-5 (telefon/SMS OTP) bu planda kapsam dışıdır.** Gerekçe: SMS sağlayıcı sözleşmesi + maliyet kararı gerektirir ve hiçbir akış buna bağlı değil (kimlik doğrulama e-posta üzerinden çalışıyor). Karar verildiğinde ayrı bir plan açılır; `doc/modules/m01_identity.md`'de madde "ertelendi (karar bekliyor)" olarak işaretlenir.

- [x] **Step 1: Testleri yaz (kırmızı)** — 8 test: yalnız kendi aktif oturumları, iptal edilmiş/süresi geçmişlerin gizlenmesi, kendi oturumunu iptal, **başkasınınkinde 403**, `revoke-all` mevcut oturumu korur, **`sid` yoksa fail-closed**, iptal edilen oturum aktif değil, rotasyonda başlangıç anının korunması.
- [x] **Step 2: Kırmızı gör** — **CS0246** ×3 (üç handler de yok).
- [x] **Step 3: Query/command + authorizer + uçlar** — `jti` yerine **`sid`** (sapma #2); 3 uç + 3 authorizer + DI.
- [x] **Step 4: Yeşil gör** — `dotnet test EgitimUssu.slnx`: 4 mimari / **724 birim** / 62 integration, başarısız 0.
- [x] **Step 5: Mobil ekran** — `/account/sessions`: cihaz adı, bağlanma + son kullanım, "Bu cihaz" rozeti, tekil iptal (**yalnız yabancı cihazda**, sapma #8) + "Diğer tüm oturumları kapat"; **her ikisi de onay diyaloğu ister**. `/more` → "Cihaz oturumları" girişi eklendi; metinler ARB'de (15 yeni anahtar).
- [x] **Step 6: Testler** — `flutter test`: **212 başarılı** (öncesi 203; +9) · `flutter analyze --fatal-infos`: **0 sorun**.
- [x] **Step 7: Doküman + commit**

`doc/modules/m01_identity.md` (§3.3 + mobil listesi), `doc/pages/device_sessions.md` + `doc/pages/00_pages_index.md`.
```bash
git add src/Modules/Identity mobile tests doc
git commit -m "feat(identity): cihaz oturumlari listesi + iptal (M01-4/D-03)"
```

---

### Task 8: Kapanış

**Files:** `render.yaml`, `doc/INDEX.md`, `doc/denetim/2026-09-02_eksik_analizi.md`, `doc/architecture/environments.md`

> **Plandan sapma (2026-09-05, yürütülürken düzeltildi):**
> 1. **"Staging'de" değil, "staging profilinde" koşuldu.** Render'a hiç deploy edilmedi (Task 3'ten
>    devreden durum) ve deploy bir **operasyon kararı** — kod değil. Bunun yerine `ASPNETCORE_ENVIRONMENT=Staging`
>    + gerçek Postgres + gerçek OTLP toplayıcısı ile **aynı kod yolu** yerelde koşuldu. Doğrulanan şey
>    kodun davranışıdır; doğrulanmayan tek şey Render'ın kendisidir.
> 2. **`render.yaml` bu görevde değişti.** Step 2 "staging'de gözlemlenebilirliği doğrula" diyordu; blueprint'te
>    **hiç `Observability__*` anahtarı yoktu** — yani deploy edilseydi telemetri kapalı gelecekti. Task 1'in işi
>    dağıtıma bağlanmamıştı; anahtarlar (varsayılan `false`, gerekçesiyle) eklendi.
> 3. **`doc/INDEX.md`'ye 4 değil 9 doküman eklendi.** Plan yalnız yeni 4'ünü istiyordu, ama `doc/architecture/`
>    klasörünün **9 dokümanı** hiç indekslenmemişti (`ux_rules`, `anti_patterns`, `animations`, `accessibility`,
>    `figma_references` dahil). Kök `CLAUDE.md` kuralı INDEX'in tam olmasını şart koşuyor.
> 4. **`git add -A` kullanılmadı.** Çalışma ağacında **paralel bir oturumun** commit'lenmemiş rakip analizi
>    değişiklikleri duruyor; `-A` onları bu commit'e karıştırırdı. Dosyalar tek tek eklendi.
> 5. **Step 2 tam geçmedi → Task 9 açıldı.** Ayrıntı aşağıda.

- [x] **Step 1: Tam testler** — `./scripts/test-with-docker.sh`: 4 mimari / **724 birim** / **62 integration**, başarısız 0, atlanan 0. `flutter test --coverage`: **212 başarılı**, satır kapsamı **%17,1** (3.466/20.240).
- [~] **Step 2: Gözlemlenebilirlik doğrulaması** — staging profili + Jaeger + gerçek `otelcol` ile koşuldu. **Geçen:** HTTP → EF/Npgsql zinciri tek trace'te (`POST /api/scheduling/lessons` → conflict `SELECT` → `INSERT`); her log satırında `TraceId`; `X-Api-Version: v1` canlı; metrikler toplayıcıda okundu (`outbox.pending`=0, `outbox.dispatched{event=LessonScheduledDomainEvent,outcome=success}`=1, `email.sent{outcome=success}`=1). **Geçmeyen:** `command` adımının kendi span'ı yok ve **outbox → handler işi isteğin trace'ine bağlanmıyor** — 1500 trace'in **1488'i** ebeveynsiz arka plan SQL span'ı. → **Task 9**.
- [x] **Step 3: Yedekten geri yükleme tatbikatı** — staging profilinin veritabanında: yedek (256 KB, 123 tablo) → **tüm şemalar düşürüldü (123 → 0 tablo)** → `restore-db.sh --yes` → **123 tablo / 812 satır birebir aynı** (`diff` farksız). Ardından uygulama geri yüklenen DB'ye karşı açıldı: `/health/ready` + `/health/live` **Healthy**, demo öğretmen girişi **200**, ders listesi **200** (elle oluşturulan ders yedeği atlatmış), tekrar tohumlama **idempotent** (öğrenci 5'te kaldı). `--yes` olmadan **çıkış 1**.
- [x] **Step 4: Dokümanlar** — `doc/INDEX.md` §1.1'e **9 satır** + §7'ye 3 soru; `doc/denetim/2026-09-02_eksik_analizi.md`'de **10 satır** güncellendi (C-10 · D-18 · D-20 · D-22 · E-05 · E-07 ✅ tam; C-04 · D-19 · D-21 · E-06 `[~]` kısmi, her biri gerekçesiyle); `doc/architecture/environments.md`'ye §5 Gözlemlenebilirlik.
- [x] **Step 5: Commit**

```bash
git add render.yaml doc/INDEX.md doc/denetim/2026-09-02_eksik_analizi.md doc/architecture/environments.md docs/superpowers
git commit -m "docs: P13 operasyon ve hijyen kapanisi"
```

---

### Task 9: Outbox/handler trace zinciri (Task 8 Step 2'nin kalanı)

> **Neden ayrı görev:** Zincirin asenkron hopu ancak `outbox_messages`'a bir `TraceParent` sütunu
> eklenirse bağlanabilir; o sütun **15 modülün her birinde bir migration** demektir. P13'ün Global
> Constraints'i "başka planların dosyalarını değiştirmez" diyor — bu görev o kuralı bilerek deler,
> o yüzden ayrı ve açıkça karar verilerek yürütülür.

**Files:**
- Modify: `src/Shared/Infrastructure/Observability/ObservabilityExtensions.cs` (`AddSource`)
- Create: `src/Shared/Infrastructure/Observability/EgitimUssuActivitySource.cs`
- Modify: `src/Shared/Infrastructure/Persistence/{OutboxMessage,OutboxBatchItem,EfOutboxStore,ModuleDbContext,DataRetentionService}.cs` *(sapma #1, #3)*
- Modify: `src/Shared/Infrastructure/Messaging/DispatchingEventBus.cs` *(sapma #2)*
- Modify: `src/Shared/Infrastructure/Application/{CommandDispatcher,QueryDispatcher}.cs`
- Modify: 6 modül arka plan servisi *(sapma #4)* — `Payments/PaymentOverdueScanner`, `Assignments/AssignmentDueScanner`, `Identity/AccountDeletionService`, `ProgressTracking/ProgressSnapshotService`, `Notifications/{NotificationDispatching,ParentWeeklySummaryService}`
- Create: `src/Modules/*/Infrastructure/Migrations/*_AddOutboxTraceParent.cs` (**15 modül**)
- Test: `tests/Unit/OutboxTracePropagationTests.cs` (9 test)
- Doküman: `doc/architecture/{backend,environments}.md`, `doc/denetim/2026-09-02_eksik_analizi.md`

> **Plandan sapma (2026-09-06, uygulanırken düzeltildi):**
> 1. **`ModuleDbContext.cs` ve `OutboxBatchItem.cs` de değişti.** Plan yalnız `OutboxMessage`/`EfOutboxStore`
>    diyordu; ama bağlamın **yazıldığı** yer `CaptureOutboxMessages`'tır. Dispatch dakikalar sonra, başka bir
>    thread'de koşar — o an `Activity.Current` boştur; okuma anında yakalamak mümkün değil.
> 2. **Handler span'ı `DispatchingEventBus`'a eklendi** (plan bu dosyayı listelemiyordu). Aynı olayı birden çok
>    modül dinliyor; tek bir dispatch span'ı altında hepsinin süresi birbirine karışır ve "hangisi yavaş"
>    sorusu cevapsız kalırdı.
> 3. **`DataRetentionService` de enstrümante edildi.** Task 8 ölçümündeki gürültünün bir kısmı outbox değil,
>    saklama turunun `inbox_messages` taramasıydı.
> 4. **6 modül arka plan servisine de span eklendi.** İlk ölçümde (yalnız Shared enstrümante edilmişken)
>    gürültü 1488 → 58'e indi ama **sıfırlanmadı**: kalan 58'in 42'si `NotificationDispatching`'in
>    `device_tokens` + `user_settings` yoklamalarıydı. Görev zaten "modüle dokunma" kısıtını (15 migration ile)
>    bilerek deldiği için, hedefi yarım bırakmak yerine yedi tarayıcının hepsine `scan.*` span'ı eklendi.
> 5. **Span link değil, ebeveyn.** Plan "kök + span link" diyordu. Ama kabul kriteri "**tek trace**'te
>    HTTP → command → EF → outbox → handler" ister; link iki ayrı trace bırakır ve Jaeger'ın trace görünümü
>    yalnız ebeveyn-çocuk zincirini izler. Saklanan `traceparent` doğrudan **ebeveyn bağlamı** yapıldı.
> 6. **17 modül var, 15'inin migration'ı oldu.** `Matching` ve `Reviews` henüz iskelet (hiç migration'ları yok).

**Interfaces:**
- `OutboxMessage.TraceParent` (`string?`, W3C `traceparent`, `HasMaxLength(55)`) — yazarken `Activity.Current?.Id`
- `EgitimUssuActivitySource.Name = "EgitimUssu"` — `AddObservability` içinde `AddSource` ile kayıtlı
- Span adları: `command <Tip>` · `query <Tip>` · `outbox.poll` · `outbox.dispatch <EventName>` (Consumer) ·
  `handler <HandlerTipi>` · `retention.sweep` · `scan.<modül>.<iş>`

- [x] **Step 1: Testleri yaz (kırmızı)** — 9 test: satıra `traceparent` yazılması, trace yokken `null`, dispatch'in üreten trace'i devam ettirmesi, bağlam yokken tur span'ının altına düşmesi, **bozuk `traceparent`'ın mesajı kilitlememesi**, boş turda bile `outbox.poll`, handler başına span, komut ve sorgu span'ı.
- [x] **Step 2: Kırmızı gör** — **CS0103** ×3 (`EgitimUssuActivitySource` yok) + **CS1061/CS0117** ×3 (`TraceParent` yok).
- [x] **Step 3: `ActivitySource` + dispatcher/outbox/handler/tarayıcı span'ları** — 733 birim testi yeşil. *(Bu adımda iki gerçek hata testin kendisindeydi: `UseInMemoryDatabase` adı lambda'nın içinde üretilince her scope ayrı veritabanı açıyordu; ve `ActivityListener` süreç geneli olduğu için paralel koşan testlerin span'larını topluyordu — testler artık kendi trace'lerine göre filtreliyor.)*
- [x] **Step 4: `TraceParent` sütunu + 15 migration** — `check-migration-down.sh`: **97 migration, hepsi geri alınabilir**. Integration paketi gerçek Postgres'te 62/62 yeşil (sütun temiz uygulanıyor).
- [x] **Step 5: Yeşil gör + Jaeger'da tek trace** — `./scripts/test-with-docker.sh`: 4 mimari / **733 birim** / 62 integration, başarısız 0, atlanan 0. `dotnet build -warnaserror --no-incremental`: 0 uyarı / 0 hata. **Jaeger kanıtı:** ders oluşturma **tek trace, 18 span** — `POST /api/scheduling/lessons → command CreateLessonScheduleCommand → (SELECT, INSERT) → outbox.dispatch LessonScheduledDomainEvent → 2 handler → outbox.dispatch LessonReminderCreatedDomainEvent`. **Gürültü: ebeveynsiz ham DB kök trace'i 1488 → 0** (29 trace'in hepsi adlandırılmış). 16 outbox satırının **1'inde** `TraceParent` dolu (HTTP'den gelen), 15'i tohumlamadan geldiği için `null` — tasarlandığı gibi.
- [x] **Step 6: Doküman** — `backend.md` §7 (`OutboxMessage` alanları) + §11.2 (span tablosu) + yeni **§11.5 asenkron zincir**; `environments.md` §5 tablosundaki iki ❌ → ✅; `doc/denetim/2026-09-02_eksik_analizi.md` **C-04 → tam ✅**.

```bash
git add src tests doc docs/superpowers
git commit -m "feat(observability): outbox trace zinciri ve arka plan span'lari (C-04)"
```

---

## Kabul Kriterleri

- [x] Trace zinciri uçtan uca görünüyor; her log satırında `TraceId` var — **Task 9'da kapandı**: tek trace / 18 span (HTTP → command → EF → outbox → handler → ikinci nesil olay); ebeveynsiz gürültü trace'i 1488 → 0
- [x] PII hiçbir log/trace alanında yok (örnek istek incelenerek doğrulandı) — istek/trace hattında yok; ⚠️ `Email__Provider=Logging` **bilerek** alıcı + doğrulama jetonunu log'a yazar (dev/staging kolaylığı, üretimde `Smtp`)
- [x] Outbox kuyruk derinliği ve push/e-posta başarı oranı metrik olarak yayınlanıyor — gerçek OTLP toplayıcısında okundu (`push.sent` tanımlı ama tetiklenmedi: cihaz jetonu yok, **Q8**)
- [x] Her `/api/*` yanıtında `X-Api-Version` başlığı var — canlı doğrulandı (200 ve 404 dahil)
- [~] Staging ortamı ayrı DB ile çalışıyor, demo veri idempotent — **kod ✅** (staging profili yerelde uçtan uca koşuldu, tohumlama idempotent); **Render'a deploy edilmedi**
- [x] Yedek alma + geri yükleme tatbikatı başarılı — 123 tablo / 812 satır birebir; uygulama geri yüklenen DB'de `Healthy`
- [x] Hesap silme talebi 30 gün sonra kişisel veriyi anonimleştiriyor, ticari kaydı koruyor — Task 5'te gerçek Postgres'te doğrulandı
- [~] Mobilde kullanıcıya görünen tüm metinler ARB'den geliyor, Türkçe karakterler doğru — **Türkçe karakter ✅ (188 → 0, testle kilitli)**; **ARB taşıması ~50 ekranda sürüyor** (Task 6b)
- [~] Mobil satır kapsamı %50+; CI analiz + test koşuyor — **CI ✅** (`--fatal-infos` + `test --coverage` + eşik adımı); **kapsam %17,1** — eşik CI'da uyarı üretiyor
