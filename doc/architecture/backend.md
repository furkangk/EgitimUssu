---
title: "Backend Mimari (.NET 9 modüler monolit)"
summary: "Çözüm yapısı, modül anatomisi, Shared/Kernel, CQRS, Outbox, persistence, JWT — 15 gerçek modül"
tags: [mimari, backend, dotnet, cqrs, outbox]
authority: code
code_refs:
  - src/**
updated: 2026-11-14
---

# 🧱 Backend Mimari — .NET 9 Modüler Monolit

> **Kapsam:** Backend'in (`src/`) mimari yapısı: çözüm düzeni, modül anatomisi, Shared katmanı, CQRS, persistence,
> Outbox/event akışı, güvenlik ve yeni modül ekleme reçetesi. **Koddan doğrulanmıştır.**
>
> **Otorite:** Modül listesi, durum ve gerçek endpoint envanteri → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md).
> Her modülün domain/iş kuralları → ilgili [`../modules/mNN_*.md`](../modules/00_genel_bakis.md). Kanonik değerler → [`../INDEX.md`](../INDEX.md) §0.
>
> **Güncelleme:** 2026-10-16

---

## 1. Mimari Stil

| İlke | Uygulama |
|------|----------|
| **Modüler Monolit** | Tek host (`API.Host`), bağımsız modüller (`src/Modules/<Ad>`) |
| **Clean Architecture** | Her modülde `API → Application → Domain ← Infrastructure` katmanları; bağımlılık içe doğru |
| **DDD** | `AggregateRoot`, `Entity`, `ValueObject`, `DomainEvent` (Shared/Kernel) |
| **CQRS** | Yazma `ICommand` / okuma `IQuery`, ayrı handler + dispatcher |
| **Outbox** | Domain event → integration event, aynı transaction'da `OutboxMessage` tablosuna yazılır |
| **Result deseni** | İstisna yerine `Result` / `Result<T>` + `Error`; HTTP'ye ProblemDetails olarak çevrilir |

## 2. Çözüm Yapısı

```
src/
├── API.Host/                    → ASP.NET Core host (giriş noktası)
│   ├── Program.cs               → bootstrap: modül keşfi, JWT, rate limit, health, middleware
│   └── ModuleAssemblies.cs      → keşfedilecek modül assembly listesi
│
├── Shared/                      → tüm modüllerin paylaştığı çekirdek
│   ├── Kernel/                  → BaseEntity, AggregateRoot, ValueObject, Result, Error, IClock…
│   ├── Application/             → ICommand/IQuery + Handler arayüzleri, dispatcher sözleşmeleri
│   ├── Contracts/               → IIntegrationEvent, IntegrationEvent (modüller-arası sözleşme)
│   └── Infrastructure/          → ModuleDbContext, Outbox, Redis, Messaging, Auth, Middleware, Health
│
└── Modules/<ModulAdi>/          → her modül 4 ayrı proje
    ├── API/                     → ModuleDefinition + endpoint mapping + request/response DTO
    ├── Application/             → Command/Query + Handler + Validator + Authorizer
    ├── Domain/                  → AggregateRoot, Entity, Enum, DomainEvent (saf domain)
    └── Infrastructure/          → DbContext, EF config, Repository impl, Migrations, DI
```

Modüller (kod adı `src/Modules`): `Identity, Teachers, Students, Scheduling, LessonSessions, Assignments, Payments,
Study, Parents, ProgressTracking, Notifications, Matching, Reviews, Reporting, Settings`. (M16–M18 — Messaging/
Membership/Feedback — henüz kod tarafında yok; planlanan.) Güncel durum → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) §1.

## 3. API.Host — Bootstrap (`Program.cs`)

Host, modülleri **elle değil, yansıma (reflection) ile keşfeder**: `ModuleAssemblies.All` içindeki assembly'lerde
`IModule` (somut, `ModuleDefinition` türevi) tipler bulunur, örneklenir, `Name`'e göre sıralanır ve servisleri kaydedilir.

```csharp
builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddDiscoveredModules(builder.Configuration, ModuleAssemblies.All);
// …
app.MapDiscoveredModules();           // her modülün MapEndpoints'i çağrılır
// E-05: koşulsuz çağrılır; "koşmalı mı" kararı MigrationStartupGuard'ın (üretimde asla).
if (await app.Services.ApplyModuleMigrationsAsync()) { /* … Development: plan kataloğu tohumu */ }
```

Host'un kurduğu çapraz-kesit servisler:

| Konu | Detay |
|------|-------|
| **Kimlik doğrulama** | JWT Bearer (`JwtOptions`: Issuer/Audience/SigningKey, ClockSkew 1 dk) |
| **Yetkilendirme** | `AuthenticatedUser` politikası (oturum zorunlu uçlar için) |
| **Rate limiting** | Sabit pencere, Redis destekli (fail-open): `auth` (10/dk, `/api/identity/*`), `messaging` (30/dk, yalnız `POST /api/messaging/conversations/{id}/messages`), `default` (120/dk, diğer `/api/*`); aşımda 429 + ProblemDetails. **Partition:** `auth`/`default` istemci IP'si (`X-Forwarded-For` ilk girdi), `messaging` **oturum kullanıcı kimliği** — aynı ev/okul ağındaki kullanıcılar birbirinin mesaj kotasını tüketmesin diye. Bu yüzden `UseAuthentication()` limitleyiciden **önce** gelir (yalnız `HttpContext.User`'ı doldurur, hiçbir isteği reddetmez); sıra tersken partition sessizce IP'ye düşüyordu (M16-1, P10 Task 5'te gerçek Redis'e karşı yakalandı) |
| **HTTP tekrar koruması** | `IdempotencyMiddleware`, `Idempotency-Key` taşıyan `/api/*` mutasyonlarında yalnız **kimliği doğrulanmış ve boş olmayan `NameIdentifier` sahibi** kullanıcılar için çalışır. Anahtar kullanıcı + HTTP metodu + yol + istemci anahtarıyla sınırlıdır; başarı yanıtı yeniden döndürülür, sürmekte olan iş 409 alır. **Anonim istekler ve `/api/identity/*` tamamen hariçtir:** giriş/kayıt yanıtları kullanıcılar arasında paylaşılmaz; refresh/logout/oturum işlemleri güncel durumu her çağrıda doğrular ve iptal edilmiş jetonlar önbellekten yeniden verilmez. Redis erişilemezse normal istek yürütülür. |
| **Hata** | `ProblemDetailsExceptionMiddleware` → tüm hatalar tutarlı ProblemDetails gövdesi |
| **Loglama** | JSON console + `RequestContextLoggingMiddleware` (istek bağlamı) |
| **Health** | `/health/live` (canlılık), `/health/ready` (configuration + database hazır mı) |
| **E-posta** | `IEmailSender` (K-01); sağlayıcı `Email:Provider` ile seçilir — `Logging` (varsayılan, göndermez) / `Smtp` (`SmtpEmailSender`, MailKit; herhangi bir relay: Gmail/SES/SendGrid/Resend). Env: `Email__Provider`, `Email__FromEmail`, `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__UseStartTls`, `Email__Smtp__Username`, `Email__Smtp__Password`. `EmailOptionsGuard` startup'ta fail-fast; üretimde `Logging` seçiliyse uyarı log'lanır. Kimlik akışı şablonları: `Identity/Infrastructure/EmailTemplates` (`egitimussu://` derin bağlantı, token URL-kodlu) |
| **Push** | `IPushSender` (K-02); sağlayıcı `Push:Provider` ile seçilir — `Logging` (varsayılan, göndermez) / `Fcm` (`FcmPushSender`, FCM HTTP v1 + service-account OAuth2, `IFcmAccessTokenProvider`). Env: `Push__Provider`, `Push__ProjectId`, `Push__ServiceAccountJson` (veya `Push__ServiceAccountJsonPath`). `PushOptionsGuard` startup'ta fail-fast; üretimde `Logging` seçiliyse uyarı log'lanır. Sonuç üç değerli: `Delivered` / `TokenInvalid` (token kapatılır) / `TransientFailure` (yeniden denenir) |
| **Dosya depolama** | `IFileStorage` (K-03); sağlayıcı `Storage:Provider` ile seçilir — `Local` (varsayılan, `LocalFileStorage`, disk) / `S3` (`S3FileStorage`, AWS SDK; AWS S3 / Cloudflare R2 / MinIO — `ServiceUrl` doluysa path-style). Env: `Storage__Provider`, `Storage__BucketName`, `Storage__ServiceUrl`, `Storage__Region`, `Storage__AccessKeyId`, `Storage__SecretAccessKey`. `StorageOptionsGuard` startup'ta fail-fast. Anahtar deseni `{keyPrefix}/{guid}{uzantı}`; **public bucket yok** — indirme uygulama üzerinden yetkiyle. Sınır: `FileValidation` 10 MB + 13 uzantılık beyaz liste (`storage.file_too_large` / `storage.unsupported_type` / `storage.empty_file`) |
| **Gözlemlenebilirlik** | OpenTelemetry (K-10): trace (ASP.NET Core + HttpClient + Npgsql) + metrik (`EgitimUssu` meter'ı) + log'da `TraceId`. `Observability:Enabled` **varsayılan kapalı**; OTLP adresi boşsa konsol exporter. PII sorgu parametreleri `SensitiveDataMask` ile `***`. Detay: §11 |
| **API sürümleme** | C-10: yayınlanan sözleşme **`v1`** — mevcut `/api/<modul>/...` yolları sürüm eki taşımaz ve **kırılmaz**; kırıcı değişiklik `/api/v2/...` altında ikinci bir grupla açılır. Her `/api/*` yanıtı `X-Api-Version` başlığını taşır (401/404 dahil); `/health/*` ve `/openapi/*` sürümlenmez. Sürüm ucun `ApiVersionMetadata` etiketinden okunur (grup `CreateModuleGroup`'ta etiketlenir), etiket yoksa yol tabanlı varsayılana düşer. Politika + kırıcı değişiklik tanımı + kaldırma prosedürü: [`api_versioning.md`](api_versioning.md) |
| **Ortamlar / tohumlama** | E-06: Development · **Staging** (ayrı servis + **ayrı veritabanı**, `render.yaml`) · Production. Staging açılışta `DemoDataSeeder` ile idempotent demo veri kurar (2 öğretmen / 5 öğrenci / 2 veli / 3 haftalık ders geçmişi / ödev / ödeme / çalışma). Seeder **doğrudan DbContext'e yazmaz** — her kaydı, o kaydı gerçekte açacak kullanıcının kimliğiyle modül komutundan geçirir, böylece iş ve yetki kuralları atlanmaz. `DemoDataSeedGuard` **Production'da bayrak açık olsa bile** engeller. Ayrıntı: [`environments.md`](environments.md) |
| **Yedekleme / migration** | E-05: Üretimde migration **açılışta uygulanmaz** — `MigrationStartupGuard` `ASPNETCORE_ENVIRONMENT=Production` ise `Database:ApplyMigrationsOnStartup` açık olsa bile engeller ve `LogWarning` düşer; şema değişikliği ayrı ve gözetilen bir deploy adımıdır. Yedek/geri yükleme `scripts/backup-db.sh` + `scripts/restore-db.sh` (geri yükleme `--yes` onayı ister, `--single-transaction` ile ya hepsi ya hiçbiri). CI'da `scripts/check-migration-down.sh` her migration'ın `Down()`'ının dolu olduğunu zorlar. Prosedür + RPO/RTO + tatbikat kaydı: [`backup_restore.md`](backup_restore.md) |
| **Meta** | `GET /api/meta/version` → `apiVersion` (**sözleşme** sürümü, `X-Api-Version` ile aynı) + `version` (**derleme** sürümü) + yüklü modül listesi. İkisi ayrı tempoda değişir: derleme her yayında, sözleşme yalnız kırıcı değişiklikte |
| **OpenAPI** | `AddOpenApi` / `MapOpenApi`; belge `info.version` olarak **sözleşme** sürümünü yayınlar (document transformer) |

## 4. Modül Anatomisi (her modül için aynı şablon)

Örnek: **Identity** modülü. Her modül aşağıdaki 4 katmana sahiptir.

### 4.1 API — `ModuleDefinition`
Modülün dış yüzü. `Name`, `RoutePrefix` ve Minimal API uçları. Uçlar iş yapmaz; sadece **dispatcher**'a Command/Query
gönderir ve `Result`'ı HTTP'ye çevirir.

```csharp
public sealed class IdentityModule : ModuleDefinition
{
    public override string Name => "Identity";
    public override string RoutePrefix => "/api/identity";

    public override void RegisterServices(IServiceCollection s, IConfiguration c) => s.AddIdentityModule(c);

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints).RequireRateLimiting("auth");
        var authorized = group.MapGroup("").RequireAuthorization("AuthenticatedUser");

        group.MapPost("/login", async (HttpContext ctx, LoginUserRequest req,
            ICommandDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Dispatch(new LoginUserCommand(req.Email, req.Password, req.DeviceName), ct);
            return ToHttpResult(ctx, result);          // Result → 200 / ProblemDetails
        });
        // … /register, /refresh, /password-reset/*, /email-verification/*, /logout (auth), /users/{id} (auth)
    }
}
```

`ToHttpResult`, `Error.Code`'a göre HTTP durum eşler (`identity.duplicate_email` → 409, `..._not_found` → 404,
`..._invalid_refresh_token` → 401, `shared.forbidden` → 403, aksi 400). Request/Response **`record`** olarak aynı dosyada.

### 4.2 Application — CQRS
- **Command/Query** (intent) + **Handler** (`ICommandHandler<TCommand, Result<T>>`, `IQueryHandler<TQuery, Result<T>>`).
- **Validator** (`ICommandValidator<T>`) ve **Authorizer** (`IQueryAuthorizer<T>`) opsiyonel ek halkalar.
- Handler'lar repository **arayüzlerine** bağımlıdır (Infrastructure'daki impl'e değil) → bağımlılık içe doğru.
- Dispatcher (`ICommandDispatcher` / `IQueryDispatcher`) handler'ı bulur ve çalıştırır.
  Boru hattı sırası **validator → authorizer → handler**; ilk hatada kısa devre olur ve
  `Result` / `Result<T>` hata örneği döner.
- **Delegate önbelleği (O4).** Dispatcher'lar istek tipi başına derlenmiş çağrı delegeleri kullanır
  (`DispatcherInvokerCache`, `DispatcherHandlerInvokerCache<TResponse>`, `DispatcherFailureFactory<TResponse>`
  — `Shared/Infrastructure/Application`). `MakeGenericType` + `Expression.Compile` maliyeti tip başına **bir kez**
  ödenir, sonrası `ConcurrentDictionary` araması olur; sıcak yolda çalışma-zamanı bağlayıcısına (C# `dynamic`)
  ve `MethodInfo.Invoke` yansımasına gerek kalmaz. Dış sözleşme değişmez.

### 4.3 Domain — saf iş modeli
`AggregateRoot<TId>` davranışı kapsüller ve `Raise(domainEvent)` ile olay üretir; dışa bağımlılığı yoktur.

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
{
    private readonly List<DomainEvent> _domainEvents = [];
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void Raise(DomainEvent e) => _domainEvents.Add(e);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### 4.4 Infrastructure — persistence + DI
- `ModuleDbContext` türevi `DbContext`; **modüle özel şema** (`Schema => "identity"`), `DbSet`'ler,
  `IEntityTypeConfiguration` ile EF Core eşleme (snake_case tablolar, enum'lar `HasConversion<string>`).
- Repository implementasyonları, güvenlik servisleri (password hasher, token issuer/protector), DI uzantısı.

```csharp
public static IServiceCollection AddIdentityModule(this IServiceCollection s, IConfiguration c)
{
    s.AddModuleDbContext<IdentityDbContext>(c, "Identity", IdentityDbContext.SchemaName);
    s.AddScoped<IUserAccountRepository, UserAccountRepository>();
    s.AddScoped<ICommandHandler<LoginUserCommand, Result<AuthResponse>>, LoginUserCommandHandler>();
    s.AddScoped<ICommandValidator<LoginUserCommand>, LoginUserCommandValidator>();
    // … diğer handler/validator/authorizer kayıtları
    return s;
}
```

## 5. Shared Katmanı

| Proje | İçerik | Örnek tipler |
|-------|--------|--------------|
| **Kernel** | Domain çekirdeği + ortak sözleşmeler | `Entity`, `AggregateRoot<TId>`, `ValueObject`, `DomainEvent`, `Result`/`Result<T>`/`Error`, `PagedResult`, `IClock`, `ICurrentUser`, `IIdGenerator`, `ProjectionEntity` |
| **Application** | CQRS soyutlamaları | `ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler` (+ dispatcher/validator/authorizer sözleşmeleri) |
| **Contracts** | Modüller-arası entegrasyon sözleşmesi | `IIntegrationEvent`, `IntegrationEvent` |
| **Infrastructure** | Çapraz-kesit altyapı | `ModuleDbContext`, Outbox (`OutboxMessage`, `EfOutboxStore`, `IOutboxStore`), Caching (`LazyRedisConnectionFactory`), Messaging (`IEventBus`, `IDomainEventMapper`), Auth (`HttpContextCurrentUser`), Modules (`IModule`, `ModuleDefinition`, kayıt uzantıları), Middleware, Health, E-posta (`IEmailSender`, `LoggingEmailSender`, `SmtpEmailSender` + `ISmtpClientFactory`/MailKit), Push (`IPushSender`, `PushMessage`/`PushResult`, `LoggingPushSender`, `FcmPushSender` + `IFcmAccessTokenProvider`/Google.Apis.Auth), Depolama (`IFileStorage`, `StoredFile`, `FileValidation`, `LocalFileStorage`, `S3FileStorage`/AWSSDK.S3), Projeksiyon (`ProjectionHandler<TEvent>`, `IProjectionRebuilder`), `SystemClock`, `GuidIdGenerator`, Configuration options |

**`Result` deseni** (istisna yerine):
```csharp
public class Result { public bool IsSuccess { get; } public Error Error { get; }
    public static Result Success(); public static Result Failure(Error error); }
public sealed class Result<TValue> : Result { public TValue? Value { get; } /* … */ }
```

**Configuration option'ları:** `DatabaseOptions` (connection + `ApplyMigrationsOnStartup`), `JwtOptions`,
`RedisOptions`, `OutboxOptions`, `RateLimitOptions`, `EmailOptions` (+`SmtpOptions`), `PushOptions`, `StorageOptions`, `ObservabilityOptions`.

**Startup guard'ları** (ortak desen: `Validate` nedeni döner → health check kullanır; `EnsureValid` fırlatır → `Program.cs` fail-fast):
`JwtSigningKeyGuard`, `ConnectionStringGuard`, `EmailOptionsGuard`, `PushOptionsGuard`, `StorageOptionsGuard`.

## 6. Persistence & Veri İzolasyonu

- **Şema-per-modül:** Her modülün `DbContext`'i ayrı PostgreSQL **şemasında** çalışır (`identity`, `teachers`, …).
  Bu, modül sınırını veritabanı düzeyinde de korur — **modüller birbirinin tablosuna erişmez.**
- **`ModuleDbContext`** taban sınıfı; `Schema` ve `ModuleName` soyut üyeleriyle şemayı ve Outbox bağlamını belirler;
  `IDomainEventMapper` enjekte edilir (kaydet sırasında domain event → Outbox).
- **EF eşleme:** `IEntityTypeConfiguration<T>` sınıfları, `ApplyConfigurationsFromAssembly` ile otomatik yüklenir.
  Tablo adları snake_case (`user_accounts`), enum'lar string, benzersiz indeksler (ör. `NormalizedEmail`).
- **Migration:** Her modülün kendi `Migrations/` klasörü ve `DesignTimeDbContextFactory`'si vardır.
  `ApplyMigrationsOnStartup=true` ise host açılışta tüm modül migration'larını uygular — **Production hariç**
  (E-05: `MigrationStartupGuard` engeller, üretimde migration ayrı bir deploy adımıdır).
  Her migration'ın `Down()`'ı dolu olmalıdır; CI bunu `scripts/check-migration-down.sh` ile zorlar
  (`Up()` boş olan no-op migration'lar muaf). Ayrıntı: [`backup_restore.md`](backup_restore.md).

## 7. Outbox & Event Akışı

Modüller arası iletişim **doğrudan çağrı değil**, event tabanlıdır:

```
Aggregate.Raise(DomainEvent)
   └─ SaveChanges sırasında ModuleDbContext:
        DomainEvent → IDomainEventMapper → IntegrationEvent
        → OutboxMessage (Module, Type, Payload, OccurredOnUtc, TraceParent) AYNI transaction'da yazılır
   └─ Outbox işleyici → IEventBus.PublishAsync → ilgili modüller/bildirim tüketir
