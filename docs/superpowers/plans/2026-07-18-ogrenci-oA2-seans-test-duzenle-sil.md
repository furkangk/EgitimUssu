# Öğrenci Ö-A2 — Seans/Test Düzenle-Sil + İstatistik Geri-Hesabı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Öğrenci tamamlanmış çalışma seansını ve test kaydını düzenleyip silebilsin (S-08.10/18); silme/düzenlemede konu rollup'ı ve net tutarlı kalsın.

**Architecture:** M08 Study'ye additive. Test net'i D/Y/B'den yeniden hesaplanır (bağımsız, kolay). Seans düzenleme/silme, ilgili (Subject, Topic) `StudyTopic` rollup'ını o öğrencinin tamamlanmış seanslarından **yeniden türetir** (`StudyRecompute`). Streak zinciri retroaktif geri sarılmaz (v1 YAGNI — dokümante edilir).

**Tech Stack:** .NET 9, EF Core (`study` şeması), xUnit, CQRS, `IClock`.

## Global Constraints
- Migration: `dotnet ef migrations add <Ad> --project src/Modules/Study/Infrastructure --startup-project src/API.Host --context StudyDbContext`
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Sahiplik: yeni komutlar `StudySessionOwnershipAuthorizer`/`StudyTestOwnershipAuthorizer` desenine tabi (öğrenci yalnız kendi kaydı; admin serbest).
- Kişisel not sızmaz — response'lar zaten öğrenci-kapsamlı.

## File Structure
- `src/Modules/Study/Domain/StudyDomainModel.cs` — `StudySession.EditCompleted/MarkDeleted`, `TestResult.Edit`.
- `src/Modules/Study/Application/StudySessionFeatures.cs` + `StudyTestFeatures.cs` — edit/delete command+handler.
- `src/Modules/Study/Application/StudyRecompute.cs` *(yeni)* — topic rollup yeniden türetme.
- `src/Modules/Study/Application/StudyContracts.cs` (`IStudyRepository`) + `StudyRepository.cs` — `RemoveSession`, `RemoveTest`, `ListCompletedSessionsByTopicAsync`.
- `src/Modules/Study/API/StudyModule.cs` — PUT/DELETE endpoint'leri.
- Test: `tests/Unit/StudySessionEditTests.cs`, `tests/Unit/TestResultEditTests.cs` (create).

---

### Task 1: `TestResult` düzenle + sil (net yeniden hesap)

**Files:** `StudyDomainModel.cs`, `StudyTestFeatures.cs`, `StudyContracts.cs`+`StudyRepository.cs`, `StudyModule.cs`, `tests/Unit/TestResultEditTests.cs`.

**Interfaces:**
- Produces: `TestResult.Edit(subject, topic, testName, testType, total, correct, wrong, blank, penaltyDivisor, durationMinutes, takenOnUtc, now)` — aynı doğrulama + net yeniden hesap. `EditTestResultCommand(TestResultId, …aynı alanlar)` → `Result<TestResultResponse>`; `DeleteTestResultCommand(TestResultId)` → `Result`. `IStudyRepository.RemoveTest(TestResult)`.

- [ ] **Step 1: Write the failing test**

`tests/Unit/TestResultEditTests.cs`:
```csharp
using EgitimUssu.Modules.Study.Domain;
namespace EgitimUssu.Tests.Unit;

public sealed class TestResultEditTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Edit_RecomputesNet()
    {
        var t = new TestResult(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, null, TestType.Subject,
            20, 10, 8, 2, 4, null, Now, false, false, Now);
        // İlk net = 10 - 8/4 = 8
        Assert.Equal(8m, t.Net);
        t.Edit("Mat", null, null, TestType.Subject, 20, 16, 4, 0, 4, null, Now, Now);
        Assert.Equal(15m, t.Net); // 16 - 4/4 = 15
    }
}
```

