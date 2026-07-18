# Öğrenci Ö-F — Ders Erteleme Talebi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Öğrenci bir dersin ertelenmesini **talep** edebilsin (neden + alternatif tarih); öğretmen kabul/red etsin; kabulde mevcut `Reschedule` (Dilim A takvim) çalışsın (B-12/S-04.5/AKIŞ 12). Öğrenci dersi kendisi değiştirmez.

**Architecture:** M04 Scheduling'e yeni hafif `LessonChangeRequest` aggregate. Öğrenci `POST /students/{studentId}/lesson-requests` ile talep açar (sahiplik `IStudentDirectory`); öğretmene domain event → Notifications. Öğretmen `accept` → mevcut `RescheduleLessonScheduleCommand` çağrılır; `reject` → talep kapanır.

**Tech Stack:** .NET 9, EF Core (`scheduling` şeması), xUnit, CQRS, Outbox.

## Global Constraints
- Migration (Scheduling): `dotnet ef migrations add <Ad> --project src/Modules/Scheduling/Infrastructure --startup-project src/API.Host --context SchedulingDbContext`
- Build `dotnet build EgitimUssu.slnx` · Test `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Öğrenci sahipliği `IStudentDirectory` (mevcut, `StudentLessonQueryAuthorizer` deseni); öğretmen sahipliği `LessonScheduleCommandAuthorizer` deseni.

## File Structure
- `src/Modules/Scheduling/Domain/SchedulingDomainModel.cs` — `LessonChangeRequest` + `enum LessonChangeRequestStatus` + event.
- `src/Modules/Scheduling/Application/LessonChangeRequestFeatures.cs` *(yeni)* — command/query/handler/repo arayüzü.
- `src/Modules/Scheduling/Application/LessonChangeRequestPolicies.cs` *(yeni)* — authorizer'lar.
- `src/Modules/Scheduling/Infrastructure/*` — repo + config + DI + migration.
- `src/Modules/Scheduling/API/SchedulingModule.cs` — endpoint'ler.
- Test: `tests/Unit/LessonChangeRequestTests.cs`.

---

### Task 1: `LessonChangeRequest` domain

**Files:** `SchedulingDomainModel.cs`, `tests/Unit/LessonChangeRequestTests.cs`.

**Interfaces:** Produces: `LessonChangeRequest : AggregateRoot<Guid>` (`Id, LessonScheduleId, StudentId, TeacherUserId, Reason, ProposedStartAtUtc?, ProposedEndAtUtc?, Status, CreatedOnUtc, ResolvedOnUtc?`). `enum LessonChangeRequestStatus { Pending=1, Accepted=2, Rejected=3 }`. `Accept(now)`, `Reject(now)`. Oluşturmada `LessonChangeRequestedDomainEvent`; kabul/redde `LessonChangeRequestResolvedDomainEvent`.

- [ ] **Step 1: Failing test** `LessonChangeRequestTests`:
```csharp
using EgitimUssu.Modules.Scheduling.Domain;
namespace EgitimUssu.Tests.Unit;
public sealed class LessonChangeRequestTests
{
    private static readonly DateTime Now = new(2026,7,20,9,0,0,DateTimeKind.Utc);
    [Fact]
    public void Accept_SetsStatus_RaisesEvent()
    {
        var r = new LessonChangeRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "hastayım", Now.AddDays(1), Now.AddDays(1).AddHours(1), Now);
        r.Accept(Now.AddHours(1));
        Assert.Equal(LessonChangeRequestStatus.Accepted, r.Status);
        Assert.Contains(r.DomainEvents, e => e is LessonChangeRequestResolvedDomainEvent);
    }
    [Fact]
    public void Reject_OnlyFromPending()
    {
        var r = new LessonChangeRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", null, null, Now);
        r.Reject(Now);
        Assert.Throws<InvalidOperationException>(() => r.Accept(Now));
    }
}
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `LessonChangeRequest` aggregate + enum + `Accept`/`Reject` (yalnız `Pending`'den; aksi `InvalidOperationException`) + event'ler. Ctor `Status=Pending` + `LessonChangeRequestedDomainEvent`.
- [ ] **Step 4: Run → PASS + commit** `feat(scheduling): ders erteleme talebi domaini (Ö-F)`.

---

### Task 2: Application (command/query/repo) + Infra

**Files:** `LessonChangeRequestFeatures.cs` (create), `LessonChangeRequestPolicies.cs` (create), `Infrastructure/LessonChangeRequestRepository.cs` (create), `SchedulingDbContext.cs`, `DependencyInjection.cs`, migration.

**Interfaces:** Produces: `CreateLessonChangeRequestCommand(StudentId, LessonScheduleId, Reason, ProposedStartAtUtc?, ProposedEndAtUtc?)` → `Result<LessonChangeRequestResponse>`; `AcceptLessonChangeRequestCommand(RequestId)`, `RejectLessonChangeRequestCommand(RequestId)`; `ListLessonChangeRequestsForTeacherQuery(TeacherUserId, onlyPending)`. `ILessonChangeRequestRepository` (Add/GetById/ListForTeacher/ListForStudent/SaveChanges).

- [ ] **Step 1:** `CreateLessonChangeRequestCommandHandler` — dersi `ILessonScheduleRepository.GetByIdAsync` ile bul (TeacherUserId'yi ondan al), request oluştur, kaydet. Validator (Reason boş değil). Authorizer: öğrenci `IStudentDirectory` ile kendi `studentId`'si (mevcut `StudentLessonQueryAuthorizer` deseni).
- [ ] **Step 2:** `AcceptLessonChangeRequestCommandHandler` — request'i bul; `request.Accept()`; `ProposedStartAtUtc` doluysa **mevcut** `RescheduleLessonScheduleCommand`'ı dispatch et (veya `ILessonScheduleRepository` + `lesson.Reschedule` doğrudan). `Reject` handler basit. Öğretmen authorizer (`LessonScheduleCommandAuthorizer` deseni — request.TeacherUserId).
- [ ] **Step 3:** Repo impl + `SchedulingDbContext` `DbSet` + config (`Reason` maxlen 500, index `TeacherUserId+Status`, `StudentId`); DI kayıtları; migration `AddLessonChangeRequests`.
- [ ] **Step 4:** build+test+commit `feat(scheduling): erteleme talebi oluştur/kabul/red + repo (Ö-F)`.

---

### Task 3: API endpoint'leri

**Files:** `SchedulingModule.cs`.

**Interfaces:** Endpoint'ler: `POST /api/scheduling/students/{studentId}/lesson-requests`, `GET /api/scheduling/teachers/{teacherUserId}/lesson-requests?onlyPending=`, `POST /api/scheduling/lesson-requests/{requestId}/accept`, `POST /api/scheduling/lesson-requests/{requestId}/reject`.

- [ ] **Step 1:** Handler metotları + request DTO (`CreateLessonChangeRequestRequest`) + `ToHttpResult` eşlemeleri (`scheduling.request_not_found`→404). Mevcut `SchedulingModule` deseni.
- [ ] **Step 2:** build+test+commit `feat(scheduling): erteleme talebi endpoint'leri (Ö-F)`.

---

### Task 4: Dokümantasyon
- [ ] `m04_scheduling.md`: `LessonChangeRequest` + endpoint'ler + "öğrenci talep eder, öğretmen uygular" kuralı; `doc/roles/ogrenci.md` S-04.5; `doc/roles/ogretmen.md` (talep kabul akışı); `veri_modeli.md` ER; `00_genel_bakis.md`. Tarih 2026-07-18.
- [ ] commit `docs: öğrenci ders erteleme talebi (Ö-F)`.

## Self-Review
- Domain testli (accept/reject durum makinesi). Kabul → **mevcut** Reschedule'ı yeniden kullanır (DRY, Dilim A takvim). Öğrenci yalnız talep; dersi değiştirmez (yetki matrisi #1 korunur).
- `IStudentDirectory` + `LessonScheduleCommandAuthorizer` mevcut desenleri; yeni yetki tipi yok.