```

`OutboxMessage`: `Id, Module, Type, Payload, OccurredOnUtc, ProcessedOnUtc?, Error?, RetryCount,
NextAttemptUtc?, DeadLetteredOnUtc?, TraceParent?`. Son alan **üreten isteğin trace bağlamıdır** —
asenkron hop'ta zincirin kopmaması için (§11.5). Bu desen **en az bir kez**
teslim ve atomik yazma (domain + event tek tx) sağlar. Mevcut `IDomainEventMapper` varsayılanı `NoOpDomainEventMapper`
(modüller mapper'larını ekledikçe gerçek event yayını devreye girer).

## 7.1 Projeksiyon (Read-Model) Deseni

**Ne zaman kullanılır:** Bir modülün ekranı/ucu **başka modüllerin** verisine ihtiyaç duyduğunda. Çapraz-modül
**senkron** DB okuması yasaktır (`Modules_Should_Not_Reference_Other_Modules` mimari testi zorlar); ihtiyaç duyulan
veri, kaynak modülün yaydığı integration event'lerden beslenen bir **projeksiyon** (read-model) satırında tutulur (karar **K-04**).

**Nerede yaşar:** Projeksiyon, **tüketen** modülün kendi şemasındadır. Kaynak modül yalnız event yayar, tüketicisini bilmez.

**Yapı taşları:** `ProjectionEntity` `src/Shared/Kernel/`'dedir (Domain katmanı Infrastructure'a referans veremez — mimari test zorlar); handler ve rebuilder `src/Shared/Infrastructure/Projections/` altındadır.

| Tip | Rol |
|-----|-----|
| `ProjectionEntity` (Shared.**Kernel**) | Projeksiyon satırlarının tabanı: `LastAppliedEventId`, `UpdatedOnUtc`, `MarkApplied(eventId, nowUtc)`. Satırın hangi event'e kadar beslendiğini taşır. |
| `ProjectionHandler<TEvent>` (Shared.Infrastructure) | `IdempotentIntegrationEventHandler` üzerine kurulu taban. Zarfın JSON payload'ını `TEvent`'e çözer, `ProjectAsync(payload, envelope, ct)` çağırır. `TEvent` **payload sözleşmesidir**, `IIntegrationEvent` değil. |
| `IProjectionRebuilder` (Shared.Infrastructure) | `Name` + `RebuildAsync(ct)`. Projeksiyonu kaynak veriden yeniden kurar; yönetim ucu (`POST /api/admin/projections/{name}/rebuild`) P12'de gelir. |

**Kurallar:**

- **Tek yön:** Projeksiyon yalnız event'ten beslenir; handler içinde başka modülün DbContext'i okunmaz.
- **Idempotent:** Aynı event iki kez teslim edilirse projeksiyon **bir kez** uygulanır — dedup `inbox_messages`
  (`EventId`, `Handler`) üzerinden tabandan gelir. Sayaç artıran projeksiyonlar bu yüzden güvenlidir.
- **Atomik:** `ProjectAsync` **SaveChanges çağırmaz**; iş-yazımı + inbox satırı tek transaction'da commit olur.
  `ProjectAsync` hata verirse (ya da `false` dönerse) inbox satırı yazılmaz → mesaj yeniden denenebilir kalır.
- **Okunamayan payload sessizce yutulmaz:** payload `null` çözülürse taban `false` döner, event işlenmiş sayılmaz.
- **Yeniden kurulabilir:** Her projeksiyon `IProjectionRebuilder` ile sıfırdan inşa edilebilmelidir; projeksiyon
  şeması "türetilmiş veri"dir, kayıp durumunda kaynak modüllerden yeniden üretilir.
  **Yeniden inşanın iki adımı vardır ve ikincisi kolayca atlanır:** projeksiyon satırları *ve* o modülün
  `inbox_messages` satırları silinmelidir. Inbox bırakılırsa tekrar oynatma sessizce **boş** bir projeksiyon
  üretir (guard "bu event zaten işlendi" der). Ayrıca rebuild **tek bir projeksiyon deposunu** hedefler:
  olayları genel `IEventBus`'a yeniden yayınlamak diğer modüllerin tüketicilerini de tetikler ve yeniden
  kurmayı amaçlamadığı veriyi bozar — yalnız hedef modülün handler'ları çağrılmalıdır.
  Özellik `tests/Integration/RealProjectionRebuildTests.cs` ile gerçek Postgres'te doğrulanır (P08 Task 6):
  boşalt → aynı akışı aynı `EventId`'lerle oynat → sayaçlar birebir aynı; üçüncü teslimde değişmiyor.
  Eksik olan yalnız geçmiş olayları çeken **host seviyesi orkestrasyon** (P12).

**İlk gerçek tüketici:** M14 Reporting — `teacher_dashboard_projections` (`GET /api/reporting/teachers/{id}/dashboard`).
Sayaçlar saklanmaz, **küme üyeliğinden türetilir** (her ders/ödev/ödeme/öğrenci bir satır): ekleme upsert, çıkarma
"varsa sil" olduğu için "+1/−1 sayaç" deseninin kırıldığı durumlar (aynı ödev hem `Completed` hem `Approved` olur)
sayacı bozmaz. Tarihe bağlı sayaç (bugünkü ders) **saklanmaz**, sorgu anında `IClock` ile hesaplanır.

```
Kaynak modül aggregate → DomainEvent → Outbox → IEventBus
    └─ ProjectionHandler<TPayload> (tüketen modülde)
         ├─ inbox guard (EventId, Handler)  → zaten işlenmişse çık
         ├─ ProjectAsync: projeksiyon satırını STAGE et + MarkApplied(eventId, now)
         └─ taban: iş-yazımı + inbox satırı TEK transaction'da commit
