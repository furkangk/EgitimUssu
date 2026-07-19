# Veli V-D — Öğretmen→Veli Davet Kodu + Claim Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Öğretmenin bir öğrenci için **veli davet kodu** üretebilmesi; velinin kaydolup bu kodu girerek çocuğuna bağlanabilmesi ("veli onayı ile" — kodu girmek onay eylemidir). Öğretmenin Faz-1'de girdiği ölü veli bilgisi sorununu, öğrenci claim deseninin veli karşılığıyla çözer.

**Architecture:** Öğrenci `TeacherStudentLink` davet/claim deseninin (InviteCode + `GenerateInviteCode()` + `ClaimStudentLinkCommand` + `/links/claim`) veli tarafına birebir uyarlanması. **Karar (2026-07-19):** telefon eşleştirme YOK (Identity'de telefonla arama yok); öğretmen kod üretir, veli kodu girerek claim eder. Yeni `StudentParentInvite` aggregate'i **Students** modülünde (öğretmen orada öğrenci sahibi); Parents tarafındaki claim, yeni `IParentInviteDirectory` (`Shared.Contracts`) ile daveti çözer, `ParentChildLink` oluşturup **onaylar**; mevcut `ParentChildLinkApprovedIntegrationEventHandler` (Students) `StudentProfile.ParentUserId`'yi zaten back-fill eder. **Bağımlılık:** V-C (Approve üç-argümanlı imza + birincil veli).

**Tech Stack:** .NET 9, EF Core (`students`/`parents` şemaları), CQRS, xUnit. Cross-module: `Shared.Contracts` okuma+işaretleme arayüzü.

## Global Constraints
- Migration (Students): `dotnet ef migrations add AddStudentParentInvites --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Reuse: `TeacherStudentLink.GenerateInviteCode()` deseni (6 haneli); mevcut `ParentChildLinkApprovedIntegrationEventHandler` (back-fill).

## File Structure
- `src/Modules/Students/Domain/StudentsDomainModel.cs` — `StudentParentInvite` aggregate + `enum ParentInviteStatus`.
- `src/Modules/Students/Application/StudentParentInviteFeatures.cs` *(yeni)* — `CreateParentInviteCommand` + handler + repo arayüzü + response + authorizer.
- `src/Modules/Students/Infrastructure/StudentParentInviteRepository.cs` *(yeni)* + `StudentsDbContext.cs` (DbSet+config) + `ParentInviteDirectory.cs` (`IParentInviteDirectory` impl) + DI + migration.
- `src/Shared/Contracts/ParentInviteContract.cs` *(yeni)* — `IParentInviteDirectory` + `ParentInviteInfo` record.
- `src/Modules/Students/API/StudentsModule.cs` — öğretmen endpoint'i.
- `src/Modules/Parents/Application/ParentFeatures.cs` — `ClaimParentInviteCommand` + handler + authorizer.
- `src/Modules/Parents/API/ParentsModule.cs` — veli claim endpoint'i + hata eşlemesi.
- Test: `tests/Unit/StudentParentInviteTests.cs`, `tests/Unit/ClaimParentInviteTests.cs`.

---

### Task 1: Students domain — `StudentParentInvite`

**Files:**
- Modify: `src/Modules/Students/Domain/StudentsDomainModel.cs`
- Test: `tests/Unit/StudentParentInviteTests.cs` *(yeni)*

**Interfaces:**
- Produces: `StudentParentInvite : AggregateRoot<Guid>` (`Id, StudentId, TeacherUserId, InviteCode, ChildDisplayName?, Status, ClaimedByParentUserId?, CreatedOnUtc, ClaimedOnUtc?`). `enum ParentInviteStatus { Pending=1, Claimed=2 }`. `Claim(Guid parentUserId, DateTime now)` — yalnız Pending'den; aksi `InvalidOperationException`. Kod üretimi `TeacherStudentLink.GenerateInviteCode()` ile handler'da.

- [ ] **Step 1: Write the failing test** — `tests/Unit/StudentParentInviteTests.cs`:

```csharp
using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentParentInviteTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Claim_SetsStatusAndParent()
    {
        var invite = new StudentParentInvite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "123456", "Ayşe", Now);
        var parentUserId = Guid.NewGuid();

        invite.Claim(parentUserId, Now.AddMinutes(1));

        Assert.Equal(ParentInviteStatus.Claimed, invite.Status);
        Assert.Equal(parentUserId, invite.ClaimedByParentUserId);
    }

    [Fact]
    public void Claim_Twice_Throws()
    {
        var invite = new StudentParentInvite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "123456", null, Now);
        invite.Claim(Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => invite.Claim(Guid.NewGuid(), Now));
    }
}
```

- [ ] **Step 2: Run → FAIL** (`dotnet test ... --filter StudentParentInviteTests`).

- [ ] **Step 3: Implement aggregate** — `StudentsDomainModel.cs` sonuna ekle:

```csharp
/// <summary>
/// Öğretmenin bir öğrenci için ürettiği veli davet kodu (Veli V-D). Veli kodu girerek claim eder ("veli onayı");
/// claim, Parents tarafında ParentChildLink oluşturup onaylar. Durum: Pending → Claimed.
/// </summary>
public sealed class StudentParentInvite : AggregateRoot<Guid>
{
    private StudentParentInvite() { }

