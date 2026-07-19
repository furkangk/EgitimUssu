# Öğrenci Ö-C — Claim (Davet Kodu) + Tam Profil Birleştirme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Öğretmenin manuel eklediği öğrenci, **6 haneli davet kodu**yla kendi hesabına geçebilsin; öğrencinin zaten self-register profili varsa iki profil **birleşsin** (veri bölünmesi bitmesin — B-01/AKIŞ 3).

**Architecture:** `TeacherStudentLink`'e tekil/süreli `InviteCode`. Kod-tabanlı claim: öğrenci kodu girer → link doğrulanır → `LinkUser` (manuel profili devral) VEYA öğrencinin mevcut self-profil'i varsa **merge**: kanonik = self-profil; manuel profilin modüller-arası `StudentId` referansları kanonike taşınır → `StudentProfilesMergedIntegrationEvent(FromStudentId, ToStudentId)` (Outbox) → Scheduling/Assignments/Payments/LessonSessions/Study kendi kayıtlarını günceller. Merge her zaman öğrenci onayıyla.

**Tech Stack:** .NET 9, EF Core, xUnit, CQRS, Outbox (Integration events → `Shared/Contracts` + handler'lar).

## Global Constraints
- Migration (Students): `--project src/Modules/Students/Infrastructure --context StudentsDbContext`
- Build `dotnet build EgitimUssu.slnx` · Test `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Modüller birbirine referans veremez; merge sinyali **Integration event** (Outbox) ile taşınır. Sözleşme `Shared/Contracts` altında.

## File Structure
- `src/Modules/Students/Domain/StudentsDomainModel.cs` — `TeacherStudentLink.InviteCode` + `GenerateInviteCode`; `StudentProfile.MarkMerged`.
- `src/Modules/Students/Application/TeacherStudentLinkFeatures.cs` — `ClaimStudentLinkCommand`; `IStudentProfileRepository.GetByUserIdAsync`.
- `src/Shared/Contracts/*` — `StudentProfilesMergedIntegrationEvent(FromStudentId, ToStudentId)` sözleşmesi.
- İlgili modüllerin `Infrastructure` handler'ları: Scheduling, Assignments, Payments, LessonSessions, Study — `Reassign(fromStudentId, toStudentId)`.
- Test: `tests/Unit/StudentClaimTests.cs`.

---

### Task 1: `TeacherStudentLink.InviteCode` + kod üretimi

**Files:** `StudentsDomainModel.cs`, `TeacherStudentLinkFeatures.cs` (invite), `StudentsDbContext.cs`, migration; `tests/Unit/StudentClaimTests.cs`.

**Interfaces:** Produces: `TeacherStudentLink.InviteCode (string?)`; `MarkInviteSent` imzasına `string inviteCode` eklenir (6 hane). `GenerateInviteCode()` yardımcı (6 haneli, rakam) — handler'da üretilir.

- [ ] **Step 1: Failing test** `StudentClaimTests`:
```csharp
using EgitimUssu.Modules.Students.Domain;
namespace EgitimUssu.Tests.Unit;
public sealed class StudentClaimTests
{
    private static readonly DateTime Now = new(2026,7,20,9,0,0,DateTimeKind.Utc);
    [Fact]
    public void MarkInviteSent_StoresCode()
    {
        var link = new TeacherStudentLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeacherStudentLinkStatus.Manual, Now);
        link.MarkInviteSent("123456", null, Now);
        Assert.Equal("123456", link.InviteCode);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }
}
```
- [ ] **Step 2: Run → FAIL** (`MarkInviteSent` imzası + `InviteCode` yok).
- [ ] **Step 3:** `InviteCode` property + `MarkInviteSent(string inviteCode, Guid? targetUserId, DateTime)` — `InviteCode = inviteCode`. Mevcut çağrı yerini (`InviteStudentCommandHandler`) güncelle: handler kod üretsin (`Random.Shared` 6 hane) ve geçsin.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** `StudentsDbContext` `InviteCode` config (`HasMaxLength(8)`, index); migration `AddTeacherStudentInviteCode`; build+test.
- [ ] **Step 6:** commit `feat(students): öğrenci davet kodu (InviteCode) (Ö-C)`.

---

### Task 2: Kod ile claim (merge yok — basit devralma)

**Files:** `TeacherStudentLinkFeatures.cs`, `ITeacherStudentLinkRepository` (GetByInviteCode), `IStudentProfileRepository` (GetByUserIdAsync), `Students/API/*`, DI; test.

**Interfaces:** Produces: `ClaimStudentLinkCommand(string InviteCode, Guid ClaimingUserId) : ICommand<Result>`; `ITeacherStudentLinkRepository.GetByInviteCodeAsync(code)`; `IStudentProfileRepository.GetByUserIdAsync(userId)`. Endpoint `POST /api/students/links/claim { inviteCode }` (claimingUserId = oturum kullanıcısı).

- [ ] **Step 1: Failing test** — sahte repository ile: geçerli kodla claim → link `Linked`, manuel profil `LinkUser(claimingUserId)`; kullanıcının mevcut profili **yoksa** merge event yayılmaz.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `ClaimStudentLinkCommandHandler`: `GetByInviteCodeAsync` → yoksa `students.invite_not_found`; link `InviteSent` değilse `students.invite_invalid`; `link.Accept()`; öğrencinin `GetByUserIdAsync(ClaimingUserId)` mevcut profili **yoksa** → `manualProfile.LinkUser(ClaimingUserId)` (mevcut davranış). (Merge Task 3'te.)
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** endpoint (`StudentsModule`) + authorizer (kimliği doğrulanmış herhangi bir öğrenci; kod bilgisi sahiplik yerine geçer) + DI.
- [ ] **Step 6:** commit `feat(students): kod tabanlı öğrenci claim (Ö-C)`.

---

### Task 3: Tam profil birleştirme (merge)

**Files:** `TeacherStudentLinkFeatures.cs` (claim handler merge dalı), `StudentsDomainModel.cs` (`StudentProfile.MarkMerged`, `TeacherStudentLinkStatus`? — gerekirse), `Shared/Contracts` (event), test.

**Interfaces:** Produces: Claim'de kullanıcının **mevcut self-profil'i varsa** → kanonik = self-profil (`existing.Id`), kaynak = manuel (`link.StudentId`). `link` kanonike bağlanır (`TeacherUserId` aynı kalır, `StudentId = existing.Id`? — bkz. not); manuel profil `MarkMerged(existing.Id)`; `StudentProfilesMergedIntegrationEvent(FromStudentId=link.StudentId, ToStudentId=existing.Id)` outbox'a. `StudentProfile.MarkMerged(Guid canonicalId, DateTime)` + `enum` durum (`IsMerged`/`MergedIntoStudentId`).

- [ ] **Step 1: Failing test** — kullanıcının mevcut self-profil'i **varken** claim → merge event yayıldı (FromStudentId=manuel, ToStudentId=self); manuel profil `IsMerged`.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** Claim handler merge dalı: `existing = GetByUserIdAsync(ClaimingUserId)`; `existing is not null && existing.Id != link.StudentId` → `link` kanonike taşınır; `manualProfile.MarkMerged(existing.Id, now)` + `Raise(StudentProfilesMergedDomainEvent(...))` (Students domain event → Outbox integration event). `StudentProfile`'a `bool IsMerged`, `Guid? MergedIntoStudentId`, `MarkMerged`. Integration event sözleşmesi `Shared/Contracts`.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** build+test+commit `feat(students): claim'de tam profil birleştirme + merge event (Ö-C/B5)`.
> **Not (StudentId taşıma):** Kanonik `StudentId` self-profil olduğundan, öğretmenin manuel profile bağladığı dersler/ödevler self-profil'e taşınmalı → Task 4. `link.StudentId` alanının kanonike güncellenmesi Students içinde; diğer modüller Task 4'te.

---

### Task 4: Modüller-arası StudentId yeniden atama (merge handler'ları)

**Files:** Her modülün `Infrastructure` altında yeni integration event handler'ı + repository `ReassignStudentAsync(fromStudentId, toStudentId)`:
- `Scheduling` — `LessonSchedule.StudentId`, `StudyScheduleEntry.StudentId`.
- `Assignments` — `Assignment.StudentId`, `LessonNote.StudentId`.
- `Payments` — `PaymentRecord.StudentId`.
- `LessonSessions` — `LessonSession.StudentId`.
- `Study` — `StudySession/TestResult/StudyGoal/StudyStreak/StudyTopic/StudentAchievement/StudySubjectCatalog/StudyTopicCatalog/StudyNote/StudyStudent.StudentId`.

**Interfaces:** Consumes: `StudentProfilesMergedIntegrationEvent(FromStudentId, ToStudentId)`. Produces (her modül): `<Module>StudentMergedHandler` → `UPDATE ... SET StudentId=ToStudentId WHERE StudentId=FromStudentId`.

- [ ] **Step 1:** Her modül için handler + repo toplu-güncelleme metodu (`ExecuteUpdateAsync` ile `SET StudentId`). Örnek desen (Scheduling):
```csharp
internal sealed class SchedulingStudentMergedHandler : IIntegrationEventHandler<StudentProfilesMergedIntegrationEvent>
{
    private readonly SchedulingDbContext _db;
    public SchedulingStudentMergedHandler(SchedulingDbContext db) => _db = db;
    public async Task Handle(StudentProfilesMergedIntegrationEvent e, CancellationToken ct)
    {
        await _db.LessonSchedules.Where(x => x.StudentId == e.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, e.ToStudentId), ct);
        await _db.StudyScheduleEntries.Where(x => x.StudentId == e.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, e.ToStudentId), ct);
    }
}
```
> Aynı deseni Assignments/Payments/LessonSessions/Study için tekrarla (her tablo için bir `ExecuteUpdateAsync`). Her handler'ı ilgili modülün `DependencyInjection` + outbox dispatch kaydına ekle (mevcut integration event handler kayıt desenini izle — örn. Notifications handler'ları).
- [ ] **Step 2:** Her modül için build+test.
- [ ] **Step 3:** Entegrasyon testi (Testcontainers): merge sonrası kaynak `StudentId`'ye ait hiçbir kayıt kalmaması; veli panelinin tek `StudentId`'den beslenmesi.
- [ ] **Step 4:** commit `feat: profil merge → modüller-arası StudentId yeniden atama (Ö-C/B5)`.

---

### Task 5: Dokümantasyon
- [ ] `m03_students.md` claim + merge akışı; `veri_modeli.md` merge event + `IsMerged`/`MergedIntoStudentId`; `00_genel_bakis.md` `/links/claim`. `doc/roles/ogrenci.md` S-01.2 claim. Tarih 2026-07-18.
- [ ] commit `docs: öğrenci claim + profil merge (Ö-C)`.

## Self-Review
- **En riskli dilim** — merge modüller-arası. Handler deseni tek örnekten çoğaltılır; her tablo için bir `ExecuteUpdateAsync`. Outbox atomikliği korunur.
- Merge her zaman öğrenci onayıyla (kod girişi). Kod tekil/süreli. Kişisel not/paylaşım kanonik profile taşınır.
- **Kapsam kararı:** Task 2 (basit claim) tek başına teslim edilebilir; Task 3-4 (merge) ayrı commit/checkpoint — merge çok modüllü olduğu için sırayla doğrulanır.