```

## 7.2 İstek Kapsamı Varlık Önbelleği (O2 — çift sorgu yok)

Bir istekte **authorizer** ve **handler** çoğu zaman aynı varlığı yükler (authorizer sahipliği doğrular,
handler işi yapar). Bu iki yükleme iki ayrı `SELECT` üretmemeli. İki mekanizma var; hangisinin geçerli
olduğu **aramanın birincil anahtar üzerinden yapılıp yapılmadığına** bağlıdır:

| Durum | Mekanizma | Nasıl |
|-------|-----------|-------|
| Birincil anahtarla arama (`GetByIdAsync`) | **EF kimlik haritası** | Repository `FindAsync(new object?[] { id }, ct)` kullanır. Varlık aynı `DbContext`'te zaten izleniyorsa DB'ye **hiç gidilmez**. |
| Anahtar dışı arama veya `Include`'lu yükleme | **`IRequestEntityCache`** (scoped) | Authorizer ile handler `cache.GetOrLoadAsync(anahtar, ct => repo.…(ct), ct)` çağırır; hangisi önce koşarsa yükler. |

Kurallar:
- `IRequestEntityCache` **scoped**'dır — bir isteğin verisi başka bir isteğe sızmaz (`ServiceCollectionExtensions`).
- Anahtar `(tip, key)` çiftidir; aynı anahtar farklı tiple istendiğinde eşleşme olmaz.
- **Bulunamayan varlık saklanmaz** — yokluk kalıcı bir cevap değildir.
- Aynı varlık iki farklı yoldan aranabiliyorsa (örn. öğrenci profili id / kullanıcı id) anahtarlar ayrıştırılır
  (`StudentProfileCacheKeys`).
- `FindAsync` yalnız **filtresiz, `Include`'suz** birincil anahtar aramasında kullanılır. `Include`'lu yükleme
  (`StudentProfileRepository.GetByIdAsync`) `FindAsync`'e çevrilemez — orada önbellek kullanılır.

Doğrulama InMemory'den alınamaz; gerçek Postgres'te EF'in `Executed DbCommand` günlükleri sayılır
(`tests/Integration/RealSingleEntityLoadTests.cs`).

## 8. Güvenlik

- **JWT Bearer:** access token; `refresh` ucu ile yenileme; `RefreshTokenSession` (cihaz başına) DB'de hash'li tutulur.
- **Yetki:** `AuthenticatedUser` politikası; `ICurrentUser` (`HttpContextCurrentUser`) ile handler'larda aktif kullanıcı.
- **Rate limiting:** kimlik uçları `auth` (10/dk), genel `default` (120/dk).
- **Parola/token:** `IPasswordHasher` (ASP.NET hasher), `ITokenProtector` (SHA-256) ile reset/verify token'ları hash'li.
- **Hata sızdırmama:** Tüm hata yanıtları ProblemDetails; 401/403 challenge'ları özel `ApiErrorHttpResults` ile tutarlı.
- **Sır repoda yok:** Bağlantı dizesi, JWT anahtarı ve SMTP kimlik bilgileri yalnız ortam değişkeninden (`ConnectionStrings__Postgres`, `Jwt__SigningKey`, `Email__Smtp__Username`, `Email__Smtp__Password`); eksik/zayıf değerde uygulama açılmaz.

### 8.1 Öğretmen–öğrenci erişim kapsamı (P15 T2, K-13)

Öğretmenin bir öğrenci üzerindeki yetkisi **rolde değil ilişkide** durur. Tek kaynak
`Shared/Contracts/TeacherStudentAccessContract.cs`:

```csharp
public enum TeacherAccessScope { None = 0, Private = 1, Class = 2 }
public interface ITeacherStudentAccess
{ Task<TeacherAccessScope> GetScopeAsync(Guid teacherUserId, Guid studentId, CancellationToken ct); }
```

| Kapsam | Ne zaman | Verdiği yetki |
|--------|----------|---------------|
| `Private` | Bağ `Linked` ve arşivli değil **veya** profil `TeacherManaged`, bu öğretmence açılmış ve bağı yaşıyor (`Manual`/`InviteSent`, arşivli değil) | Ders, ödeme, oturum, veli daveti, mesaj, gelişim/rapor |
| `Class` | Sınıf üyeliği — **P22'ye kadar hiç dönmez** | Özel ders yetkisi **vermez** (K-17, K-18) |
| `None` | Arşivli / reddedilmiş (`Rejected`) / kopmuş (`Disconnected`) bağ, hiç bağ yok, başkasının profilinde `Manual` bağ | — |

- **Uygulayan:** Students (`Infrastructure/TeacherStudentAccess.cs`). Eski `IStudentDirectory.IsLinkedTeacherOfStudentAsync`
  geriye uyumluluk için kalır ve `GetScopeAsync == Private`'a delege eder (Messaging, ProgressTracking, Reporting).
- **Karşılaştırma kuralı:** yazma uçları `!= None` değil **`== Private`** sorar — P22'de `Class` dönmeye başlayınca
  sınıf öğretmeni özel ders kaydı açamamalı.
- **Kapsam soran yazma uçları:** ders oluştur/güncelle/ertele (M04), ödeme oluştur/güncelle (M07), manuel oturum (M05,
  ayrıca bağlandığı planlı ders aynı öğretmen–öğrenci ikilisine ait olmalı → `403 lesson_sessions.schedule_not_owned`),
  veli daveti (M03). Kapsam yoksa `403 shared.forbidden` ("Bu öğrenciyle aktif bir özel ders bağınız yok."). **Admin muaf.**
- **Bilinçli olarak sormayanlar:** ders iptal/silme/tamamlama, oturum başlat/iptal/tamamla ve okumalar yalnız sahipliğe
  bakar — bağı biten öğretmen eski kayıtlarını temizleyebilir.
- **Kapsam soran okuma (P15 T3 / N-28):** öğrenci profili okuma (`StudentProfileQueryAuthorizer`) — sahip / açan öğretmen /
  veli kontrolleri başarısızsa `Teacher` rolüne `== Private` sorulur. Birleşme sonrası kanonik profil öğrencinin kendi
  açtığıdır (`CreatedByTeacherUserId` boş); bağı taşınan öğretmen profili açabilmeli. Profil **yazma** genişletilmedi.
- **Test:** kural gerçek sorguya karşı (`TeacherStudentAccessTests`, EF InMemory); her yazma ucu için "başka öğretmen /
  bağı arşivlenmiş öğretmen / bağsız öğretmen → 403" (`WriteEndpointLinkAuthorizationTests`); DI kaydı ve HTTP eşlemesi
  gerçek Postgres'te (`RealWriteLinkAuthorizationTests`).

## 9. Hata Yönetimi

İş hataları istisna fırlatmaz; `Result.Failure(new Error(code, message))` döner. API katmanı `Error.Code` → HTTP
durum eşler. Beklenmeyen istisnalar `ProblemDetailsExceptionMiddleware` tarafından yakalanır. Hata kodları modül
ön ekiyle ad-alanlıdır (`identity.*`, `shared.*`).

## 10. Yeni Modül Ekleme Reçetesi

1. `src/Modules/<Ad>/{API, Application, Domain, Infrastructure}` 4 projeyi oluştur.
2. **Domain:** AggregateRoot + Entity + Enum + DomainEvent.
3. **Application:** Command/Query + Handler (+ Validator/Authorizer), repository **arayüzü**.
4. **Infrastructure:** `ModuleDbContext` (yeni şema) + EF config + Repository impl + `AddXModule` DI uzantısı + ilk migration.
5. **API:** `ModuleDefinition` türevi (`Name`, `RoutePrefix`, `MapEndpoints`).
6. `ModuleAssemblies.All`'a modülün assembly'sini ekle → host onu otomatik keşfeder.
7. **Dokümanı güncelle (KALICI KURAL):** `modules/mNN_<ad>.md`, `modules/00_genel_bakis.md` (indeks + endpoint envanteri),
   `modules/veri_modeli.md` ve `INDEX.md`. Bkz. kökteki `CLAUDE.md`.

## 11. Gözlemlenebilirlik (C-04 / K-10)

**Kurulum:** `Shared/Infrastructure/Observability/ObservabilityExtensions.AddObservability(configuration, serviceName)`
— `Program.cs`'te `AddSharedInfrastructure` hemen ardından çağrılır. OpenTelemetry .NET; exporter OTLP.

### 11.1 Açma/kapama

| Ayar | Varsayılan | Anlamı |
|------|-----------|--------|
| `Observability:Enabled` | `false` | **Kapalıyken hiçbir exporter/enstrümantasyon kurulmaz** — testte ve CI'da maliyet sıfır |
| `Observability:OtlpEndpoint` | `""` | OTLP toplayıcı (ör. `http://localhost:4317`). **Boşsa konsol exporter** |
| `Observability:ServiceName` | `egitimussu-api` | Toplayıcıda görünen servis adı |
| `Observability:ServiceVersion` | `""` | Boşsa çalışan derlemenin sürümü |