- [ ] **Step 2: Run → FAIL** (`Edit` yok).
Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~TestResultEditTests`

- [ ] **Step 3: Implement `TestResult.Edit`**

`StudyDomainModel.cs` — `TestResult`'a: aynı doğrulama bloğunu (D+Y+B=Toplam, negatif kontrol, penaltyDivisor≤0→4) uygulayan `Edit(...)` metodu; alanları set edip `Net = Math.Round(correct - ((decimal)wrong/penaltyDivisor), 2, MidpointRounding.AwayFromZero)` yeniden hesaplar. (Ctor'daki hesap satırının aynısı.)

- [ ] **Step 4: Run → PASS.**

- [ ] **Step 5: Command/handler/endpoint + repo**

`StudyTestFeatures.cs` — `EditTestResultCommand` (TestResultId + tüm test alanları) + handler: `GetTestAsync` → yoksa `study.test_not_found`; `test.Edit(...)`; `SaveChangesAsync`; response. `DeleteTestResultCommand` + handler: `GetTestAsync` → `RemoveTest` → save. Ownership authorizer'a (`StudyTestOwnershipAuthorizer`) her iki komutu ekle (kimlik=TestResultId).
`StudyContracts.cs` `IStudyRepository`'ye `void RemoveTest(TestResult t);` + `StudyRepository.cs` impl (`_db.TestResults.Remove(t)`).
`StudyModule.cs` — `PUT /students/{studentId}/tests/{testId}` + `DELETE /students/{studentId}/tests/{testId}` (mevcut test endpoint deseni; öğrenci-kapsamlı). DI kayıtları (`DependencyInjection.cs`).

- [ ] **Step 6: Build + test + commit**
```bash
git add -A && git commit -m "feat(study): test kaydı düzenle/sil + net yeniden hesap (Ö-A2/B7)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: `StudyRecompute` + `StudySession` düzenle/sil

**Files:** `StudyDomainModel.cs`, `StudyRecompute.cs` (create), `StudySessionFeatures.cs`, `StudyContracts.cs`+`StudyRepository.cs`, `StudyModule.cs`, `tests/Unit/StudySessionEditTests.cs`.

**Interfaces:**
- Produces: `StudySession.EditCompleted(subject, topic, effectiveMinutes, personalNote, now)` (yalnız `Completed` seans; `effectiveMinutes>0`). `IStudyRepository.RemoveSession(StudySession)`, `ListCompletedSessionsByTopicAsync(studentId, subject, topic, ct)`. `StudyRecompute.RebuildTopicAsync(repo, idGen, studentId, subject, topic, ct)` — o konu için `StudyTopic`'i tamamlanmış seanslardan yeniden türetir (yoksa oluşturur/siler). `EditStudySessionCommand`, `DeleteStudySessionCommand`.

- [ ] **Step 1: Write the failing test**

