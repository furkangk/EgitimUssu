# Öğrenci Ö-B — Hedef Sınav + Net Formülü + Deneme Sınavı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Net doğru formülle hesaplansın (LGS `/3`, TYT/AYT `/4`, okul denemesi yanlış götürmez) ve öğrenci çok dersli **deneme** (MockExam) girebilsin.

**Architecture:** Öğrencinin **hedef sınavı** M03 `StudentProfile`'da tutulur (S-03.9). Net formülü Study içinde saf `ExamPenalty` ile türetilir; modül izolasyonu için hedef sınav istemci tarafından teste geçirilir (Study, Students'a referans vermez). Deneme = yeni `MockExam` aggregate + `TestResult.MockExamId` FK.

**Tech Stack:** .NET 9, EF Core (`students` + `study` şemaları), xUnit, CQRS.

## Global Constraints
- Migration (Students): `dotnet ef migrations add <Ad> --project src/Modules/Students/Infrastructure --startup-project src/API.Host --context StudentsDbContext`
- Migration (Study): `--project src/Modules/Study/Infrastructure --context StudyDbContext`
- Build `dotnet build EgitimUssu.slnx` · Test `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`

## File Structure
- `src/Modules/Students/Domain/StudentsDomainModel.cs` — `TargetExam` enum + `StudentProfile.TargetExam` + `SetTargetExam`.
- `src/Modules/Students/Application/StudentProfileFeatures.cs` + `API/*` — upsert alanı.
- `src/Modules/Study/Application/ExamPenalty.cs` *(yeni)* — saf divisor fonksiyonu.
- `src/Modules/Study/Domain/StudyDomainModel.cs` — `MockExam` + `TestResult.MockExamId`.
- `src/Modules/Study/Application/StudyMockExamFeatures.cs` *(yeni)* — deneme command/handler.
- `src/Modules/Study/Infrastructure/*` + `StudyModule.cs` — config, endpoint, DI.
- Test: `tests/Unit/ExamPenaltyTests.cs`, `tests/Unit/MockExamTests.cs`.

---

### Task 1: `StudentProfile.TargetExam`

**Files:** `Students/Domain/StudentsDomainModel.cs`, `StudentProfileFeatures.cs`, `Students/API/*`, `StudentsDbContext.cs`, migration; `tests/Unit/StudentProfileTests.cs` (varsa ekle, yoksa create).

**Interfaces:** Produces: `enum TargetExam { None=0, LGS=1, TYT=2, AYT=3, YDS=4, School=5, Other=6 }`; `StudentProfile.TargetExam` (get) + `SetTargetExam(TargetExam, now)`; upsert command/request'e `TargetExam`.

- [ ] **Step 1: Failing test** — `StudentProfileTests`: yeni profilin `TargetExam` set edilip `Update`/`SetTargetExam` ile değiştiğini doğrula.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `TargetExam` enum + property (default `None`) + ctor param (sona) + `SetTargetExam`. `Update(...)` imzasına da ekle ya da ayrı `SetTargetExam`.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** `CreateStudentProfileCommand`/`UpdateStudentProfileCommand` + request DTO'lara `TargetExam`; handler'larda geçir; response'a ekle.
- [ ] **Step 6:** `StudentsDbContext` config `HasConversion<string>().HasMaxLength(16)`; migration `AddStudentTargetExam` (default `None`).
- [ ] **Step 7:** build+test+commit `feat(students): öğrenci hedef sınavı (TargetExam) (Ö-B/S-03.9)`.

---

### Task 2: `ExamPenalty` + net formülü türetme

**Files:** `Study/Application/ExamPenalty.cs` (create), `StudyTestFeatures.cs`, `StudyModule.cs`, `tests/Unit/ExamPenaltyTests.cs`.

**Interfaces:** Produces: `ExamPenalty.DivisorFor(string? targetExam) → int?` (LGS→3, TYT/AYT→4, School→null[yanlış götürmez], diğer→4). `RecordTestResultCommand`'a `string? TargetExam` (opsiyonel); `PenaltyDivisor` null ve `TargetExam` verildiyse ondan türet; School (null) ise `Net=Doğru` (penaltyDivisor'ı çok büyük tut → wrong/∞≈0; pratikte `Net=Correct`).