> **`EgitimUssuMetrics` her koşulda kaydedilir** — `Enabled=false` olsa bile. İş kodu (outbox, push,
> e-posta) ona bağımlıdır ve her ortamda çağırabilmelidir; dinleyici yokken ölçüm zaten ücretsizdir.
> Kapalıyken kurulmayan tek şey exporter ve enstrümantasyondur.

> **Boş endpoint neden sessizce hiçbir şey yapmıyor?** "Açtım ama hiçbir yere gitmiyor" durumu fark
> edilmeden kalmasın diye konsola yazılır (§1 çıtası: sessiz başarısızlık yok).

### 11.2 Trace

Kaynaklar: **ASP.NET Core** (gelen istek) · **HttpClient** (giden istek; FCM/SMTP çağrıları) ·
**Npgsql** (SQL komutları) · **`EgitimUssu`** (uygulamanın kendi span'ları — `EgitimUssuActivitySource`).

Uygulamanın kendi span'ları, enstrümantasyon paketlerinin göremediği aradaki adımları doldurur:

| Span | Nerede | Ne gösterir |
|------|--------|-------------|
| `command <CommandType>` | `CommandDispatcher` | Hangi komut koştu; doğrulama + yetki + handler süresi |
| `query <QueryType>` | `QueryDispatcher` | Aynısı sorgu tarafında |
| `outbox.poll` | `EfOutboxStore.ProcessPendingAsync` | Bir yoklama turu (claim + sayım sorguları buranın altında) |
| `outbox.dispatch <EventName>` | `EfOutboxStore` (mesaj başına) | Mesajın yayınlanması; **ebeveyni üreten istektir** (§11.6) |
| `handler <HandlerType>` | `DispatchingEventBus` | Olayı dinleyen her handler ayrı ayrı |
| `retention.sweep` | `DataRetentionProcessor` | Saklama süresi dolan outbox/inbox satırlarının temizliği |

- `/health*` istekleri **bilerek filtrelenir**: saniyede bir çağrılıp trace'i doldurur ve gerçek isteği gizlerlerdi.
- ⚠️ **EF Core enstrümantasyonu kullanılmıyor**: `OpenTelemetry.Instrumentation.EntityFrameworkCore`'un
  kararlı sürümü yok (yalnız `beta`). Yerine Npgsql'in **kararlı** `Npgsql.OpenTelemetry` köprüsü
  (`AddNpgsql()`) kullanılır; SQL komutları zaten span olarak yayılır. Paket sürümü Npgsql 9.x'e
  **sabitlenmiştir** (`9.0.4`) — serbest bırakılırsa Npgsql'i 10'a çekiyor ve EF sağlayıcısı (9.0.4)
  desteklenmeyen bir sürücüyle kalıyor.

### 11.3 Metrikler

Enstrümantasyondan gelenler: `http.server.request.duration`, `http.server.active_requests`,
`http.client.*`. Uygulamanın kendi metrikleri (meter: **`EgitimUssu`**):

| Metrik | Tip | Etiketler | Alarm adayı |
|--------|-----|-----------|-------------|
| `egitimussu.outbox.pending` | Gauge | — | **Evet** — sürekli artıyorsa dispatcher durmuştur |
| `egitimussu.outbox.dispatched` | Counter | `event`, `outcome` | `outcome=failure` oranı |
| `egitimussu.push.sent` | Counter | `status` (`Delivered`/`TokenInvalid`/`TransientFailure`) | `TransientFailure` oranı |
| `egitimussu.email.sent` | Counter | `outcome` | `failure` oranı |

> **`outbox.pending` claim edilen parti değil, gerçek bekleyen sayısıdır.** Parti `Outbox:BatchSize`
> ile sınırlı olduğundan 10.000 mesaj biriktiğinde bile 20'de tavan yapar ve alarm hiç ötmezdi;
> bu yüzden tur başına ayrı bir `COUNT` çalışır (15 sn'lik döngüde ihmal edilebilir).

> **Push/e-posta ölçümü sağlayıcının içinde değil, sarmalayıcıdadır** (`MeteredPushSender`,
> `MeteredEmailSender`). Her sağlayıcıya ayrı yazılsaydı yarın eklenecek üçüncü sağlayıcı ölçümü
> sessizce atlayabilirdi; sarmalayıcı kompozisyon noktasında bir kez kurulur.

> **Yüksek kardinalite yasağı API ile zorlanır:** hiçbir kayıt metodu kullanıcı kimliği/e-posta/token
> almaz ve `EgitimUssuMetricsTests` bunu yansımayla denetler. Böyle bir boyut zaman serisi sayısını
> kullanıcı sayısıyla büyütür.

### 11.4 Log korelasyonu ve PII maskesi

- Yapılandırılmış JSON log (console). Her erişim satırı hem `CorrelationId` (istemciden gelen
  `X-Correlation-Id` ya da üretilen) hem **`TraceId`** taşır — log'dan trace'e geçiş ancak ikisi
  aynı satırdayken mümkündür.
- **PII log'a yazılmaz:** sorgu dizesi `SensitiveDataMask`'ten geçer. Adında `token`, `password`,
  `secret`, `email`, `phone`, `apikey`, `authorization` **geçen** her parametrenin değeri `***`
  olur (`accessToken`, `refresh_token`, `userEmail` de yakalanır). Anahtarlar ve zararsız değerler
  korunur — okunamayan log'a kimse bakmaz.

### 11.5 Asenkron zincir: outbox trace yayılımı

`outbox_messages.TraceParent` (W3C `traceparent`, 55 karakter) mesajı **üreten** isteğin bağlamını
taşır. `ModuleDbContext.CaptureOutboxMessages` satırı yazarken `Activity.Current?.Id`'yi kopyalar;
`EfOutboxStore` dispatch ederken bu değeri `ActivityContext.TryParse` ile çözüp span'ın **ebeveyni**
yapar. Sonuç: Jaeger'da `POST /api/scheduling/lessons` trace'ini açtığınızda, dakikalar sonra koşan
outbox dispatch'i ve handler'ları **aynı trace'in altında** görünür.

```
POST /api/scheduling/lessons
├── command CreateLessonScheduleCommand
│   ├── SELECT EXISTS (çakışma kontrolü)          ← Npgsql
│   └── INSERT INTO scheduling.lesson_schedules   ← Npgsql
└── outbox.dispatch LessonScheduledDomainEvent    ← dakikalar sonra, arka planda
    ├── handler LessonScheduledNotificationHandler
    └── handler TeacherDashboardProjectionHandler
```

Üç kural:

- **Bağlam yazma anında yakalanır, okuma anında değil.** Dispatch başka bir thread'de, dakikalar
  sonra koşar; o an `Activity.Current` çoktan boştur.
- **Bağlam yoksa akış bozulmaz.** Tohumlama ve zamanlanmış tarayıcılar trace'siz üretir; `TraceParent`
  `null` kalır ve dispatch span'ı içinde bulunduğu `outbox.poll` turunun altına düşer.
- **Bozuk bir `traceparent` mesajı kilitlemez.** `TryParse` başarısız olursa değer yok sayılır;
  yayınlama normal şekilde sürer.

> **Neden arka plan işine span şart:** enstrümante edilmemiş bir yoklayıcı trace'i temizlemez,
> *kirletir*. Npgsql her SQL komutunu yayar ve ebeveyni yoksa **her biri ayrı bir kök trace** olur.
> P13 Task 8'in staging ölçümünde 1500 trace'in **1488'i** böyle oluşmuştu (adları da veritabanı adı
> olarak görünüyordu) ve 12 gerçek HTTP isteği aralarında kayboluyordu. Periyodik bir işçi eklerken
> span'ını da ekleyin.

### 11.6 Yerelde doğrulama

```bash
docker run -d --name otel-jaeger -p 16686:16686 -p 4317:4317 jaegertracing/all-in-one
ConnectionStrings__Postgres="InMemory:otel" \
Jwt__SigningKey="<32+ karakter>" Jwt__Issuer=egitimussu Jwt__Audience=egitimussu \
Observability__Enabled=true Observability__OtlpEndpoint=http://localhost:4317 \
dotnet run --project src/API.Host
# → http://localhost:16686 (servis: egitimussu-api)
```
OTLP adresi verilmezse metrikler ve trace'ler **konsola** yazılır (toplayıcı kurmadan doğrulama).

- **Health:** `/health/live` + `/health/ready` (config + DB). `ConfigurationHealthCheck`, `DatabaseConnectionHealthCheck`.
- **Sürüm/meta:** `GET /api/meta/version`.

## 12. Yerel Geliştirme

### Testleri gerçek altyapıyla koşma

Testcontainers tabanlı testler (`RealDatabase`, `RealOutbox`, `RealRedis`, `RealStudentMerge`, `RealMessagingChain` …) çalışan bir Docker
daemon gerektirir; yoksa `Skipped` olurlar. Docker varken tam paketi koşmak için:

```bash
cp infra/.env.example infra/.env   # bir kez (yerel altyapı yığını için)
./scripts/test-with-docker.sh
```

Script Docker'ı doğrular, Compose kuruluysa `infra/docker-compose.yml` yığınını (Postgres + Redis, healthcheck'li)
ayağa kaldırır, `dotnet test EgitimUssu.slnx` koşar ve yığını kapatır. Compose kurulu değilse yığın adımı atlanır —
Testcontainers kendi container'larını yönetir, testler yine gerçek DB ile koşar.

