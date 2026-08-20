---
title: "Backend Mimari (.NET 9 modüler monolit)"
summary: "Çözüm yapısı, modül anatomisi, Shared/Kernel, CQRS, Outbox, persistence, JWT — 15 gerçek modül"
tags: [mimari, backend, dotnet, cqrs, outbox]
authority: code
code_refs:
  - src/**
updated: 2026-06-24
---

# 🧱 Backend Mimari — .NET 9 Modüler Monolit

> **Kapsam:** Backend'in (`src/`) mimari yapısı: çözüm düzeni, modül anatomisi, Shared katmanı, CQRS, persistence,
> Outbox/event akışı, güvenlik ve yeni modül ekleme reçetesi. **Koddan doğrulanmıştır.**
>
> **Otorite:** Modül listesi, durum ve gerçek endpoint envanteri → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md).
> Her modülün domain/iş kuralları → ilgili [`../modules/mNN_*.md`](../modules/00_genel_bakis.md). Kanonik değerler → [`../INDEX.md`](../INDEX.md) §0.
>
> **Güncelleme:** 2026-06-24

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
if (databaseOptions.ApplyMigrationsOnStartup)
    await app.Services.ApplyModuleMigrationsAsync();   // her modülün migration'ı
```

Host'un kurduğu çapraz-kesit servisler:

| Konu | Detay |
|------|-------|
| **Kimlik doğrulama** | JWT Bearer (`JwtOptions`: Issuer/Audience/SigningKey, ClockSkew 1 dk) |
| **Yetkilendirme** | `AuthenticatedUser` politikası (oturum zorunlu uçlar için) |
| **Rate limiting** | Sabit pencere: `auth` (10 istek/dk), `default` (120 istek/dk); aşımda 429 + ProblemDetails |
| **Hata** | `ProblemDetailsExceptionMiddleware` → tüm hatalar tutarlı ProblemDetails gövdesi |
| **Loglama** | JSON console + `RequestContextLoggingMiddleware` (istek bağlamı) |
| **Health** | `/health/live` (canlılık), `/health/ready` (configuration + database hazır mı) |
| **Meta** | `GET /api/meta/version` → servis sürümü + yüklü modül listesi |
| **OpenAPI** | `AddOpenApi` / `MapOpenApi` |

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
| **Kernel** | Domain çekirdeği + ortak sözleşmeler | `Entity`, `AggregateRoot<TId>`, `ValueObject`, `DomainEvent`, `Result`/`Result<T>`/`Error`, `PagedResult`, `IClock`, `ICurrentUser`, `IIdGenerator` |
| **Application** | CQRS soyutlamaları | `ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler` (+ dispatcher/validator/authorizer sözleşmeleri) |
| **Contracts** | Modüller-arası entegrasyon sözleşmesi | `IIntegrationEvent`, `IntegrationEvent` |
| **Infrastructure** | Çapraz-kesit altyapı | `ModuleDbContext`, Outbox (`OutboxMessage`, `EfOutboxStore`, `IOutboxStore`), Caching (`LazyRedisConnectionFactory`), Messaging (`IEventBus`, `IDomainEventMapper`), Auth (`HttpContextCurrentUser`), Modules (`IModule`, `ModuleDefinition`, kayıt uzantıları), Middleware, Health, `SystemClock`, `GuidIdGenerator`, Configuration options |

**`Result` deseni** (istisna yerine):
```csharp
public class Result { public bool IsSuccess { get; } public Error Error { get; }
    public static Result Success(); public static Result Failure(Error error); }
public sealed class Result<TValue> : Result { public TValue? Value { get; } /* … */ }
```

**Configuration option'ları:** `DatabaseOptions` (connection + `ApplyMigrationsOnStartup`), `JwtOptions`,
`RedisOptions`, `OutboxOptions`.

## 6. Persistence & Veri İzolasyonu

- **Şema-per-modül:** Her modülün `DbContext`'i ayrı PostgreSQL **şemasında** çalışır (`identity`, `teachers`, …).
  Bu, modül sınırını veritabanı düzeyinde de korur — **modüller birbirinin tablosuna erişmez.**
- **`ModuleDbContext`** taban sınıfı; `Schema` ve `ModuleName` soyut üyeleriyle şemayı ve Outbox bağlamını belirler;
  `IDomainEventMapper` enjekte edilir (kaydet sırasında domain event → Outbox).
- **EF eşleme:** `IEntityTypeConfiguration<T>` sınıfları, `ApplyConfigurationsFromAssembly` ile otomatik yüklenir.
  Tablo adları snake_case (`user_accounts`), enum'lar string, benzersiz indeksler (ör. `NormalizedEmail`).
- **Migration:** Her modülün kendi `Migrations/` klasörü ve `DesignTimeDbContextFactory`'si vardır.
  `ApplyMigrationsOnStartup=true` ise host açılışta tüm modül migration'larını uygular.

## 7. Outbox & Event Akışı

Modüller arası iletişim **doğrudan çağrı değil**, event tabanlıdır:

```
Aggregate.Raise(DomainEvent)
   └─ SaveChanges sırasında ModuleDbContext:
        DomainEvent → IDomainEventMapper → IntegrationEvent
        → OutboxMessage (Module, Type, Payload, OccurredOnUtc) AYNI transaction'da yazılır
   └─ Outbox işleyici → IEventBus.PublishAsync → ilgili modüller/bildirim tüketir
```

`OutboxMessage`: `Id, Module, Type, Payload, OccurredOnUtc, ProcessedOnUtc?, Error?`. Bu desen **en az bir kez**
teslim ve atomik yazma (domain + event tek tx) sağlar. Mevcut `IDomainEventMapper` varsayılanı `NoOpDomainEventMapper`
(modüller mapper'larını ekledikçe gerçek event yayını devreye girer).

## 8. Güvenlik

- **JWT Bearer:** access token; `refresh` ucu ile yenileme; `RefreshTokenSession` (cihaz başına) DB'de hash'li tutulur.
- **Yetki:** `AuthenticatedUser` politikası; `ICurrentUser` (`HttpContextCurrentUser`) ile handler'larda aktif kullanıcı.
- **Rate limiting:** kimlik uçları `auth` (10/dk), genel `default` (120/dk).
- **Parola/token:** `IPasswordHasher` (ASP.NET hasher), `ITokenProtector` (SHA-256) ile reset/verify token'ları hash'li.
- **Hata sızdırmama:** Tüm hata yanıtları ProblemDetails; 401/403 challenge'ları özel `ApiErrorHttpResults` ile tutarlı.

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

## 11. Gözlemlenebilirlik

- **Loglama:** yapılandırılmış JSON (console); `RequestContextLoggingMiddleware` ile istek korelasyonu.
- **Health:** `/health/live` + `/health/ready` (config + DB). `ConfigurationHealthCheck`, `DatabaseConnectionHealthCheck`.
- **Sürüm/meta:** `GET /api/meta/version`.

---

> İlgili: sistem geneli → [`00_genel_bakis.md`](00_genel_bakis.md) · modüller (gerçek) → [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) ·
> mimari açıklar/öncelik → [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md) · ER → [`../modules/veri_modeli.md`](../modules/veri_modeli.md)

*Backend Mimari | Güncelleme: 2026-06-24*
