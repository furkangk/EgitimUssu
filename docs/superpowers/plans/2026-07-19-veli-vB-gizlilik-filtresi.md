# Veli V-B — Gizlilik Filtresi (Öğrenci Paylaşım Ayarı) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Veli panelinin çocuğun bireysel çalışma verisini yalnızca öğrenci **paylaşıma izin verdiyse** göstermesi; izin yoksa alanların "Ayşe bu veriyi paylaşmıyor" işaretiyle dönmesi (değer sızmadan). Kişisel seans notu hiçbir koşulda dönmez (zaten dashboard'da yok — korunur).

**Architecture:** Settings modülü bugün iskelet (yalnız `UserSetting` aggregate + DbContext; Application/endpoint/event yok). Bu plan Settings'e **minimal okuma/yazma yüzeyi** ekler: `ShareStudyDataWithParent` toggle'ını set eden bir upsert komutu + `IStudentPrivacyDirectory` adında bir `Shared.Contracts` okuma arayüzü (Settings uygular, kayıt yoksa **paylaşım açık** varsayar). Parents dashboard'u, `KnownStudent` read-model'i ile `StudentId → UserId` çözer, bu arayüzü çağırır ve çalışma alanlarını `IsStudyShared=false` ise maskeler. **Karar (2026-07-19):** gizli alan "paylaşılmıyor" olarak şeffaf işaretlenir (değer 0/gizli döner).

**Tech Stack:** .NET 9, EF Core (`settings`/`parents` şemaları), CQRS, xUnit. Modüller-arası: `Shared.Contracts` okuma arayüzü deseni (`IStudentDirectory`/`IMembershipDirectory` ile aynı).

## Global Constraints
- Migration (Settings): `dotnet ef migrations add <Ad> --project src/Modules/Settings/Infrastructure --startup-project src/API.Host --context SettingsDbContext`
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Modüller-arası okuma: yeni arayüz `src/Shared/Contracts`'ta; Settings implementasyonu kendi Infrastructure'ında; DI kendi modülünde. Parents doğrudan Settings DB'sine erişmez.
- Gizlilik **veri katmanında** uygulanır (handler), arayüzde değil.

