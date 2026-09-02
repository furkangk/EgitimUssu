# P03 — Push Bildirim ve Bildirim Merkezi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bildirimleri gerçekten teslim etmek: cihaz token kaydı, FCM üzerinden push gönderimi, tüm roller için in-app bildirim listesi (okundu/okunmadı + rozet) ve mobilde izin/handler/deep-link akışı.

**Architecture:** `Shared/Infrastructure/Push` altında `IPushSender` soyutlaması; `FcmPushSender` (FCM HTTP v1, service-account OAuth) ve `LoggingPushSender` (dev). Notifications modülüne iki yeni aggregate girer: `DeviceToken` (kullanıcı ↔ cihaz token'ı) ve `UserNotification` (rolden bağımsız in-app bildirim; mevcut `ParentNotification` bunun özel hali olarak kalır, yeni akışlar `UserNotification` üretir). `NotificationDispatchProcessor` artık `MarkSent` demeden önce gerçekten gönderir; FCM'in `UNREGISTERED`/`INVALID_ARGUMENT` yanıtında token devre dışı bırakılır. Mobilde `firebase_messaging` başlatılır, izin istenir, token kaydedilir/yenilenir ve bildirime dokunma `egitimussu://` derin bağlantısıyla ilgili ekrana götürür.

**Tech Stack:** .NET 9, `Google.Apis.Auth` (service-account token), `HttpClient` (FCM HTTP v1), EF Core, xUnit; Flutter `firebase_messaging`, `flutter_local_notifications`, `go_router`.

**Spec:** `docs/superpowers/specs/2026-09-02-saglamlastirma-master-design.md` (karar **K-02**)

## Global Constraints

- **Sessiz başarısızlık yok:** Push gönderilemezse `LogError` + `LessonReminder`/`UserNotification` **gönderildi işaretlenmez**; bir sonraki turda tekrar denenir (maksimum deneme sonrası `Failed`).
- **Fail-open değil, fail-fast:** `Push:Provider=Fcm` iken `ProjectId` veya kimlik dosyası yoksa uygulama açılmaz. Varsayılan `Logging`.
- **Modül sınırı:** Cihaz token'ı ve in-app bildirim yalnız Notifications modülünde. Diğer modüller push göndermez; integration event üretir.
- **Tercih kapısı:** Her gönderimden önce `UserSetting` bayrakları kontrol edilir (P05'te tamamlanacak `IUserNotificationPreferences` sözleşmesi üzerinden; bu planda arayüz tanımlanır ve varsayılan "hepsi açık" implementasyonu kullanılır).
- **Zaman:** `IClock.UtcNow`. **Kimlik:** `IIdGenerator.New()`. **Sonuç:** `Result`/`Result<T>`.
- **Migration:** `dotnet ef migrations add <Ad> --project src/Modules/Notifications/Infrastructure --startup-project src/API.Host --context NotificationsDbContext`
- **Commit:** Conventional Commits, görev başına bir commit.

---

### Task 1: `DeviceToken` aggregate + kayıt/silme uçları

**Files:**
- Modify: `src/Modules/Notifications/Domain/NotificationsDomainModel.cs` (yeni `DeviceToken` + `DevicePlatform`)
- Modify: `src/Modules/Notifications/Application/NotificationFeatures.cs` (command/query + handler + repository arayüzü)
- Modify: `src/Modules/Notifications/Application/NotificationPolicies.cs` (authorizer)
- Create: `src/Modules/Notifications/Infrastructure/DeviceTokenRepository.cs`
- Modify: `src/Modules/Notifications/Infrastructure/NotificationsDbContext.cs` (DbSet + konfigürasyon)
- Modify: `src/Modules/Notifications/Infrastructure/DependencyInjection.cs`
- Modify: `src/Modules/Notifications/API/NotificationsModule.cs` (2 endpoint)
- Test: `tests/Unit/DeviceTokenTests.cs`

**Interfaces:**
- Produces:
  - `enum DevicePlatform { Android = 1, IOS = 2, Web = 3 }`
  - `sealed class DeviceToken : AggregateRoot<Guid>` — `Guid UserId`, `string Token`, `DevicePlatform Platform`, `string? DeviceName`, `bool IsActive`, `DateTime CreatedOnUtc`, `DateTime LastSeenOnUtc`; metotlar `Touch(DateTime nowUtc)`, `Deactivate(DateTime nowUtc)`.
  - `sealed record RegisterDeviceTokenCommand(Guid UserId, string Token, DevicePlatform Platform, string? DeviceName) : ICommand<Result>`
  - `sealed record UnregisterDeviceTokenCommand(Guid UserId, string Token) : ICommand<Result>`
  - `interface IDeviceTokenRepository { Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken ct); Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken ct); Task AddAsync(DeviceToken token, CancellationToken ct); Task SaveChangesAsync(CancellationToken ct); }`
  - `POST /api/notifications/device-tokens` (auth) · `DELETE /api/notifications/device-tokens/{token}` (auth)

- [ ] **Step 1: Domain testini yaz (kırmızı)**

`tests/Unit/DeviceTokenTests.cs`:
```csharp
using EgitimUssu.Modules.Notifications.Domain;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class DeviceTokenTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_Token_Should_Be_Active()
    {
        var token = new DeviceToken(Guid.NewGuid(), Guid.NewGuid(), "fcm-token", DevicePlatform.Android, "Pixel 8", Now);
        Assert.True(token.IsActive);
        Assert.Equal(Now, token.LastSeenOnUtc);
    }

    [Fact]
    public void Touch_Should_Update_LastSeen_And_Reactivate()
    {
        var token = new DeviceToken(Guid.NewGuid(), Guid.NewGuid(), "fcm-token", DevicePlatform.IOS, null, Now);
        token.Deactivate(Now.AddMinutes(1));
        Assert.False(token.IsActive);

        token.Touch(Now.AddMinutes(2));
        Assert.True(token.IsActive);
        Assert.Equal(Now.AddMinutes(2), token.LastSeenOnUtc);
    }
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~DeviceTokenTests"`
Expected: FAIL — tip yok.

- [ ] **Step 3: Domain'i yaz**

`NotificationsDomainModel.cs` sonuna:
```csharp
public enum DevicePlatform
{
    Android = 1,
    IOS = 2,
    Web = 3
}

/// <summary>
/// Bir kullanıcının bir cihazındaki push token'ı. Aynı token yeniden kaydedilirse yeni satır
/// açılmaz, mevcut satır <see cref="Touch"/> ile tazelenir (FCM token'ları rotasyona uğrar).
/// </summary>
public sealed class DeviceToken : AggregateRoot<Guid>
{
    private DeviceToken()
    {
    }

    public DeviceToken(Guid id, Guid userId, string token, DevicePlatform platform, string? deviceName, DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        Token = token;
        Platform = platform;
        DeviceName = deviceName;
        IsActive = true;
        CreatedOnUtc = createdOnUtc;
        LastSeenOnUtc = createdOnUtc;
    }

    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; }
    public string? DeviceName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime LastSeenOnUtc { get; private set; }

    public void Touch(DateTime nowUtc)
    {
        IsActive = true;
        LastSeenOnUtc = nowUtc;
    }

    /// <summary>FCM "UNREGISTERED"/"INVALID_ARGUMENT" döndüğünde veya kullanıcı çıkış yaptığında çağrılır.</summary>
    public void Deactivate(DateTime nowUtc)
    {
        IsActive = false;
        LastSeenOnUtc = nowUtc;
    }
}
```

- [ ] **Step 4: Testi çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~DeviceTokenTests"`
Expected: PASS.

- [ ] **Step 5: Application katmanı (command + handler + authorizer)**

`NotificationFeatures.cs`:
```csharp
public sealed record RegisterDeviceTokenCommand(Guid UserId, string Token, DevicePlatform Platform, string? DeviceName)
    : ICommand<Result>;

public sealed record UnregisterDeviceTokenCommand(Guid UserId, string Token) : ICommand<Result>;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class RegisterDeviceTokenCommandHandler : ICommandHandler<RegisterDeviceTokenCommand, Result>
{
    private static readonly Error InvalidToken = new("notifications.invalid_device_token", "Cihaz token'ı geçersiz.");
    private readonly IDeviceTokenRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public RegisterDeviceTokenCommandHandler(IDeviceTokenRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result> Handle(RegisterDeviceTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(InvalidToken);
        }

        var existing = await _repository.GetByTokenAsync(command.Token, cancellationToken);
        if (existing is null)
        {
            await _repository.AddAsync(
                new DeviceToken(_idGenerator.New(), command.UserId, command.Token, command.Platform, command.DeviceName, _clock.UtcNow),
                cancellationToken);
        }
        else
        {
            existing.Touch(_clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```
`UnregisterDeviceTokenCommandHandler`: token'ı bulur, varsa `Deactivate(_clock.UtcNow)`, yoksa yine `Result.Success()` (idempotent).

`NotificationPolicies.cs` → her iki komut için `ICommandAuthorizer<T>`: `_currentUser.IsAuthenticated` **ve** (`UserId` eşleşiyor veya `Admin`), aksi halde `shared.forbidden`.

- [ ] **Step 6: Infrastructure (repository + DbContext + DI + migration)**

`DeviceTokenRepository.cs` — `SettingsDbContext`/`UserSettingRepository` desenini izle.
`NotificationsDbContext.cs`:
```csharp
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
```
ve konfigürasyon sınıfı:
```csharp
internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Token).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.Platform).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.DeviceName).HasMaxLength(128);
        builder.HasIndex(entity => entity.Token).IsUnique();
        builder.HasIndex(entity => new { entity.UserId, entity.IsActive });
    }
}
```
`DependencyInjection.cs` → repository + 2 handler + 2 authorizer kaydı.

Run: `dotnet ef migrations add AddDeviceTokens --project src/Modules/Notifications/Infrastructure --startup-project src/API.Host --context NotificationsDbContext`
Expected: `Migrations/<zaman>_AddDeviceTokens.cs` üretildi.

- [ ] **Step 7: Endpoint'leri ekle**

`NotificationsModule.cs`:
```csharp
        group.MapPost("/device-tokens", RegisterDeviceTokenAsync)
            .WithSummary("Cihaz push token'ını kaydeder/tazeler")
            .RequireAuthorization("AuthenticatedUser");

        group.MapDelete("/device-tokens/{token}", UnregisterDeviceTokenAsync)
            .WithSummary("Cihaz push token'ını devre dışı bırakır (çıkış)")
            .RequireAuthorization("AuthenticatedUser");
```
İstek gövdesi:
```csharp
public sealed record RegisterDeviceTokenRequest(Guid UserId, string Token, DevicePlatform Platform, string? DeviceName);
```
Metotlar `SettingsModule.SetStudySharingAsync` desenini izler (`ICommandDispatcher` + `ApiErrorHttpResults`).

- [ ] **Step 8: Testler + commit**

Run: `dotnet test EgitimUssu.slnx`
Expected: yeşil (yeni `AuthorizationCoverageValidator` şikayeti yoksa authorizer'lar doğru kayıtlı demektir).
```bash
git add src/Modules/Notifications tests/Unit/DeviceTokenTests.cs
git commit -m "feat(notifications): cihaz push token kaydi (M11-2)"
```

---

### Task 2: `IPushSender` + FCM implementasyonu

**Files:**
- Create: `src/Shared/Infrastructure/Push/IPushSender.cs`
- Create: `src/Shared/Infrastructure/Push/PushMessage.cs`
- Create: `src/Shared/Infrastructure/Push/PushResult.cs`
- Create: `src/Shared/Infrastructure/Push/LoggingPushSender.cs`
- Create: `src/Shared/Infrastructure/Push/FcmPushSender.cs`
- Create: `src/Shared/Infrastructure/Configuration/PushOptions.cs`
- Create: `src/Shared/Infrastructure/Configuration/PushOptionsGuard.cs`
- Modify: `src/Shared/Infrastructure/ServiceCollectionExtensions.cs`
- Modify: `src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj` (`Google.Apis.Auth`)
- Modify: `src/API.Host/Program.cs` (guard), `appsettings.json`
- Test: `tests/Unit/FcmPushSenderTests.cs`, `tests/Unit/PushOptionsGuardTests.cs`

**Interfaces:**
- Produces:
  - `sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data)`
  - `enum PushDeliveryStatus { Delivered, TokenInvalid, TransientFailure }`
  - `sealed record PushResult(PushDeliveryStatus Status, string? Error)`
  - `interface IPushSender { Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken ct = default); }`
  - `sealed class PushOptions { public string Provider = "Logging"; public string ProjectId = ""; public string ServiceAccountJsonPath = ""; public string ServiceAccountJson = ""; }`

- [ ] **Step 1: Guard + gönderici testlerini yaz (kırmızı)**

`tests/Unit/PushOptionsGuardTests.cs`:
```csharp
[Fact]
public void Fcm_Without_ProjectId_Should_Throw()
    => Assert.Throws<InvalidOperationException>(() =>
        PushOptionsGuard.Validate(new PushOptions { Provider = "Fcm" }, isDevelopment: false));

[Fact]
public void Fcm_Without_Credentials_Should_Throw()
    => Assert.Throws<InvalidOperationException>(() =>
        PushOptionsGuard.Validate(new PushOptions { Provider = "Fcm", ProjectId = "egitimussu" }, isDevelopment: false));

[Fact]
public void Logging_Should_Pass()
    => PushOptionsGuard.Validate(new PushOptions(), isDevelopment: false);
```

`tests/Unit/FcmPushSenderTests.cs` — `HttpMessageHandler` sahtesiyle üç senaryo:
```csharp
[Fact]
public async Task Successful_Response_Should_Return_Delivered() { /* 200 + {"name":"projects/x/messages/1"} */ }

[Fact]
public async Task Unregistered_Error_Should_Return_TokenInvalid()
{
    // 404 + {"error":{"status":"NOT_FOUND","details":[{"errorCode":"UNREGISTERED"}]}}
    // Assert: result.Status == PushDeliveryStatus.TokenInvalid
}

[Fact]
public async Task Server_Error_Should_Return_TransientFailure()
{
    // 503
    // Assert: result.Status == PushDeliveryStatus.TransientFailure
}
```
Sahte handler: `sealed class StubHttpMessageHandler : HttpMessageHandler` — kurucu `(HttpStatusCode status, string body)`, `SendAsync` bunu döndürür. `FcmPushSender` kurucusu `IHttpClientFactory` yerine doğrudan `HttpClient` alsın ki test enjekte edebilsin; erişim token'ı için `IFcmAccessTokenProvider` arayüzü kullanılsın ve testte sabit token döndüren sahte verilsin.

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~Push"`
Expected: FAIL.

- [ ] **Step 3: Paketi ekle**

Run: `dotnet add src/Shared/Infrastructure/EgitimUssu.Shared.Infrastructure.csproj package Google.Apis.Auth`

- [ ] **Step 4: Sözleşmeleri ve seçenekleri yaz**

`PushMessage.cs` / `PushResult.cs` / `IPushSender.cs`:
```csharp
namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>Tek bir push bildirimi. `Data` alanı mobilde derin bağlantı için kullanılır (ör. {"route":"/lesson-sessions"}).</summary>
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public enum PushDeliveryStatus
{
    Delivered = 1,
    /// <summary>Token artık geçerli değil → kalıcı olarak devre dışı bırakılmalı.</summary>
    TokenInvalid = 2,
    /// <summary>Geçici hata → tekrar denenmeli.</summary>
    TransientFailure = 3
}

public sealed record PushResult(PushDeliveryStatus Status, string? Error);

public interface IPushSender
{
    Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default);
}
```
`PushOptions.cs` / `PushOptionsGuard.cs` — `EmailOptions`/`EmailOptionsGuard` (P02) deseninin birebir eşleniği: `Provider = "Fcm"` ise `ProjectId` zorunlu ve `ServiceAccountJsonPath` **veya** `ServiceAccountJson` dolu olmalı.

`LoggingPushSender.cs` — `LoggingEmailSender` gibi log'lar ve `PushDeliveryStatus.Delivered` döner.

- [ ] **Step 5: `FcmPushSender`'ı yaz**

`src/Shared/Infrastructure/Push/FcmPushSender.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Shared.Infrastructure.Configuration;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>Service-account ile FCM erişim token'ı üretir (1 saatlik, önbelleklenir).</summary>
public interface IFcmAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

internal sealed class GoogleFcmAccessTokenProvider : IFcmAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly PushOptions _options;

    public GoogleFcmAccessTokenProvider(IOptions<PushOptions> options) => _options = options.Value;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var credential = string.IsNullOrWhiteSpace(_options.ServiceAccountJson)
            ? GoogleCredential.FromFile(_options.ServiceAccountJsonPath)
            : GoogleCredential.FromJson(_options.ServiceAccountJson);

        return await credential.CreateScoped(Scope).UnderlyingCredential
            .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
    }
}

/// <summary>
/// FCM HTTP v1 üzerinden push gönderir. Android ve iOS (APNs proxy) tek kanaldan (karar K-02).
/// </summary>
internal sealed class FcmPushSender : IPushSender
{
    private readonly HttpClient _httpClient;
    private readonly IFcmAccessTokenProvider _tokenProvider;
    private readonly PushOptions _options;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(
        HttpClient httpClient,
        IFcmAccessTokenProvider tokenProvider,
        IOptions<PushOptions> options,
        ILogger<FcmPushSender> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default)
    {
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";

        var payload = new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title = message.Title, body = message.Body },
                data = message.Data,
                android = new { priority = "high" },
                apns = new { headers = new Dictionary<string, string> { ["apns-priority"] = "10" } }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new PushResult(PushDeliveryStatus.Delivered, null);
        }

        if (IsTokenInvalid(response.StatusCode, body))
        {
            _logger.LogInformation("FCM token geçersiz, devre dışı bırakılacak: {Body}", body);
            return new PushResult(PushDeliveryStatus.TokenInvalid, body);
        }

        _logger.LogWarning("FCM gönderimi başarısız ({Status}): {Body}", response.StatusCode, body);
        return new PushResult(PushDeliveryStatus.TransientFailure, body);
    }

    private static bool IsTokenInvalid(HttpStatusCode statusCode, string body)
    {
        if (statusCode is not (HttpStatusCode.NotFound or HttpStatusCode.BadRequest))
        {
            return false;
        }

        return body.Contains("UNREGISTERED", StringComparison.Ordinal)
            || body.Contains("INVALID_ARGUMENT", StringComparison.Ordinal);
    }
}
```

- [ ] **Step 6: DI + konfigürasyon**

`ServiceCollectionExtensions.cs`:
```csharp
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.AddSingleton<IFcmAccessTokenProvider, GoogleFcmAccessTokenProvider>();
        services.AddHttpClient<FcmPushSender>();
        services.AddScoped<IPushSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PushOptions>>().Value;
            return string.Equals(options.Provider, "Fcm", StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<FcmPushSender>()
                : ActivatorUtilities.CreateInstance<LoggingPushSender>(provider);
        });
```
`appsettings.json`:
```json
  "Push": {
    "Provider": "Logging",
    "ProjectId": "",
    "ServiceAccountJsonPath": "",
    "ServiceAccountJson": ""
  },
```
`Program.cs` → `EmailOptionsGuard` çağrısının ardına `PushOptionsGuard.Validate(...)`.

- [ ] **Step 7: Testleri çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~Push"`
Expected: PASS (6 test).

- [ ] **Step 8: Commit**

```bash
git add src/Shared/Infrastructure/Push src/Shared/Infrastructure/Configuration src/API.Host tests/Unit
git commit -m "feat(push): IPushSender + FCM HTTP v1 gonderici"
```

---

### Task 3: Hatırlatmaları gerçekten gönder + geçersiz token'ı kapat

**Files:**
- Modify: `src/Modules/Notifications/Infrastructure/NotificationDispatching.cs:24-38`
- Modify: `src/Modules/Notifications/Domain/NotificationsDomainModel.cs` (`LessonReminder`'a `MarkFailed`)
- Modify: `src/Modules/Notifications/Application/NotificationFeatures.cs` (`ILessonReminderRepository`'ye gerekli metotlar)
- Test: `tests/Unit/NotificationDispatchProcessorTests.cs`

**Interfaces:**
- Consumes: `IPushSender`, `IDeviceTokenRepository`, `ILessonReminderRepository`, `IClock`.
- Produces: `LessonReminder.MarkFailed(string error, DateTime nowUtc)` — `AttemptCount++`, `LastError`, 5 denemeden sonra `Status = Failed`.

- [ ] **Step 1: Testi yaz (kırmızı)**

`tests/Unit/NotificationDispatchProcessorTests.cs`:
```csharp
[Fact]
public async Task Due_Reminder_Should_Be_Sent_To_All_Active_Device_Tokens()
{
    // Arrange: 1 due reminder + 2 aktif token; sahte IPushSender Delivered döner
    // Act: DispatchDueRemindersAsync
    // Assert: pushSender 2 kez çağrıldı, reminder.Status == Sent
}

[Fact]
public async Task Reminder_Should_Not_Be_Marked_Sent_When_Push_Fails()
{
    // Sahte sender TransientFailure döner
    // Assert: reminder.Status != Sent, AttemptCount == 1
}

[Fact]
public async Task Invalid_Token_Should_Be_Deactivated()
{
    // Sahte sender TokenInvalid döner
    // Assert: deviceToken.IsActive == false
}

[Fact]
public async Task Reminder_Without_Active_Token_Should_Be_Marked_Sent_Once()
{
    // Kullanıcının hiç aktif token'ı yok → sonsuz tekrar denememeli
    // Assert: reminder.Status == Sent (teslim edilecek cihaz yok)
}
```

- [ ] **Step 2: Çalıştır, kırmızı gör**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~NotificationDispatchProcessorTests"`
Expected: FAIL.

- [ ] **Step 3: `NotificationDispatchProcessor`'ı gerçek gönderime çevir**

`NotificationDispatching.cs`:
```csharp
    public async Task<int> DispatchDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var dueReminders = await _repository.ListDuePendingAsync(now, cancellationToken);
        var sentCount = 0;

        foreach (var reminder in dueReminders)
        {
            var tokens = await _deviceTokens.ListActiveByUserAsync(reminder.UserId, cancellationToken);
            if (tokens.Count == 0)
            {
                // Teslim edilecek cihaz yok; kuyruğu tıkamasın diye gönderilmiş sayılır.
                reminder.MarkSent(now);
                sentCount++;
                continue;
            }

            var anyDelivered = false;
            string? lastError = null;

            foreach (var token in tokens)
            {
                var result = await _pushSender.SendAsync(
                    token.Token,
                    new PushMessage(
                        reminder.Title,
                        reminder.Message,
                        new Dictionary<string, string> { ["route"] = "/scheduling", ["lessonId"] = reminder.LessonScheduleId.ToString() }),
                    cancellationToken);

                switch (result.Status)
                {
                    case PushDeliveryStatus.Delivered:
                        anyDelivered = true;
                        break;
                    case PushDeliveryStatus.TokenInvalid:
                        token.Deactivate(now);
                        break;
                    case PushDeliveryStatus.TransientFailure:
                        lastError = result.Error;
                        break;
                }
            }

            if (anyDelivered)
            {
                reminder.MarkSent(now);
                sentCount++;
            }
            else
            {
                reminder.MarkFailed(lastError ?? "Aktif cihaza teslim edilemedi.", now);
            }
        }

        if (dueReminders.Count > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken);
            await _deviceTokens.SaveChangesAsync(cancellationToken);
        }

        return sentCount;
    }
```
> `reminder.Title` / `reminder.Message` / `reminder.UserId` gerçek alan adları için `NotificationsDomainModel.cs`'e bak; yoksa `LessonReminder`'a ekle (öğretmen kullanıcı kimliği zaten var olmalı).
> Kurucuya `IPushSender _pushSender` ve `IDeviceTokenRepository _deviceTokens` enjekte et; DI kaydını güncelle.

`LessonReminder`'a:
```csharp
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public void MarkFailed(string error, DateTime nowUtc)
    {
        AttemptCount++;
        LastError = error;
        if (AttemptCount >= 5)
        {
            Status = LessonReminderStatus.Failed;
        }
    }
```
`LessonReminderStatus` enum'una `Failed` ekle. Migration üret:
Run: `dotnet ef migrations add AddReminderDeliveryTracking --project src/Modules/Notifications/Infrastructure --startup-project src/API.Host --context NotificationsDbContext`

- [ ] **Step 4: Testleri çalıştır**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~NotificationDispatchProcessorTests"`
Expected: PASS (4 test).

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Notifications tests/Unit/NotificationDispatchProcessorTests.cs
git commit -m "feat(notifications): hatirlatmalari gercek push ile gonder + gecersiz token kapat (A-04/Y5)"
```

---

### Task 4: Tüm roller için in-app bildirim (`UserNotification`)

**Files:**
- Modify: `src/Modules/Notifications/Domain/NotificationsDomainModel.cs`
- Modify: `src/Modules/Notifications/Application/NotificationFeatures.cs`
- Modify: `src/Modules/Notifications/Application/NotificationPolicies.cs`
- Create: `src/Modules/Notifications/Infrastructure/UserNotificationRepository.cs`
- Modify: `src/Modules/Notifications/Infrastructure/NotificationsDbContext.cs`, `DependencyInjection.cs`
- Modify: `src/Modules/Notifications/API/NotificationsModule.cs` (3 endpoint)
- Modify: `src/Modules/Notifications/Infrastructure/ParentEventNotificationHandler.cs` (aynı olaylardan `UserNotification` da üret)
- Test: `tests/Unit/UserNotificationTests.cs`

**Interfaces:**
- Produces:
  - `enum UserNotificationKind { LessonReminder = 1, AssignmentCreated = 2, AssignmentDue = 3, AssignmentMissed = 4, PaymentDue = 5, PaymentOverdue = 6, LessonCompleted = 7, MessageReceived = 8, System = 9 }`
  - `sealed class UserNotification : AggregateRoot<Guid>` — `Guid UserId`, `UserNotificationKind Kind`, `string Title`, `string Body`, `string? Route`, `DateTime CreatedOnUtc`, `DateTime? ReadOnUtc`, `bool IsRead => ReadOnUtc.HasValue`; metot `MarkRead(DateTime nowUtc)`.
  - `GET /api/notifications/users/{userId}/notifications?onlyUnread=&skip=&take=` → `PagedResult<UserNotificationResponse>`
  - `GET /api/notifications/users/{userId}/unread-count` → `{ count: int }`
  - `POST /api/notifications/{notificationId}/read`

- [ ] **Step 1: Testi yaz (kırmızı)**

`tests/Unit/UserNotificationTests.cs`:
```csharp
[Fact]
public void MarkRead_Should_Set_ReadOn_Once()
{
    var notification = new UserNotification(Guid.NewGuid(), Guid.NewGuid(), UserNotificationKind.AssignmentCreated,
        "Yeni ödev", "Matematik ödevi eklendi", "/student/assignments", Now);
    Assert.False(notification.IsRead);

    notification.MarkRead(Now.AddMinutes(1));
    Assert.True(notification.IsRead);
    Assert.Equal(Now.AddMinutes(1), notification.ReadOnUtc);

    notification.MarkRead(Now.AddMinutes(5)); // ikinci çağrı zamanı değiştirmez
    Assert.Equal(Now.AddMinutes(1), notification.ReadOnUtc);
}
```

- [ ] **Step 2: Kırmızı gör → domain'i yaz → yeşil**

Run (önce): `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~UserNotificationTests"` → FAIL
Domain:
```csharp
public enum UserNotificationKind
{
    LessonReminder = 1,
    AssignmentCreated = 2,
    AssignmentDue = 3,
    AssignmentMissed = 4,
    PaymentDue = 5,
    PaymentOverdue = 6,
    LessonCompleted = 7,
    MessageReceived = 8,
    System = 9
}

/// <summary>
/// Rolden bağımsız in-app bildirim. Veli-özel <see cref="ParentNotification"/> korunur;
/// yeni akışlar bu genel tipi üretir (öğrenci ve öğretmen bildirim merkezi — M11-3).
/// </summary>
public sealed class UserNotification : AggregateRoot<Guid>
{
    private UserNotification() { }

    public UserNotification(Guid id, Guid userId, UserNotificationKind kind, string title, string body, string? route, DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        Kind = kind;
        Title = title;
        Body = body;
        Route = route;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid UserId { get; private set; }
    public UserNotificationKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    /// <summary>Mobilde dokunulunca gidilecek rota (ör. "/student/assignments").</summary>
    public string? Route { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ReadOnUtc { get; private set; }
    public bool IsRead => ReadOnUtc.HasValue;

    public void MarkRead(DateTime nowUtc) => ReadOnUtc ??= nowUtc;
}
```
Run (sonra): aynı komut → PASS.

- [ ] **Step 3: Query/command + handler + authorizer + repository**

- `ListUserNotificationsQuery(Guid UserId, bool OnlyUnread, int Skip, int Take) : IQuery<Result<PagedResult<UserNotificationResponse>>>`
- `GetUnreadCountQuery(Guid UserId) : IQuery<Result<int>>`
- `MarkNotificationReadCommand(Guid NotificationId) : ICommand<Result>`
- Authorizer'lar: kullanıcı yalnız **kendi** bildirimlerine erişir (Admin istisna). `MarkNotificationReadCommand` için authorizer bildirimi yükleyip `UserId` karşılaştırır.
- `IUserNotificationRepository`: `ListAsync`, `CountUnreadAsync`, `GetByIdAsync`, `AddAsync`, `SaveChangesAsync`.
- `PagedResult<T>` `Shared.Kernel`'de mevcut; onu kullan.

- [ ] **Step 4: Endpoint'ler + DbContext + migration**

`NotificationsModule.cs`'e 3 endpoint (hepsi `RequireAuthorization("AuthenticatedUser")`).
`NotificationsDbContext` → `DbSet<UserNotification>` + `user_notifications` tablosu; `HasIndex(e => new { e.UserId, e.ReadOnUtc })`.
Run: `dotnet ef migrations add AddUserNotifications --project src/Modules/Notifications/Infrastructure --startup-project src/API.Host --context NotificationsDbContext`

- [ ] **Step 5: Olay tüketicilerini `UserNotification` üretecek şekilde genişlet**

`ParentEventNotificationHandler.cs` (ve kardeşleri) `IdempotentIntegrationEventHandler` tabanını korur; `ApplyAsync` içinde veli bildirimine **ek olarak** ilgili öğrenci/öğretmen kullanıcısı için `UserNotification` stage eder ve mümkünse push kuyruğuna düşer. Push gönderimi handler içinde **senkron yapılmaz**; `UserNotification` yazılır, `NotificationDispatcher` turu bunu da alır (aynı `IPushSender` yolu).

> Bu adımda yeni bir arka plan servisi yazma: `NotificationDispatcher`'ın döngüsüne "gönderilmemiş `UserNotification`'ları da gönder" adımını ekle ve `UserNotification`'a `DateTime? PushedOnUtc` alanı koy.

- [ ] **Step 6: Testler + commit**

Run: `dotnet test EgitimUssu.slnx`
```bash
git add src/Modules/Notifications tests/Unit/UserNotificationTests.cs
git commit -m "feat(notifications): tum roller icin in-app bildirim + okundu/rozet (M11-3/M11-4)"
```

---

### Task 5: Mobil — Firebase başlatma, izin, token kaydı, mesaj yakalama

**Files:**
- Modify: `mobile/pubspec.yaml` (`firebase_core` ekle)
- Create: `mobile/lib/core/push/push_service.dart`
- Modify: `mobile/lib/main.dart` (Firebase init + arka plan handler)
- Modify: `mobile/lib/core/di/injector.dart` (PushService kaydı)
- Modify: `mobile/lib/features/auth/presentation/cubit/auth_cubit.dart` (giriş → token kaydı, çıkış → token silme)
- Create: `mobile/lib/features/notifications/data/repositories/device_token_repository_impl.dart`
- Platform: `mobile/android/app/google-services.json`, `mobile/ios/Runner/GoogleService-Info.plist`, `mobile/android/app/build.gradle.kts`, `mobile/android/build.gradle.kts`
- Test: `mobile/test/core/push/push_service_test.dart`

**Interfaces:**
- Produces:
  - `class PushService { Future<void> initialize(); Future<String?> registerForUser(String userId); Future<void> unregister(String userId); Stream<PushTap> get taps; }`
  - `class PushTap { final String? route; final Map<String, String> data; }`
  - `DeviceTokenRepository` (mobil): `Future<void> register({required String userId, required String token, required String platform, String? deviceName})` → `POST /api/notifications/device-tokens`; `Future<void> unregister(String token)` → `DELETE`.

- [ ] **Step 1: Firebase projesini hazırla (manuel, kod öncesi)**

Firebase Console → yeni proje → Android + iOS uygulaması ekle → `google-services.json` ve `GoogleService-Info.plist` indir → ilgili yollara koy. Service-account anahtarını (JSON) backend için `Push__ServiceAccountJson` ortam değişkenine koy, **repoya koyma**.
Expected: iki platform dosyası yerinde, `.gitignore`'a `google-services.json` ve `GoogleService-Info.plist` eklendi (sırlar).

- [ ] **Step 2: Paketleri ekle**

Run: `cd mobile && flutter pub add firebase_core`
(`firebase_messaging` ve `flutter_local_notifications` zaten `pubspec.yaml`'da — artık gerçekten kullanılacak, D-18 kapanır.)

- [ ] **Step 3: Test yaz (kırmızı)**

`mobile/test/core/push/push_service_test.dart` — `PushService`'i platform kanalına bağımlı olmayan bir arayüz arkasından test et:
```dart
test('registerForUser token alinca repository.register cagirir', () async {
  final repo = RecordingDeviceTokenRepository();
  final service = PushService(
    repository: repo,
    tokenProvider: () async => 'fcm-token-1',
    permissionRequester: () async => true,
  );

  final token = await service.registerForUser('user-1');

  expect(token, 'fcm-token-1');
  expect(repo.registered.single.token, 'fcm-token-1');
});

test('izin verilmezse token kaydedilmez', () async {
  final repo = RecordingDeviceTokenRepository();
  final service = PushService(
    repository: repo,
    tokenProvider: () async => 'fcm-token-1',
    permissionRequester: () async => false,
  );

  final token = await service.registerForUser('user-1');

  expect(token, isNull);
  expect(repo.registered, isEmpty);
});
```

- [ ] **Step 4: `PushService`'i yaz**

`mobile/lib/core/push/push_service.dart` — `tokenProvider` ve `permissionRequester` fonksiyon parametreleriyle test edilebilir; üretimde `FirebaseMessaging.instance.getToken` ve `requestPermission` bağlanır. `onTokenRefresh` aboneliği ile token yenilendiğinde tekrar `register` çağrılır. `FirebaseMessaging.onMessageOpenedApp` ve `getInitialMessage` `taps` akışına `PushTap(route: message.data['route'], data: ...)` basar.

- [ ] **Step 5: `main.dart` ve DI bağlantısı**

`main.dart`:
```dart
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp();
  FirebaseMessaging.onBackgroundMessage(_firebaseBackgroundHandler);
  await configureDependencies();
```
`_firebaseBackgroundHandler` üst düzey fonksiyon olarak tanımlanır (`@pragma('vm:entry-point')`).
`injector.dart` → `registerLazySingleton<DeviceTokenRepository>` + `registerLazySingleton<PushService>`.

- [ ] **Step 6: Oturum yaşam döngüsüne bağla**

`AuthCubit` giriş başarısında `injector<PushService>().registerForUser(session.userId)`, çıkışta `unregister(session.userId)` çağırır. Uygulama açılışında oturum restore edilirse de `registerForUser` çağrılır (token rotasyonu için).

- [ ] **Step 7: Bildirime dokunma → derin bağlantı**

`app.dart` içinde `PushService.taps` dinlenir; `tap.route` doluysa `router.push(tap.route!)`.

- [ ] **Step 8: Testler + gerçek cihaz doğrulaması**

Run: `cd mobile && flutter test && flutter analyze`
Ardından gerçek cihaz/emülatörde: giriş yap → backend'de `device_tokens` tablosunda satır oluştuğunu doğrula → backend'den test bildirimi gönder (`Push__Provider=Fcm` ile bir ders hatırlatması oluştur) → cihazda bildirim görün → dokun → doğru ekran açılsın.
Expected: bildirim geldi, dokunma doğru rotayı açtı.

- [ ] **Step 9: Commit**

```bash
git add mobile
git commit -m "feat(mobile): FCM push entegrasyonu + cihaz token kaydi (A-04)"
```

---

### Task 6: Mobil — Bildirim merkezi (rozet + okundu)

**Files:**
- Modify: `mobile/lib/features/notifications/domain/notification_contracts.dart`
- Modify: `mobile/lib/features/notifications/data/repositories/notification_repository_impl.dart`
- Create: `mobile/lib/features/notifications/data/models/user_notification_model.dart`
- Create: `mobile/lib/features/notifications/presentation/cubit/notification_center_cubit.dart`
- Modify: `mobile/lib/features/notifications/presentation/pages/notifications_page.dart`
- Modify: `mobile/lib/features/study/presentation/pages/student_home_page.dart` + öğretmen `dashboard_page.dart` (rozet)
- Test: `mobile/test/features/notifications/notification_center_cubit_test.dart`
- Modify: `doc/pages/notifications.md` (yoksa oluştur) + `doc/pages/00_pages_index.md`

**Interfaces:**
- Produces: `NotificationRepository.listUserNotifications({required String userId, bool onlyUnread, int skip, int take})`, `unreadCount(String userId)`, `markRead(String notificationId)`.
- `NotificationCenterCubit` durumları: `loading | loaded(items, unreadCount) | failure(message)`; `markRead(id)` iyimser günceller.

- [ ] **Step 1: Cubit testini yaz (kırmızı)** — `loaded` sonrası `markRead` çağrısında `unreadCount` bir azalmalı ve ilgili öğe `isRead: true` olmalı.
- [ ] **Step 2: Çalıştır, kırmızı gör** — Run: `cd mobile && flutter test test/features/notifications/notification_center_cubit_test.dart`
- [ ] **Step 3: Model + repository metotlarını yaz** (mevcut `notification_repository_impl.dart` desenine uy; **mock fallback ekleme** — A-05 kararı).
- [ ] **Step 4: Cubit'i yaz** ve testi yeşile al.
- [ ] **Step 5: `notifications_page.dart`'ı bildirim merkezine çevir** — sekmeler "Tümü / Okunmamış", öğe kartında ikon (kind'e göre), başlık, gövde, göreli zaman; dokununca `route` varsa oraya git + `markRead`. Boş durum: "Henüz bildirimin yok."
- [ ] **Step 6: Rozet** — öğretmen dashboard ve öğrenci ana ekranındaki bildirim ikonuna okunmamış sayısı rozeti (`unreadCount`).
- [ ] **Step 7: Testler** — Run: `cd mobile && flutter test && flutter analyze` → yeşil.
- [ ] **Step 8: Doküman + commit**

```bash
git add mobile doc/pages
git commit -m "feat(mobile): bildirim merkezi + okunmamis rozeti (D-11)"
```

---

### Task 7: Kapanış

- [ ] **Step 1: Tam test paketleri** — Run: `dotnet test EgitimUssu.slnx && cd mobile && flutter test` → başarısız 0.
- [ ] **Step 2: Uçtan uca senaryo** — Öğretmen hesabıyla yarın için ders planla → `lesson_reminders` satırı oluştu → `RemindAtUtc`'yi geçmişe çekip dispatcher turunu bekle → cihaza bildirim geldi → dokun → takvim açıldı.
- [ ] **Step 3: Dokümanlar**
  - `doc/modules/m11_notifications.md`: yeni endpoint'ler, `DeviceToken`/`UserNotification` domain, gerçek push akışı, kontrol listesi maddeleri `- [x]`.
  - `doc/modules/00_genel_bakis.md`: Notifications endpoint bloğu (2 → 7 endpoint).
  - `doc/modules/veri_modeli.md`: `device_tokens` + `user_notifications` ER.
  - `doc/architecture/backend.md`: "Push" başlığı.
  - `doc/denetim/2026-09-02_eksik_analizi.md`: A-04, M11-1..4, D-11, D-18 → `✅ (P03)`.
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "docs: P03 push bildirim kapanisi (A-04/M11-1..4/D-11)"
```

---

## Kabul Kriterleri

- [ ] `Push:Provider=Fcm` iken eksik konfigürasyonla uygulama açılmıyor
- [ ] Giriş yapan cihazın token'ı `device_tokens`'a yazılıyor; çıkışta pasifleşiyor
- [ ] Planlanan ders için gerçek cihaza bildirim ulaşıyor
- [ ] FCM `UNREGISTERED` yanıtında token otomatik pasifleşiyor
- [ ] Push başarısızsa hatırlatma "gönderildi" işaretlenmiyor, tekrar deneniyor, 5 denemede `Failed`
- [ ] Öğrenci/öğretmen/veli bildirim listesini görüyor; okunmamış rozeti doğru
- [ ] Bildirime dokunma ilgili ekranı açıyor
- [ ] `dotnet test EgitimUssu.slnx` ve `flutter test` yeşil