`tests/Unit/StudySessionEditTests.cs`:
```csharp
using EgitimUssu.Modules.Study.Domain;
namespace EgitimUssu.Tests.Unit;

public sealed class StudySessionEditTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EditCompleted_ChangesMinutesAndTopic()
    {
        var s = StudySession.CreateManual(Guid.NewGuid(), Guid.NewGuid(), "Mat", "Türev", 30, Now, null, false, false, Now);
        s.EditCompleted("Mat", "İntegral", 45, "düzeltildi", Now.AddMinutes(1));
        Assert.Equal(45, s.EffectiveMinutes);
        Assert.Equal("İntegral", s.Topic);
    }

    [Fact]
    public void EditCompleted_RejectsNonCompleted()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Now);
        Assert.Throws<InvalidOperationException>(() => s.EditCompleted("Mat", null, 10, null, Now));
    }
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement `StudySession.EditCompleted`**

`StudyDomainModel.cs` — `StudySession`'a:
```csharp
public void EditCompleted(string subject, string? topic, int effectiveMinutes, string? personalNote, DateTime nowUtc)
{
    if (Status != StudySessionStatus.Completed)
        throw new InvalidOperationException("Yalnızca tamamlanmış seans düzenlenebilir.");
    if (effectiveMinutes <= 0)
        throw new InvalidOperationException("Süre 0'dan büyük olmalıdır.");
    Subject = subject.Trim();
    Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
    EffectiveMinutes = effectiveMinutes;
    PersonalNote = string.IsNullOrWhiteSpace(personalNote) ? PersonalNote : personalNote.Trim();
    UpdatedOnUtc = nowUtc;
}
```

- [ ] **Step 4: Run → PASS.**

- [ ] **Step 5: `StudyRecompute` + repo metotları**

`StudyRepository.cs`+`IStudyRepository`: `void RemoveSession(StudySession)`; `Task<IReadOnlyList<StudySession>> ListCompletedSessionsByTopicAsync(studentId, subject, topic, ct)` (`Status==Completed && Subject==subject && Topic==topic`).
`StudyRecompute.cs` (yeni):
```csharp
public static async Task RebuildTopicAsync(IStudyRepository repo, IIdGenerator idGen, Guid studentId, string subject, string topic, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(topic)) return;
    var sessions = await repo.ListCompletedSessionsByTopicAsync(studentId, subject, topic, ct);
    var existing = await repo.GetTopicAsync(studentId, subject, topic, ct);
    if (sessions.Count == 0)
    {
        if (existing is not null) repo.RemoveTopic(existing); // IStudyRepository.RemoveTopic ekle
        return;
    }
    var total = sessions.Sum(s => s.EffectiveMinutes);
    var count = sessions.Count;
    var first = sessions.Min(s => s.EndedAtUtc ?? s.StartedAtUtc);
    var last = sessions.Max(s => s.EndedAtUtc ?? s.StartedAtUtc);
    if (existing is null)
        await repo.AddTopicAsync(new StudyTopic(idGen.New(), studentId, subject, topic, total, last), ct);
    else
        existing.Overwrite(total, count, first, last); // StudyTopic.Overwrite ekle
}
```
> Gereken domain/ repo eklemeleri: `StudyTopic.Overwrite(totalMinutes, sessionCount, firstUtc, lastUtc)` (private setter'ları güncelleyen metot); `IStudyRepository.RemoveTopic(StudyTopic)`.

- [ ] **Step 6: Command/handler/endpoint**

`StudySessionFeatures.cs`: `EditStudySessionCommand(SessionId, Subject, Topic, EffectiveMinutes, PersonalNote)` + handler: eski `(Subject,Topic)`'i sakla → `session.EditCompleted(...)` → save → `StudyRecompute.RebuildTopicAsync` **hem eski hem yeni** topic için. `DeleteStudySessionCommand(SessionId)` + handler: konu bilgisini al → `RemoveSession` → save → `RebuildTopicAsync(eski topic)`. Ownership authorizer'a ekle. `StudyModule.cs` `PUT/DELETE /students/{studentId}/sessions/{sessionId}` + DI.
> **Streak notu (YAGNI):** Bu task streak zincirini geri sarmaz; yalnız topic rollup'ı tutarlı tutar. Düzenleme/silme sonrası o günün streak-uygunluğu bir sonraki seans kaydında yeniden değerlendirilir. Tam streak recompute gerekirse ayrı iş.

- [ ] **Step 7: Build + test + commit**
```bash
git add -A && git commit -m "feat(study): çalışma seansı düzenle/sil + konu rollup yeniden türetme (Ö-A2/B7)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Migration yok — Dokümantasyon

**Files:** `doc/modules/m08_study.md`, `doc/roles/ogrenci.md`.

- [ ] **Step 1:** m08'e "seans/test düzenle-sil (S-08.10/18) + konu rollup yeniden türetme; streak zinciri v1'de geri sarılmaz" ekle. Tarih 2026-07-18.
- [ ] **Step 2:** ogrenci.md §9 kabul kriterine "seans/test düzenle-sil" ekle.
- [ ] **Step 3:** commit `docs: öğrenci seans/test düzenle-sil (Ö-A2)`.

## Self-Review
- Şema değişmez (davranış). Net/rollup yeniden hesabı testli. Streak geri-sarma bilinçli kapsam dışı.
- `RebuildTopicAsync` hem eski hem yeni topic için çağrılır (konu değişince eski rollup düşürülür).