Docker yoksa `dotnet test EgitimUssu.slnx` de çalışır; Testcontainers testleri `Skipped` olur (CI'da koşarlar).

---

> İlgili: sistem geneli → [`00_genel_bakis.md`](00_genel_bakis.md) · modüller (gerçek) → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) ·
> mimari açıklar/öncelik → [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) · ER → [`../modules/veri_modeli.md`](../modules/veri_modeli.md)

*Backend Mimari | Güncelleme: 2026-11-14 (P15 Task 3 / N-28: §8.1'e kapsam soran okuma — öğrenci profili okuma `Private` kapsamlı öğretmene açık) · 2026-11-13 (P15 Task 2 / N-08, N-21: yeni **§8.1 öğretmen–öğrenci erişim kapsamı** — `ITeacherStudentAccess` + `TeacherAccessScope { None, Private, Class }` tek kaynak; ders/ödeme/manuel oturum/veli daveti yazma uçları `== Private` sorar, Admin muaf; iptal/silme/okuma yalnız sahiplik) · 2026-10-16 (denetim: HTTP idempotency anonim/kimliksiz isteklerde ve Identity uçlarında kapatıldı; kullanıcılar arası yanıt ve eski jeton tekrarı engellendi, `IdempotencyIdentityIsolationTests`) · 2026-09-06 (P13 Task 9 / C-04: §11.2'ye **uygulamanın kendi span'ları** tablosu (`command`/`query`/`outbox.poll`/`outbox.dispatch`/`handler`/`retention.sweep`) ve yeni **§11.5 asenkron zincir** — `outbox_messages.TraceParent` ile üreten isteğin bağlamı mesajla taşınıyor, dispatch o trace'i devam ettiriyor; enstrümante edilmemiş yoklayıcının trace'i nasıl kirlettiği ölçümle anlatıldı) · 2026-09-05 (P13 Task 4 / E-05: §3'e **Yedekleme / migration** satırı — üretimde açılış migration'ı `MigrationStartupGuard` ile kapatıldı, `ApplyModuleMigrationsAsync` artık kararın sahibi ve `bool` döndürüyor; §6 migration maddesine `Down()` kuralı + CI kontrolü eklendi. Prosedür: `backup_restore.md`) · 2026-09-05 (P13 Task 3 / E-06: §3'e **Ortamlar / tohumlama** satırı — staging ayrı servis + ayrı veritabanı, açılışta idempotent demo veri; seeder modül komutlarından geçer (kimliğe bürünerek), Production'da guard bayrağı yok sayar. Ayrıntı `environments.md`) · 2026-09-05 (P13 Task 2 / C-10: §3'e **API sürümleme** satırı — yayınlanan sözleşme `v1`, mevcut yollar sürüm eki taşımaz ve kırılmaz, kırıcı değişiklik `/api/v2/...` altında açılır; her `/api/*` yanıtında `X-Api-Version` (401/404 dahil), `/health*` ve `/openapi*` sürümlenmez. `GET /api/meta/version` artık `apiVersion` (sözleşme) ile `version` (derleme) ayrımını yapıyor, OpenAPI belgesi sözleşme sürümünü yayınlıyor. Politika: `api_versioning.md`) · 2026-09-05 (P13 Task 1 / C-04+K-10: §11 Gözlemlenebilirlik yeniden yazıldı — OpenTelemetry trace (ASP.NET Core + HttpClient + Npgsql) + metrik (`EgitimUssu` meter'ı: outbox/push/e-posta) + log'da `TraceId` ve PII maskesi; `Observability:Enabled` varsayılan **kapalı**. EF Core enstrümantasyonu **kullanılmadı** (kararlı sürümü yok), yerine kararlı `Npgsql.OpenTelemetry` — Npgsql 9.x'e sabitlendi) · 2026-09-04 (P10 Task 5 / M16-1: §3 rate limiting satırı gerçeğe göre yazıldı — üçüncü politika `messaging` (30/dk) ve **kullanıcı başına partition**; `UseAuthentication()` limitleyicinin önüne alındı, sıra tersken partition sessizce IP'ye düşüyordu · P08 Task 6: §7.1 yeniden-kurulabilirlik maddesi genisletildi — inbox satirlari da silinmeli, rebuild tek modulu hedefler; ozellik RealProjectionRebuildTests ile gercek Postgres'te dogrulandi · P07: dispatcher delegate onbellegi — DispatcherInvokerCache/DispatcherHandlerInvokerCache/DispatcherFailureFactory, dynamic kaldirildi (O4) · P07: istek kapsamı varlık önbelleği (IRequestEntityCache) + PK aramalarında FindAsync — O2 · P07: projeksiyon (read-model) deseni — ProjectionEntity + ProjectionHandler<TEvent> + IProjectionRebuilder · P04: IFileStorage + LocalFileStorage/S3FileStorage, StorageOptions/StorageOptionsGuard, FileValidation · P03: IPushSender + FcmPushSender, PushOptions/PushOptionsGuard)*