## File Structure
- `src/Shared/Contracts/StudentPrivacyContract.cs` *(yeni)* — `IStudentPrivacyDirectory` + `StudentPrivacy` record.
- `src/Modules/Settings/Application/*` *(yeni)* — `SetStudySharingCommand` + handler + repo arayüzü + validator/authorizer.
- `src/Modules/Settings/Infrastructure/StudentPrivacyDirectory.cs` *(yeni)* — arayüz implementasyonu.
- `src/Modules/Settings/Infrastructure/UserSettingRepository.cs` *(yeni)* + `SettingsDbContext.cs` (config zaten var) + `DependencyInjection.cs` (kayıtlar).
- `src/Modules/Settings/API/SettingsModule.cs` — upsert endpoint.
- `src/Modules/Settings/Domain/SettingsDomainModel.cs` — `SetStudySharing` mutator.
- `src/Modules/Parents/Application/ParentFeatures.cs` — dashboard handler filtre + `StudySummaryResponse.IsShared`.
- `src/Modules/Parents/Infrastructure/DependencyInjection.cs` — (değişiklik yok; directory Settings'te kayıtlı).
- Test: `tests/Unit/StudentPrivacyFilterTests.cs`, `tests/Unit/UserSettingTests.cs`.

---

### Task 1: Settings domain — `SetStudySharing` mutator

**Files:**
- Modify: `src/Modules/Settings/Domain/SettingsDomainModel.cs`
- Test: `tests/Unit/UserSettingTests.cs` *(yeni)*

**Interfaces:**
- Produces: `UserSetting.SetStudySharing(bool shareWithTeacher, bool shareWithParent, DateTime updatedOnUtc)` — ilgili iki bool + `LastUpdatedOnUtc` günceller.

- [ ] **Step 1: Write the failing test** — `tests/Unit/UserSettingTests.cs`:

```csharp
using EgitimUssu.Modules.Settings.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class UserSettingTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private static UserSetting New()
        => new(Guid.NewGuid(), Guid.NewGuid(),
            pushNotificationsEnabled: true, emailNotificationsEnabled: true,
            upcomingLessonReminderEnabled: true, homeworkReminderEnabled: true,
            paymentReminderEnabled: true, weeklySummaryEnabled: true,
            shareStudyDataWithTeacher: true, shareStudyDataWithParent: true,
            privacyLevel: PrivacyLevel.Standard,
            sessionTerminationPolicy: SessionTerminationPolicy.KeepLatest,
            lastUpdatedOnUtc: Now);

    [Fact]
    public void SetStudySharing_UpdatesFlagsAndTimestamp()
    {
        var s = New();
        var later = Now.AddMinutes(5);

        s.SetStudySharing(shareWithTeacher: false, shareWithParent: false, later);

        Assert.False(s.ShareStudyDataWithTeacher);
        Assert.False(s.ShareStudyDataWithParent);
        Assert.Equal(later, s.LastUpdatedOnUtc);
    }
}
```

> Not: `New()` içindeki ctor argüman adlarını/sırasını `SettingsDomainModel.cs`'teki gerçek ctor imzasıyla (12 parametre; sıra: userId, pushNotificationsEnabled, emailNotificationsEnabled, upcomingLessonReminderEnabled, homeworkReminderEnabled, paymentReminderEnabled, weeklySummaryEnabled, shareStudyDataWithTeacher, shareStudyDataWithParent, privacyLevel, sessionTerminationPolicy, lastUpdatedOnUtc) doğrula; farklıysa test çağrısını gerçek imzaya uyarla.

- [ ] **Step 2: Run → FAIL**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~UserSettingTests"`
Expected: FAIL — `UserSetting` `SetStudySharing` tanımı yok.

- [ ] **Step 3: Add the mutator** — `SettingsDomainModel.cs`, `LastUpdatedOnUtc` property'sinden sonra, sınıf içinde ekle:

```csharp
    public void SetStudySharing(bool shareWithTeacher, bool shareWithParent, DateTime updatedOnUtc)
    {
        ShareStudyDataWithTeacher = shareWithTeacher;
        ShareStudyDataWithParent = shareWithParent;
        LastUpdatedOnUtc = updatedOnUtc;
    }
```

- [ ] **Step 4: Run → PASS**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~UserSettingTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Settings/Domain/SettingsDomainModel.cs tests/Unit/UserSettingTests.cs
git commit -m "feat(settings): UserSetting.SetStudySharing mutator (Veli V-B)"
```

---

### Task 2: `Shared.Contracts` privacy arayüzü + Settings implementasyonu

**Files:**
- Create: `src/Shared/Contracts/StudentPrivacyContract.cs`
- Create: `src/Modules/Settings/Infrastructure/UserSettingRepository.cs`
- Create: `src/Modules/Settings/Infrastructure/StudentPrivacyDirectory.cs`
- Modify: `src/Modules/Settings/Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces: `IStudentPrivacyDirectory.GetForUserAsync(Guid userId, CancellationToken) → StudentPrivacy`; `record StudentPrivacy(bool ShareStudyDataWithParent, bool ShareStudyDataWithTeacher)`. Kayıt yoksa `new StudentPrivacy(true, true)` (paylaşım açık varsayımı).
- Produces: `IUserSettingRepository.GetByUserIdAsync` / `AddAsync` / `SaveChangesAsync`.

- [ ] **Step 1: Contract** — `src/Shared/Contracts/StudentPrivacyContract.cs`:

```csharp
namespace EgitimUssu.Shared.Contracts;

// Modüller-arası salt-okunur gizlilik sözleşmesi. Settings uygular; Parents (ve ileride başka modüller)
// öğrencinin bireysel çalışma verisini paylaşıp paylaşmadığını okumak için tüketir.
public sealed record StudentPrivacy(bool ShareStudyDataWithParent, bool ShareStudyDataWithTeacher);

public interface IStudentPrivacyDirectory
{
    // userId: öğrencinin login kullanıcı kimliği. Ayar kaydı yoksa paylaşım AÇIK varsayılır.
    Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Repository** — `src/Modules/Settings/Infrastructure/UserSettingRepository.cs`:

```csharp
using EgitimUssu.Modules.Settings.Application;
using EgitimUssu.Modules.Settings.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

internal sealed class UserSettingRepository : IUserSettingRepository
{
    private readonly SettingsDbContext _dbContext;

    public UserSettingRepository(SettingsDbContext dbContext) => _dbContext = dbContext;

    public Task<UserSetting?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public Task AddAsync(UserSetting setting, CancellationToken cancellationToken)
        => _dbContext.UserSettings.AddAsync(setting, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
```

- [ ] **Step 3: Directory implementasyonu** — `src/Modules/Settings/Infrastructure/StudentPrivacyDirectory.cs`:

```csharp
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

internal sealed class StudentPrivacyDirectory : IStudentPrivacyDirectory
{
    private readonly SettingsDbContext _dbContext;

    public StudentPrivacyDirectory(SettingsDbContext dbContext) => _dbContext = dbContext;

    public async Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => new { s.ShareStudyDataWithParent, s.ShareStudyDataWithTeacher })
            .FirstOrDefaultAsync(cancellationToken);

        // Kayıt yoksa paylaşım açık varsayılır (öğrenci henüz kısıtlamadı).
        return row is null
            ? new StudentPrivacy(true, true)
            : new StudentPrivacy(row.ShareStudyDataWithParent, row.ShareStudyDataWithTeacher);
    }
}
```

- [ ] **Step 4: DI** — `src/Modules/Settings/Infrastructure/DependencyInjection.cs`, `AddModuleDbContext` satırından sonra ekle (ayrıca `using EgitimUssu.Shared.Contracts;`, `using EgitimUssu.Modules.Settings.Application;`, `using EgitimUssu.Shared.Application;`, `using EgitimUssu.Shared.Kernel;` gerekli):

```csharp
        services.AddScoped<IUserSettingRepository, UserSettingRepository>();
        services.AddScoped<IStudentPrivacyDirectory, StudentPrivacyDirectory>();
```

- [ ] **Step 5: Build**

Run: `dotnet build EgitimUssu.slnx`
Expected: `0 Hata` (henüz `IUserSettingRepository` arayüzü Application'da tanımlanmadı → Task 3'te; bu adımda repo dosyası derlenmezse Task 3 ile birlikte derle). Bu iki task birlikte commit'lenir (Step sonu Task 3'te).

---

### Task 3: Settings upsert komutu + endpoint

**Files:**
- Create: `src/Modules/Settings/Application/SettingsFeatures.cs` (`IUserSettingRepository`, `SetStudySharingCommand`, response, handler, validator, authorizer)
- Modify: `src/Modules/Settings/Infrastructure/DependencyInjection.cs` (handler/validator/authorizer kaydı)
- Modify: `src/Modules/Settings/API/SettingsModule.cs` (endpoint)

**Interfaces:**
- Produces: `SetStudySharingCommand(Guid UserId, bool ShareWithTeacher, bool ShareWithParent) : ICommand<Result<StudySharingResponse>>`; upsert (kayıt yoksa makul varsayılanlarla oluşturur, sonra `SetStudySharing`).

- [ ] **Step 1: Application** — `src/Modules/Settings/Application/SettingsFeatures.cs`:

```csharp
using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Settings.Application;

public sealed record SetStudySharingCommand(Guid UserId, bool ShareWithTeacher, bool ShareWithParent)
    : ICommand<Result<StudySharingResponse>>;

public sealed record StudySharingResponse(Guid UserId, bool ShareWithTeacher, bool ShareWithParent);

public interface IUserSettingRepository
{
    Task<UserSetting?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(UserSetting setting, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class SetStudySharingCommandValidator : ICommandValidator<SetStudySharingCommand>
{
    private static readonly Error Invalid = new("settings.invalid_request", "Ayar bilgileri eksik veya hatalı.");

    public Task<Result> Validate(SetStudySharingCommand command, CancellationToken cancellationToken)
        => Task.FromResult(command.UserId == Guid.Empty ? Result.Failure(Invalid) : Result.Success());
}

public sealed class SetStudySharingCommandHandler : ICommandHandler<SetStudySharingCommand, Result<StudySharingResponse>>
{
    private readonly IUserSettingRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public SetStudySharingCommandHandler(IUserSettingRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudySharingResponse>> Handle(SetStudySharingCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var setting = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (setting is null)
        {
            setting = new UserSetting(
                _idGenerator.New(), command.UserId,
                pushNotificationsEnabled: true, emailNotificationsEnabled: true,
                upcomingLessonReminderEnabled: true, homeworkReminderEnabled: true,
                paymentReminderEnabled: true, weeklySummaryEnabled: true,
                shareStudyDataWithTeacher: command.ShareWithTeacher,
                shareStudyDataWithParent: command.ShareWithParent,
                privacyLevel: PrivacyLevel.Standard,
                sessionTerminationPolicy: SessionTerminationPolicy.KeepLatest,
                lastUpdatedOnUtc: now);
            await _repository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.SetStudySharing(command.ShareWithTeacher, command.ShareWithParent, now);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudySharingResponse>.Success(
            new StudySharingResponse(command.UserId, command.ShareWithTeacher, command.ShareWithParent));
    }
}

// Yalnızca kullanıcının kendisi (veya Admin) kendi ayarını değiştirebilir.
public sealed class SettingsAuthorizer : ICommandAuthorizer<SetStudySharingCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ayarı değiştirme yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public SettingsAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(SetStudySharingCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Task.FromResult(Result.Failure(Forbidden));
        if (_currentUser.Roles.Contains("Admin")) return Task.FromResult(Result.Success());
        return Task.FromResult(
            Guid.TryParse(_currentUser.UserId, out var uid) && uid == command.UserId
                ? Result.Success() : Result.Failure(Forbidden));
    }
}
```

- [ ] **Step 2: DI** — `DependencyInjection.cs`'e ekle:

```csharp
        services.AddScoped<ICommandHandler<SetStudySharingCommand, Result<StudySharingResponse>>, SetStudySharingCommandHandler>();
        services.AddScoped<ICommandValidator<SetStudySharingCommand>, SetStudySharingCommandValidator>();
        services.AddScoped<ICommandAuthorizer<SetStudySharingCommand>, SettingsAuthorizer>();
```

- [ ] **Step 3: Endpoint** — `SettingsModule.cs` `MapEndpoints` içine ekle (mevcut `/status` yanına), ve request DTO + handler metodu:

```csharp
        group.MapPut("/users/{userId:guid}/study-sharing", SetStudySharingAsync)
            .WithSummary("Öğrencinin bireysel çalışma verisini öğretmen/veli ile paylaşımını ayarlar");
```

Handler metodu + DTO (dosyanın altına, mevcut `SettingsModule` desenine göre — `ICommandDispatcher`, `ToHttpResult` gerekiyorsa Scheduling/Parents `ToHttpResult` deseninden kopyala; `settings.invalid_request`→400, `shared.forbidden`→403):

```csharp
    private static async Task<IResult> SetStudySharingAsync(
        HttpContext context, Guid userId, SetStudySharingRequest request,
        ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new SetStudySharingCommand(userId, request.ShareWithTeacher, request.ShareWithParent),
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.Code switch
            {
                "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
                _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
            };
    }

    public sealed record SetStudySharingRequest(bool ShareWithTeacher, bool ShareWithParent);
```

> Not: `SettingsModule.cs` şu an yalnız `/status` içeriyor; gerekli `using`'leri (Scheduling/Parents modüllerindeki `using` bloğunu örnek al: `EgitimUssu.Shared.Application`, `EgitimUssu.Shared.Infrastructure.Http`, `Microsoft.AspNetCore.Http`, vb.) ekle.

- [ ] **Step 4: Build + migration (yok) + test**

Run: `dotnet build EgitimUssu.slnx`
Expected: `0 Hata`. (Şema değişikliği yok — `UserSetting` alanları zaten var; migration gerekmez.)
Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: tüm testler PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Shared/Contracts/StudentPrivacyContract.cs src/Modules/Settings/
git commit -m "feat(settings): çalışma paylaşımı ayarı + IStudentPrivacyDirectory (Veli V-B)"
```

---

### Task 4: Parents dashboard gizlilik filtresi

**Files:**
- Modify: `src/Modules/Parents/Application/ParentFeatures.cs` (`GetChildDashboardQueryHandler`, `StudySummaryResponse`, `ToDashboard`)
- Test: `tests/Unit/StudentPrivacyFilterTests.cs` *(yeni)*

**Interfaces:**
- Consumes: `IStudentPrivacyDirectory` (Task 2), `IParentRepository.GetKnownStudentAsync` (mevcut → `KnownStudent.UserId`).
- Produces: `StudySummaryResponse(int WeeklyStudyMinutes, int StreakDays, bool HasData, bool IsShared)` — yeni `IsShared` alanı. `IsShared=false` ise `WeeklyStudyMinutes=0, StreakDays=0, HasData=false`.

- [ ] **Step 1: Write the failing test** — `tests/Unit/StudentPrivacyFilterTests.cs`. Handler'ı sahte repo + sahte directory ile kur:

```csharp
using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentPrivacyFilterTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Dashboard_WhenNotShared_MasksStudyFields()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();

        var link = new ParentChildLink(Guid.NewGuid(), parentUserId, studentId, "Ayşe", "anne", null, true, Now);
        link.Approve(Guid.NewGuid(), Now);
        var snapshot = new ChildProgressSnapshot(Guid.NewGuid(), studentId, Now);
        // snapshot çalışma alanlarını doldurmak için mevcut mutator yoksa 0 kalır; test IsShared davranışını doğrular.

        var repo = new FakeParentRepository(link, snapshot, studentUserId);
        var privacy = new FakePrivacyDirectory(new StudentPrivacy(ShareStudyDataWithParent: false, ShareStudyDataWithTeacher: true));
        var handler = new GetChildDashboardQueryHandler(repo, privacy);

        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Study.IsShared);
        Assert.Equal(0, result.Value.Study.WeeklyStudyMinutes);
    }

    [Fact]
    public async Task Dashboard_WhenShared_MarksIsSharedTrue()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = new ParentChildLink(Guid.NewGuid(), parentUserId, studentId, "Ayşe", "anne", null, true, Now);
        link.Approve(Guid.NewGuid(), Now);
        var repo = new FakeParentRepository(link, new ChildProgressSnapshot(Guid.NewGuid(), studentId, Now), Guid.NewGuid());
        var privacy = new FakePrivacyDirectory(new StudentPrivacy(true, true));
        var handler = new GetChildDashboardQueryHandler(repo, privacy);

        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.True(result.Value.Study.IsShared);
    }

    private sealed class FakePrivacyDirectory : IStudentPrivacyDirectory
    {
        private readonly StudentPrivacy _value;
        public FakePrivacyDirectory(StudentPrivacy value) => _value = value;
        public Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(_value);
    }

    private sealed class FakeParentRepository : IParentRepository
    {
        private readonly ParentChildLink _link;
        private readonly ChildProgressSnapshot _snapshot;
        private readonly Guid _studentUserId;
        public FakeParentRepository(ParentChildLink link, ChildProgressSnapshot snapshot, Guid studentUserId)
        { _link = link; _snapshot = snapshot; _studentUserId = studentUserId; }

        public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(_link);
        public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken ct) => Task.FromResult<ChildProgressSnapshot?>(_snapshot);
        public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult<KnownStudent?>(new KnownStudent(Guid.NewGuid(), studentId, _studentUserId, Now));
        public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<ParentProfile?>(null);
        public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(new[] { _link });
        public Task AddProfileAsync(ParentProfile profile, CancellationToken ct) => Task.CompletedTask;
        public Task AddLinkAsync(ParentChildLink link, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
```

> Not: `IParentRepository`'nin tüm üyelerini sahtelerken gerçek arayüzle (ParentFeatures.cs) birebir imza kullan; eksik/yeni üye varsa test sahtesine ekle.

- [ ] **Step 2: Run → FAIL**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~StudentPrivacyFilterTests"`
Expected: FAIL — `GetChildDashboardQueryHandler` iki argümanlı ctor yok; `StudySummaryResponse.IsShared` yok.

- [ ] **Step 3: Add `IsShared` to response** — `ParentFeatures.cs`, `StudySummaryResponse`'u değiştir:

```csharp
public sealed record StudySummaryResponse(int WeeklyStudyMinutes, int StreakDays, bool HasData, bool IsShared);
```

- [ ] **Step 4: Inject directory + apply filter** — `GetChildDashboardQueryHandler`'ı değiştir: ctor'a `IStudentPrivacyDirectory` ekle; `Handle` içinde snapshot alındıktan sonra öğrencinin UserId'sini `GetKnownStudentAsync` ile çöz, `GetForUserAsync` çağır, `ToDashboard`'a `isStudyShared` geçir.

```csharp
public sealed class GetChildDashboardQueryHandler : IQueryHandler<GetChildDashboardQuery, Result<ChildDashboardResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IStudentPrivacyDirectory _privacy;

    public GetChildDashboardQueryHandler(IParentRepository repository, IStudentPrivacyDirectory privacy)
    {
        _repository = repository;
        _privacy = privacy;
    }

    public async Task<Result<ChildDashboardResponse>> Handle(GetChildDashboardQuery query, CancellationToken cancellationToken)
    {
        var link = await _repository.GetActiveLinkAsync(query.ParentUserId, query.StudentId, cancellationToken);
        if (link is null || !link.IsApproved)
        {
            return Result<ChildDashboardResponse>.Failure(ParentErrors.LinkNotApproved);
        }

        var isStudyShared = true;
        var known = await _repository.GetKnownStudentAsync(query.StudentId, cancellationToken);
        if (known?.UserId is { } studentUserId)
        {
            var privacy = await _privacy.GetForUserAsync(studentUserId, cancellationToken);
            isStudyShared = privacy.ShareStudyDataWithParent;
        }

        var snapshot = await _repository.GetSnapshotAsync(query.StudentId, cancellationToken);
        return Result<ChildDashboardResponse>.Success(snapshot.ToDashboard(query.StudentId, link, isStudyShared));
    }
}
```

- [ ] **Step 5: Apply mask in mapping** — `ToDashboard`'a `bool isStudyShared` parametresi ekle; `StudySummaryResponse` üretimini maskele. `snapshot is null` dalında `new StudySummaryResponse(0, 0, false, isStudyShared)`; dolu dalda:

```csharp
    public static ChildDashboardResponse ToDashboard(this ChildProgressSnapshot? snapshot, Guid studentId, ParentChildLink link, bool isStudyShared)
    {
        if (snapshot is null)
        {
            return new ChildDashboardResponse(
                studentId, link.ChildDisplayName, link.Status.ToString(),
                new StudySummaryResponse(0, 0, false, isStudyShared),
                new LessonSummaryResponse(0, 0, null),
                new AssignmentSummaryResponse(0, 0, 0),
                new PaymentSummaryResponse("TRY", 0m, 0m, 0m, null),
                null);
        }

        var weeklyMinutes = isStudyShared ? snapshot.WeeklyStudyMinutes : 0;
        var streakDays = isStudyShared ? snapshot.StudyStreakDays : 0;
        var hasStudyData = isStudyShared && (snapshot.WeeklyStudyMinutes > 0 || snapshot.StudyStreakDays > 0);
        return new ChildDashboardResponse(
            studentId, link.ChildDisplayName, link.Status.ToString(),
            new StudySummaryResponse(weeklyMinutes, streakDays, hasStudyData, isStudyShared),
            new LessonSummaryResponse(snapshot.CompletedLessonCount, snapshot.PlannedLessonCount, snapshot.LastLessonCompletedAtUtc),
            new AssignmentSummaryResponse(snapshot.TotalAssignmentCount, snapshot.OpenAssignmentCount, snapshot.CompletedAssignmentCount),
            new PaymentSummaryResponse(snapshot.Currency, snapshot.ExpectedPaymentTotal, snapshot.CollectedPaymentTotal, snapshot.OutstandingPaymentTotal, snapshot.LastPaymentUpdatedAtUtc),
            snapshot.UpdatedOnUtc);
    }
```

> Not: `ListChildrenQueryHandler` içindeki `ToProgressSummary` çağrısı çalışma verisi taşımıyor (yalnız ders/ödev/ödeme sayıları); gizlilik yalnız dashboard'daki çalışma alanlarına uygulanır — `ToProgressSummary` değişmez.

- [ ] **Step 6: DI güncelle** — `GetChildDashboardQueryHandler` artık `IStudentPrivacyDirectory` alıyor; bu Settings'te kayıtlı (Task 2 Step 4), ek Parents DI kaydı gerekmez. Yalnız derleme doğrula.

Run: `dotnet build EgitimUssu.slnx`
Expected: `0 Hata`.

- [ ] **Step 7: Run tests → PASS**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: tüm testler PASS (yeni privacy testleri dahil).

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Parents/ tests/Unit/StudentPrivacyFilterTests.cs
git commit -m "feat(parents): dashboard gizlilik filtresi (çalışma verisi paylaşımı) (Veli V-B)"
```

---

### Task 5: Dokümantasyon
- [ ] `doc/modules/m09_parents.md`: dashboard gizlilik filtresi (`ShareStudyDataWithParent` → çalışma alanları maskelenir, `IsShared`); değişmez kural "seans notu asla dönmez" vurgusu. Yeni Settings endpoint'i `PUT /api/settings/users/{id}/study-sharing`. Tarih 2026-07-19.
- [ ] `doc/modules/00_genel_bakis.md`: Settings endpoint envanterine `PUT /users/{userId}/study-sharing` ekle; Settings durumunu 🟡 (artık gerçek endpoint + directory var) güncelle.
- [ ] `doc/modules/veri_modeli.md`: `IStudentPrivacyDirectory` sözleşmesini modüller-arası kontrat listesine ekle.
- [ ] `doc/roles/veli.md`: gizlilik davranışı (paylaşılmıyor işareti) satırı.
- [ ] commit: `docs: veli gizlilik filtresi + settings paylaşım ayarı (Veli V-B)`

## Self-Review
- **Spec coverage:** Spec V-B "dashboard ShareStudyDataWithParent'e uyar; gizli alan 'paylaşılmıyor' işaretiyle döner; seans notu sızmaz" → Task 4 karşılıyor. Settings'in yazma/okuma yüzeyi olmadığı keşfi → Task 1-3 ön-koşul olarak eklendi (spec'te "Settings kontrat/okuma" notu vardı).
- **Placeholder:** Endpoint `using`/`ToHttpResult` ayrıntısı örnek desene yönlendiriliyor (SettingsModule şu an iskelet); geri kalan tüm kod kesindir.
- **Type consistency:** `StudySummaryResponse` dört alanlı; `ToDashboard` yeni `bool isStudyShared` parametresi tüm çağrı yerlerinde güncellenir (yalnız `GetChildDashboardQueryHandler`).
- **Karar izi:** Gizli alan **şeffaf** (0 + `IsShared=false`) döner — kullanıcı kararı 2026-07-19.