    public StudentParentInvite(Guid id, Guid studentId, Guid teacherUserId, string inviteCode, string? childDisplayName, DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        TeacherUserId = teacherUserId;
        InviteCode = inviteCode;
        ChildDisplayName = childDisplayName?.Trim();
        Status = ParentInviteStatus.Pending;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid StudentId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public string InviteCode { get; private set; } = string.Empty;
    public string? ChildDisplayName { get; private set; }
    public ParentInviteStatus Status { get; private set; }
    public Guid? ClaimedByParentUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ClaimedOnUtc { get; private set; }

    public void Claim(Guid parentUserId, DateTime nowUtc)
    {
        if (Status != ParentInviteStatus.Pending)
        {
            throw new InvalidOperationException("Davet zaten kullanılmış.");
        }

        Status = ParentInviteStatus.Claimed;
        ClaimedByParentUserId = parentUserId;
        ClaimedOnUtc = nowUtc;
    }
}

public enum ParentInviteStatus
{
    Pending = 1,
    Claimed = 2
}
```

- [ ] **Step 4: Run → PASS** ; **Commit** `feat(students): veli davet kodu domaini (Veli V-D)`.

---

### Task 2: Students application + infra + öğretmen endpoint + migration

**Files:** `StudentParentInviteFeatures.cs` (yeni), `StudentParentInviteRepository.cs` (yeni), `ParentInviteDirectory.cs` (yeni), `src/Shared/Contracts/ParentInviteContract.cs` (yeni), `StudentsDbContext.cs`, `StudentsModule.cs`, `DependencyInjection.cs`, migration.

**Interfaces:**
- Produces: `CreateParentInviteCommand(Guid StudentId, Guid TeacherUserId, string? ChildDisplayName) → Result<ParentInviteResponse>` (`ParentInviteResponse(Guid Id, string InviteCode)`).
- Produces: `IParentInviteDirectory.ResolveAsync(string code, CancellationToken) → ParentInviteInfo?` (`record ParentInviteInfo(Guid InviteId, Guid StudentId, string? ChildDisplayName)`); `MarkClaimedAsync(Guid inviteId, Guid parentUserId, CancellationToken)`.
- Produces: `IStudentParentInviteRepository` (Add/GetByCode/GetById/SaveChanges).

- [ ] **Step 1: Shared contract** — `src/Shared/Contracts/ParentInviteContract.cs`:

```csharp
namespace EgitimUssu.Shared.Contracts;

public sealed record ParentInviteInfo(Guid InviteId, Guid StudentId, string? ChildDisplayName);

// Students uygular; Parents tüketir. Veli, öğretmenin ürettiği kodu girerek çocuğuna bağlanır.
public interface IParentInviteDirectory
{
    Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken cancellationToken);
    Task MarkClaimedAsync(Guid inviteId, Guid parentUserId, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Application** — `StudentParentInviteFeatures.cs`: `IStudentParentInviteRepository` (`Task<StudentParentInvite?> GetByInviteCodeAsync(string code, …)`, `GetByIdAsync`, `AddAsync`, `SaveChangesAsync`), `CreateParentInviteCommand` + `ParentInviteResponse` + handler (öğrenciyi `IStudentProfileRepository.GetByIdAsync` ile doğrula → yoksa `students.student_not_found`; `TeacherStudentLink.GenerateInviteCode()` ile kod üret; kaydet), validator (StudentId/TeacherUserId boş değil), authorizer (öğretmen = `TeacherUserId`, mevcut `LessonScheduleCommandAuthorizer`/`TeacherStudentLink` authorizer desenindeki `CanManageTeacher`). Tam kod, `TeacherStudentLinkFeatures.cs`'teki `InviteStudentCommand` handler + authorizer desenini izler.

- [ ] **Step 3: Infra** — `StudentParentInviteRepository.cs` (EF impl), `ParentInviteDirectory.cs`:

```csharp
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

internal sealed class ParentInviteDirectory : IParentInviteDirectory
{
    private readonly StudentsDbContext _dbContext;
    public ParentInviteDirectory(StudentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var invite = await _dbContext.StudentParentInvites
            .FirstOrDefaultAsync(i => i.InviteCode == inviteCode && i.Status == ParentInviteStatus.Pending, cancellationToken);
        return invite is null ? null : new ParentInviteInfo(invite.Id, invite.StudentId, invite.ChildDisplayName);
    }

    public async Task MarkClaimedAsync(Guid inviteId, Guid parentUserId, CancellationToken cancellationToken)
    {
        var invite = await _dbContext.StudentParentInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        if (invite is null) return;
        invite.Claim(parentUserId, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

> Not: `DateTime.UtcNow` yerine mevcut `IClock` enjeksiyonu tercih edilir; `ParentInviteDirectory`'ye `IClock` ekleyip `_clock.UtcNow` kullan (kod tabanı `IClock` konvansiyonu — `DateTime.UtcNow` doğrudan kullanımından kaçın).

`StudentsDbContext.cs`: `DbSet<StudentParentInvite> StudentParentInvites`; `StudentParentInviteConfiguration` (table `student_parent_invites`, `InviteCode` maxlen 8 + index, `Status` string maxlen 16, `ChildDisplayName` maxlen 200, index `{TeacherUserId}`, index `{StudentId}`).

`DependencyInjection.cs`: `IStudentParentInviteRepository`, `CreateParentInviteCommand` handler/validator/authorizer, `IParentInviteDirectory → ParentInviteDirectory`.

- [ ] **Step 4: Öğretmen endpoint** — `StudentsModule.cs`: `POST /students/{studentId:guid}/parent-invite` → `CreateParentInviteRequest(Guid TeacherUserId, string? ChildDisplayName)` → dispatch `CreateParentInviteCommand`. Yanıt `ParentInviteResponse` (kod). Hata: `students.student_not_found`→404 (mevcut `ToHttpResult`'a ekle), `shared.forbidden`→403.

- [ ] **Step 5: Build + migration + test**

Run: `dotnet build EgitimUssu.slnx`
Run: `dotnet ef migrations add AddStudentParentInvites --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: 0 hata; `student_parent_invites` tablosu; testler PASS.

- [ ] **Step 6: Commit** `feat(students): veli davet kodu üretimi + IParentInviteDirectory + migration (Veli V-D)`.

---

### Task 3: Parents claim komutu + endpoint

**Files:** `ParentFeatures.cs`, `ParentsModule.cs`, `DependencyInjection.cs` (Parents), Test: `tests/Unit/ClaimParentInviteTests.cs`.

**Interfaces:**
- Consumes: `IParentInviteDirectory` (Task 2), `IParentRepository` (+ V-C `ListApprovedLinksForStudentAsync`, `Approve(approvedByUserId, existingPrimary, now)`).
- Produces: `ClaimParentInviteCommand(Guid ParentUserId, string InviteCode) → Result<ChildLinkResponse>`. Yeni hata `parents.invite_not_found`.

- [ ] **Step 1: Write the failing test** — `tests/Unit/ClaimParentInviteTests.cs`: sahte `IParentInviteDirectory` (kod→`ParentInviteInfo`) + sahte `IParentRepository`; claim sonucu `ParentChildLink` Approved + `IsPrimaryContact` doğrula, event yayıldığını doğrula. (Sahteler gerçek arayüz imzalarına uyar.)

```csharp
[Fact]
public async Task Claim_ValidCode_CreatesApprovedPrimaryLink()
{
    var parentUserId = Guid.NewGuid();
    var studentId = Guid.NewGuid();
    var directory = new FakeInviteDirectory(new ParentInviteInfo(Guid.NewGuid(), studentId, "Ayşe"));
    var repo = new FakeRepo(); // no existing link/primary
    var handler = new ClaimParentInviteCommandHandler(repo, directory, new FixedClock(Now), new SeqIdGen());

    var result = await handler.Handle(new ClaimParentInviteCommand(parentUserId, "123456"), default);

    Assert.True(result.IsSuccess);
    Assert.Equal("Approved", result.Value.Status);
    Assert.True(result.Value.IsPrimaryContact);
    Assert.True(directory.Claimed);
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Handler** — `ParentFeatures.cs`:

```csharp
public sealed record ClaimParentInviteCommand(Guid ParentUserId, string InviteCode) : ICommand<Result<ChildLinkResponse>>;

public sealed class ClaimParentInviteCommandHandler : ICommandHandler<ClaimParentInviteCommand, Result<ChildLinkResponse>>
{
    private readonly IParentRepository _repository;
    private readonly IParentInviteDirectory _inviteDirectory;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    public ClaimParentInviteCommandHandler(IParentRepository repository, IParentInviteDirectory inviteDirectory, IClock clock, IIdGenerator idGenerator)
    {
        _repository = repository;
        _inviteDirectory = inviteDirectory;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public async Task<Result<ChildLinkResponse>> Handle(ClaimParentInviteCommand command, CancellationToken cancellationToken)
    {
        var info = await _inviteDirectory.ResolveAsync(command.InviteCode.Trim(), cancellationToken);
        if (info is null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.InviteNotFound);
        }

        var existing = await _repository.GetActiveLinkAsync(command.ParentUserId, info.StudentId, cancellationToken);
        if (existing is not null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkAlreadyExists);
        }

        var now = _clock.UtcNow;
        // Bu çocuğa halihazırda birincil veli var mı? (V-C birincil tekilliği)
        var approved = await _repository.ListApprovedLinksForStudentAsync(info.StudentId, cancellationToken);
        var existingPrimary = approved.FirstOrDefault(l => l.IsPrimaryContact);
        var isPrimary = existingPrimary is null; // ilk veli birincil olur; ikinci veli birincil olmaz

        var link = new ParentChildLink(_idGenerator.New(), command.ParentUserId, info.StudentId, info.ChildDisplayName, null, command.InviteCode.Trim(), isPrimary, now);
        await _repository.AddLinkAsync(link, cancellationToken);
        // Öğretmen kodu = öğretmen onayı; veli kodu girdi = veli onayı → doğrudan Approved.
        link.Approve(command.ParentUserId, existingPrimary?.ParentUserId, now);
        await _repository.SaveChangesAsync(cancellationToken);
        await _inviteDirectory.MarkClaimedAsync(info.InviteId, command.ParentUserId, cancellationToken);

        return Result<ChildLinkResponse>.Success(link.ToResponse(null));
    }
}
```

Ayrıca `ParentErrors`'a: `public static readonly Error InviteNotFound = new("parents.invite_not_found", "Davet kodu bulunamadı veya kullanılmış.");`

- [ ] **Step 4: Authorizer + DI** — `ParentAuthorizer`'a `ICommandAuthorizer<ClaimParentInviteCommand>` ekle (`RequireSelfOrAdmin(command.ParentUserId)`). DI: handler + authorizer kaydı.

- [ ] **Step 5: Endpoint** — `ParentsModule.cs`: `POST /api/parents/children/claim-invite` → `ClaimParentInviteRequest(string InviteCode)`, `currentUser.UserId` = ParentUserId. `ToHttpResult`'a `parents.invite_not_found`→404.

- [ ] **Step 6: Build + test → PASS** ; **Commit** `feat(parents): veli davet kodu claim (Veli V-D)`.

---

### Task 4: Dokümantasyon
- [ ] `doc/modules/m03_students.md`: `StudentParentInvite` + `POST /students/{id}/parent-invite` + `IParentInviteDirectory`.
- [ ] `doc/modules/m09_parents.md`: veli claim akışı + `POST /children/claim-invite`; "öğretmen kodu = öğretmen onayı, veli kod girer = veli onayı → Approved".
- [ ] `doc/modules/veri_modeli.md`: `StudentParentInvite` ER satırı + `ParentInviteStatus` enum + kontrat; `doc/modules/00_genel_bakis.md` endpoint envanteri; `doc/roles/veli.md` V-09.3 satırı.
- [ ] commit `docs: öğretmen→veli davet kodu + claim (Veli V-D)`.

## Self-Review
- **Spec coverage:** Spec V-D "öğretmenin girdiği veli → veli onayı ile bağlan" → davet-kodu modeli (Task 1-3). Telefon eşleştirme bilinçle dışta (karar 2026-07-19; Identity'de telefon araması yok).
- **Bağımlılık:** V-C (Approve 3-arg imza + birincil tekillik). V-D, V-C'den sonra uygulanmalı (spec sırası bunu sağlar).
- **Placeholder:** Task 2 Step 2 (Students application) tam kod yerine `InviteStudentCommand` desenine yönlendiriyor — bu mevcut, kanıtlanmış bir şablon; uygulayıcı birebir kopyalar. Novel/kritik kod (domain, contract, claim handler) tam verildi.
- **Type consistency:** `IParentInviteDirectory` (Students impl, Parents consume) ve `ParentChildLink.Approve` üç-argümanlı imza (V-C) tutarlı kullanılır.