- [ ] **Step 1: Failing test** `ExamPenaltyTests`:
```csharp
using EgitimUssu.Modules.Study.Application;
namespace EgitimUssu.Tests.Unit;
public sealed class ExamPenaltyTests
{
    [Theory]
    [InlineData("LGS", 3)]
    [InlineData("TYT", 4)]
    [InlineData("AYT", 4)]
    [InlineData("Other", 4)]
    public void DivisorFor_KnownExams(string exam, int expected)
        => Assert.Equal(expected, ExamPenalty.DivisorFor(exam));

    [Fact]
    public void DivisorFor_School_ReturnsNull() // yanlış götürmez
        => Assert.Null(ExamPenalty.DivisorFor("School"));
}
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `ExamPenalty.DivisorFor(string? targetExam)` saf metot (case-insensitive; null/None/unknown→4; School→null).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** `RecordTestResultCommand`'a `string? TargetExam` ekle; handler'da `var divisor = command.PenaltyDivisor is > 0 ? command.PenaltyDivisor.Value : (ExamPenalty.DivisorFor(command.TargetExam) ?? int.MaxValue);` — `int.MaxValue` durumunda `TestResult` net'i pratikte `Correct` olur (wrong/MaxValue≈0). `TestResult` ctor `penaltyDivisor<=0→4` mantığını koru; School için MaxValue geçmek net=Correct verir. `StudyModule.cs` record-test request DTO'suna `TargetExam` ekle.
- [ ] **Step 6:** build+test+commit `feat(study): net formülü sınav tipine göre (ExamPenalty) (Ö-B/B4)`.

---

### Task 3: `MockExam` (çok dersli deneme)

**Files:** `Study/Domain/StudyDomainModel.cs`, `StudyMockExamFeatures.cs` (create), `StudyContracts.cs`, `StudyDbContext.cs`, `StudyModule.cs`, `DependencyInjection.cs`, migration; `tests/Unit/MockExamTests.cs`.

**Interfaces:** Produces: `MockExam : AggregateRoot<Guid>` (`Id, StudentId, ExamType(string), TakenOnUtc, TotalNet, EstimatedRank?, CreatedOnUtc`) + `AddSubject(TestResult)`/hesap. `TestResult.MockExamId (Guid?)` + ctor'a opsiyonel param. `CreateMockExamCommand(StudentId, ExamType, TakenOnUtc, IReadOnlyCollection<MockExamSubjectInput>)` (her subject: subject, total, correct, wrong, blank, penaltyDivisor/targetExam). `IStudyRepository.AddMockExamAsync`.

- [ ] **Step 1: Failing test** `MockExamTests`:
```csharp
using EgitimUssu.Modules.Study.Domain;
namespace EgitimUssu.Tests.Unit;
public sealed class MockExamTests
{
    private static readonly DateTime Now = new(2026,7,20,9,0,0,DateTimeKind.Utc);
    [Fact]
    public void MockExam_SumsNetOfSubjects()
    {
        var m = new MockExam(Guid.NewGuid(), Guid.NewGuid(), "TYT", Now, Now);
        var t1 = new TestResult(Guid.NewGuid(), m.StudentId, "Türkçe", null, null, TestType.General, 40, 30, 8, 2, 4, null, Now, false, false, Now); // 30-2=28
        var t2 = new TestResult(Guid.NewGuid(), m.StudentId, "Matematik", null, null, TestType.General, 40, 20, 4, 16, 4, null, Now, false, false, Now); // 20-1=19
        m.AddSubject(t1); m.AddSubject(t2);
        Assert.Equal(47m, m.TotalNet); // 28+19
    }
}
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `MockExam` aggregate: ctor + `List<Guid> SubjectTestIds` (veya sadece `TotalNet` toplayan) + `AddSubject(TestResult t)` → `TotalNet += t.Net`, `t`'ye `MockExamId` set. `TestResult`'a `Guid? MockExamId { get; private set; }` + `AttachToMockExam(Guid)`.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** `StudyMockExamFeatures.cs` — `CreateMockExamCommand` + handler: her subject için `TestResult` üret (Task 2 divisor mantığı) + `MockExam.AddSubject`; `AddMockExamAsync` + her test `AddTestAsync`; tek `SaveChanges`. `MockExamResponse(Id, ExamType, TakenOnUtc, TotalNet, Subjects[])`. `StudyContracts` response + `IStudyRepository.AddMockExamAsync`. `StudyDbContext` `MockExam` config (`ExamType` string, index `StudentId+TakenOnUtc`) + `TestResult.MockExamId` config + index. `StudyModule` `POST /students/{studentId}/mock-exams`. DI + ownership authorizer.
- [ ] **Step 6:** migration `AddMockExam` (yeni tablo + `TestResults.MockExamId`); build+test.
- [ ] **Step 7:** commit `feat(study): çok dersli deneme sınavı (MockExam) (Ö-B/B6)`.

---

### Task 4: Dokümantasyon
- [ ] `doc/modules/m08_study.md`: `MockExam` + net formülü kuralı; `m03_students.md`: `TargetExam`. `veri_modeli.md` ER (`MockExam`, `TestResult.MockExamId`, `TargetExam` enum). `00_genel_bakis.md` endpoint. Tarih 2026-07-18.
- [ ] commit `docs: öğrenci net formülü + deneme sınavı (Ö-B)`.

## Self-Review
- `ExamPenalty` saf + testli (LGS≠TYT, School yanlış götürmez). MockExam net toplamı testli. Modül izolasyonu: Study, Students'a referans vermez — hedef sınav istemci üzerinden geçer.
- Tip tutarlılığı: `TargetExam` string olarak Study'ye geçer; `MockExamId` Guid? ctor→domain→config boyunca aynı.
