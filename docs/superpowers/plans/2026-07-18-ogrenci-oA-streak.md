# Öğrenci Ö-A — Streak Kuralları Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Streak'i "hafızasız kronometre"den çıkarıp anlamlı kurala bağlamak — gün ancak günlük hedefin ayarlanabilir bir yüzdesi (varsayılan %60, hedef yoksa 20 dk) tamamlanınca seriye sayılır; streak gün sınırı 04:00'e taşınır.

**Architecture:** Mevcut M08 Study'ye additive. Kurallar saf `StreakRules` sınıfında toplanır (birim testli). Eşik, `StudyGoal`'a eklenen `StreakThresholdPercent` ile ayarlanır. `StudyCompletionService.RecordCompletedAsync`, günü işaretlemeden önce o günün (04:00 tabanlı) toplam efektif dakikasını eşiğe karşı kontrol eder.

**Tech Stack:** .NET 9, C#, EF Core (PostgreSQL, `study` şeması), xUnit + Assert (`tests/Unit`), CQRS, `IClock`/`IIdGenerator`.

## Global Constraints

- **Persistence:** `study` şeması + `StudyDbContext`. Migration: `dotnet ef migrations add <Ad> --project src/Modules/Study/Infrastructure --startup-project src/API.Host --context StudyDbContext`
- **Test:** `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`. Build: `dotnet build EgitimUssu.slnx` (dosya `.slnx`, `.sln` değil).
- **DateTime:** UTC; `IClock.UtcNow`. Yerel gün hesabı `StudyLocalTime` (Europe/Istanbul, `OffsetHours=3`).
- **Commit sonu:** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- **Doküman:** Son task'ta `doc/modules/m08_study.md` + `doc/roles/ogrenci.md` güncellenir; tarih 2026-07-18.
- **Korunacak kural:** Kişisel not hiçbir role sızmaz — bu plan not alanına dokunmaz.

## File Structure

- `src/Modules/Study/Domain/StudyDomainModel.cs` — `StudyGoal.StreakThresholdPercent`.
- `src/Modules/Study/Application/StudyPolicies.cs` — `StudyLocalTime.StreakDate(...)` + yeni saf `StreakRules`.
- `src/Modules/Study/Application/StudySessionFeatures.cs` — `RecordCompletedAsync` eşik kontrolü.
- `src/Modules/Study/Application/StudyProgressFeatures.cs` — `UpdateStudyGoalsCommand`/`StudyGoalResponse` alanı.
- `src/Modules/Study/API/StudyModule.cs` — goals request/response alanı.
- `src/Modules/Study/Infrastructure/StudyDbContext.cs` — config (int, non-null, default 60).
- `tests/Unit/StreakRulesTests.cs` *(yeni)*, `tests/Unit/StudyStreakTests.cs` *(yeni)*.

**Kapsam notu:** Streak **dondurma** (Premium) → Ö-D; **proaktif kırılma uyarısı** (Notifications) → ayrı; **seans/test düzenle-sil + rollup geri-hesabı** (B7) → **Ö-A2** (recompute karmaşıklığı ayrı ele alınır). Bu plan yalnız B3 streak eşiği + 04:00 sınırını kapsar.

---

### Task 1: `StreakRules` saf sınıfı (eşik + 04:00 gün)

**Files:**
- Modify: `src/Modules/Study/Application/StudyPolicies.cs`
- Test: `tests/Unit/StreakRulesTests.cs` (create)

**Interfaces:**
- Produces: `StreakRules.EffectiveThresholdMinutes(int dailyGoalMinutes, int thresholdPercent) → int` (dailyGoal>0 ise `ceil(dailyGoal*pct/100)`, aksi halde `20`). `StreakRules.DayCounts(int dayTotalMinutes, int dailyGoalMinutes, int thresholdPercent) → bool`. `StudyLocalTime.StreakDate(DateTime utc) → DateOnly` (04:00 sınırı: `LocalDate(utc.AddHours(-4))`).

- [ ] **Step 1: Write the failing test**

`tests/Unit/StreakRulesTests.cs`:
```csharp
using EgitimUssu.Modules.Study.Application;

namespace EgitimUssu.Tests.Unit;

public sealed class StreakRulesTests
{
    [Theory]
    [InlineData(120, 60, 72)]  // 120 dk hedef, %60 → 72 dk eşik
    [InlineData(0, 60, 20)]    // hedef yok → 20 dk sabit
    [InlineData(100, 65, 65)]  // %65
    public void EffectiveThresholdMinutes_Computes(int dailyGoal, int pct, int expected)
        => Assert.Equal(expected, StreakRules.EffectiveThresholdMinutes(dailyGoal, pct));

    [Fact]
    public void DayCounts_TrueWhenAtOrAboveThreshold_FalseBelow()
    {
        Assert.True(StreakRules.DayCounts(72, 120, 60));   // eşiğe eşit
        Assert.True(StreakRules.DayCounts(90, 120, 60));   // üstünde
        Assert.False(StreakRules.DayCounts(71, 120, 60));  // altında
        Assert.False(StreakRules.DayCounts(10, 0, 60));    // 10<20 sabit
        Assert.True(StreakRules.DayCounts(20, 0, 60));     // 20=20 sabit
    }

    [Fact]
    public void StreakDate_RollsAt0400Local()
    {
        // 2026-07-20 00:30 Europe/Istanbul (UTC 2026-07-19 21:30) → hâlâ 19 Temmuz (04:00 öncesi)
        var utc = new DateTime(2026, 7, 19, 21, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 19), StudyLocalTime.StreakDate(utc));

        // 2026-07-20 05:00 Europe/Istanbul (UTC 2026-07-20 02:00) → 20 Temmuz (04:00 sonrası)
        var utc2 = new DateTime(2026, 7, 20, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 7, 20), StudyLocalTime.StreakDate(utc2));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StreakRulesTests`
Expected: FAIL — `StreakRules` ve `StudyLocalTime.StreakDate` yok.

- [ ] **Step 3: Implement `StreakRules` + `StreakDate`**

`StudyPolicies.cs` — `StudyLocalTime` içine ekle:
```csharp
    /// <summary>Streak gün sınırı 04:00'tir (gece geç çalışan öğrenci dünü korur).</summary>
    public static DateOnly StreakDate(DateTime utcNow) => DateOnly.FromDateTime(utcNow.AddHours(OffsetHours).AddHours(-4));
```
`StudyPolicies.cs` — `StudyLocalTime`'dan sonra yeni saf sınıf:
```csharp
/// <summary>Streak (seri) kuralları — birim testli saf mantık.</summary>
internal static class StreakRules
{
    public const int MinFixedThresholdMinutes = 20;

    public static int EffectiveThresholdMinutes(int dailyGoalMinutes, int thresholdPercent)
        => dailyGoalMinutes > 0
            ? (int)Math.Ceiling(dailyGoalMinutes * (thresholdPercent / 100.0))
            : MinFixedThresholdMinutes;

    public static bool DayCounts(int dayTotalMinutes, int dailyGoalMinutes, int thresholdPercent)
        => dayTotalMinutes >= EffectiveThresholdMinutes(dailyGoalMinutes, thresholdPercent);
}
```
> `StreakRules` `internal`; test projesi Study.Application'a referanslı ve `InternalsVisibleTo` varsa erişir. Erişilemezse `public` yap (mevcut `StudyLocalTime` görünürlük desenini izle — testte kullanılıyorsa public olmalı).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StreakRulesTests`
Expected: PASS.
> Not: Erişim hatası alırsan `StreakRules`'u `public static` yap ve tekrar çalıştır.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(study): streak eşik kuralları + 04:00 gün sınırı saf sınıfı (Ö-A)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: `StudyGoal.StreakThresholdPercent` alanı

**Files:**
- Modify: `src/Modules/Study/Domain/StudyDomainModel.cs`
- Modify: `src/Modules/Study/Application/StudyProgressFeatures.cs`
- Modify: `src/Modules/Study/Application/StudyContracts.cs`
- Modify: `src/Modules/Study/API/StudyModule.cs`
- Modify: `src/Modules/Study/Infrastructure/StudyDbContext.cs`
- Test: `tests/Unit/StudyStreakTests.cs` (create)

**Interfaces:**
- Produces: `StudyGoal` ctor + `UpdateGoals` sonuna `int streakThresholdPercent`; `StudyGoal.StreakThresholdPercent` (int, get). `UpdateStudyGoalsCommand` + `StudyGoalResponse` + `UpdateStudyGoalRequest`'e `int StreakThresholdPercent`.

- [ ] **Step 1: Write the failing test**

`tests/Unit/StudyStreakTests.cs`:
```csharp
using EgitimUssu.Modules.Study.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudyStreakTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Goal_StoresStreakThresholdPercent()
    {
        var goal = new StudyGoal(Guid.NewGuid(), Guid.NewGuid(), 120, null, null, null, null, 60, Now);
        Assert.Equal(60, goal.StreakThresholdPercent);

        goal.UpdateGoals(120, null, null, null, null, 75, Now);
        Assert.Equal(75, goal.StreakThresholdPercent);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StudyStreakTests`
Expected: FAIL — `StudyGoal` ctor 8 argüman + `StreakThresholdPercent` yok.

- [ ] **Step 3: Add field to `StudyGoal`**

`StudyDomainModel.cs` — `StudyGoal` ctor imzasına `string? subject`'ten sonra `int streakThresholdPercent` ekle; atama + property; `UpdateGoals` imzasına da aynı konuma ekle + atama. Varsayılan/geçersiz değeri sınırla: `StreakThresholdPercent = Math.Clamp(streakThresholdPercent <= 0 ? 60 : streakThresholdPercent, 1, 100);`
```csharp
    public int StreakThresholdPercent { get; private set; }
```
(ctor ve `UpdateGoals` gövdesinde yukarıdaki clamp atamasını uygula.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StudyStreakTests`
Expected: PASS.

- [ ] **Step 5: Thread through Application + API + config**

`StudyProgressFeatures.cs`:
- `UpdateStudyGoalsCommand` kaydına `string? Subject`'ten sonra `int StreakThresholdPercent` ekle.
- Handler'da `new StudyGoal(...)` ve `goal.UpdateGoals(...)` çağrılarına `subject`'ten sonra `command.StreakThresholdPercent` geçir.
- `StudyGoalResponse.ToResponse` (nerede tanımlıysa — muhtemelen `StudyProgressFeatures`/mappings) `Subject`'ten sonra `goal.StreakThresholdPercent` ekle.

`StudyContracts.cs` — `StudyGoalResponse` kaydına `string? Subject`'ten sonra `int StreakThresholdPercent` ekle.

`StudyModule.cs` — goals güncelleme request DTO'suna `int StreakThresholdPercent` ekle; `ToCommand`/dispatch'e geçir.

`StudyDbContext.cs` — `StudyGoal` konfigürasyonuna: `builder.Property(e => e.StreakThresholdPercent);` (int non-null; migration default 60 verilecek).

- [ ] **Step 6: Migration + build + test**

Run: `dotnet ef migrations add AddStreakThresholdPercent --project src/Modules/Study/Infrastructure --startup-project src/API.Host --context StudyDbContext`
Migration dosyasında kolonu `defaultValue: 60` ile ekle (elle düzenle: `AddColumn<int>(... nullable: false, defaultValue: 60)`).
Sonra `dotnet build EgitimUssu.slnx` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(study): StudyGoal ayarlanabilir StreakThresholdPercent (Ö-A)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Eşik-tabanlı gün işaretleme (`RecordCompletedAsync`)

**Files:**
- Modify: `src/Modules/Study/Application/StudySessionFeatures.cs`
- Test: `tests/Unit/StudyStreakTests.cs`

**Interfaces:**
- Consumes: `StreakRules.DayCounts`, `StudyLocalTime.StreakDate`, `IStudyRepository.ListCompletedSessionsAsync`, `GetActiveGoalAsync`.
- Produces: davranış — gün yalnız o günün toplam efektif dakikası eşiği geçince `RegisterStudyDay` çağrılır.

- [ ] **Step 1: Write the failing test (davranış — sahte repository ile)**

`tests/Unit/StudyStreakTests.cs` içine ekle. Hafif bir `IStudyRepository` sahtesi (yalnız gereken metotlar; diğerleri `NotImplementedException`), sabit `IClock`, `AchievementEvaluator` yerine gerçek/no-op bağımlılık gerektiğinden **`StudyCompletionService`'i doğrudan test etmek yerine** eşik kararını izole eden bir yardımcı test yaz: `RecordCompletedAsync`'in çağırdığı "gün sayılır mı" kararını `StreakRules.DayCounts` üzerinden Task 1 kapsıyor. Bu task'ta **entegre davranış** testi:
```csharp
    [Fact]
    public async Task RecordCompleted_BelowThreshold_DoesNotRegisterStreakDay()
    {
        // 10 dk seans, hedef 120 dk / %60 → eşik 72 dk. Gün sayılmamalı.
        var studentId = Guid.NewGuid();
        var repo = new FakeStudyRepository(studentId, dailyGoal: 120, thresholdPercent: 60, existingTodayMinutes: 0);
        var svc = new StudyCompletionService(repo, new NoopAchievementEvaluator(repo), new FakeIdGen(), new FakeClock(Now));
        var session = StudySession.CreateManual(Guid.NewGuid(), studentId, "Mat", "Türev", 10, Now, null, false, false, Now);
        repo.Seed(session);

        await svc.RecordCompletedAsync(session, CancellationToken.None);

        Assert.Equal(0, repo.Streak?.CurrentStreakDays ?? 0);
    }

    [Fact]
    public async Task RecordCompleted_AtThreshold_RegistersStreakDay()
    {
        var studentId = Guid.NewGuid();
        var repo = new FakeStudyRepository(studentId, dailyGoal: 120, thresholdPercent: 60, existingTodayMinutes: 62);
        var svc = new StudyCompletionService(repo, new NoopAchievementEvaluator(repo), new FakeIdGen(), new FakeClock(Now));
        var session = StudySession.CreateManual(Guid.NewGuid(), studentId, "Mat", "Türev", 10, Now, null, false, false, Now); // 62+10=72=eşik
        repo.Seed(session);

        await svc.RecordCompletedAsync(session, CancellationToken.None);

        Assert.Equal(1, repo.Streak?.CurrentStreakDays);
    }
```
> `FakeStudyRepository`, `NoopAchievementEvaluator`, `FakeIdGen`, `FakeClock` yardımcılarını test dosyasının sonuna ekle. `FakeStudyRepository.ListCompletedSessionsAsync` verilen gün için `existingTodayMinutes` + seed edilen seansları döndürsün; `GetActiveGoalAsync` `dailyGoal`+`thresholdPercent` içeren bir `StudyGoal` döndürsün; `GetStreakAsync`/`AddStreakAsync`/`SaveChangesAsync`/`SumEffectiveMinutesAsync`/`CountCompletedSessionsAsync`/`CountTestsAsync`/`GetTopicAsync` (null) uygulansın; kalanlar `throw new NotImplementedException()`. `NoopAchievementEvaluator.EvaluateAsync` no-op. `AchievementEvaluator`'ın gerçek imzasını `StudyPolicies.cs`/ilgili dosyadan doğrula ve sahteyi ona göre yaz (gerekirse gerçek `AchievementEvaluator`'ı boş katalogla kullan).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StudyStreakTests`
Expected: FAIL — şu an eşiğe bakılmadan her seans günü işaretliyor; ilk test (below threshold) `CurrentStreakDays=1` bulup patlar.

- [ ] **Step 3: Add threshold gate to `RecordCompletedAsync`**

`StudySessionFeatures.cs` — `RecordCompletedAsync` içinde `streak.RegisterStudyDay(...)` satırını eşikle koşulla:
```csharp
        var streakDate = StudyLocalTime.StreakDate(studiedOn);
        var dayStartUtc = StudyLocalTime.LocalDayStartUtc(streakDate);
        var daySessions = await _repository.ListCompletedSessionsAsync(
            session.StudentId, dayStartUtc, StudyLocalTime.LocalDayStartUtc(streakDate.AddDays(1)), cancellationToken);
        var dayTotal = daySessions.Sum(s => s.EffectiveMinutes);

        var goal = await _repository.GetActiveGoalAsync(session.StudentId, cancellationToken);
        var thresholdPercent = goal?.StreakThresholdPercent ?? 60;
        var dailyGoal = goal?.DailyGoalMinutes ?? 0;

        if (StreakRules.DayCounts(dayTotal, dailyGoal, thresholdPercent))
        {
            streak.RegisterStudyDay(streakDate, now);
        }
```
> `RegisterStudyDay`'e artık `StudyLocalTime.LocalDate` yerine `streakDate` (04:00 tabanlı) geçilir. Mevcut `streak.RegisterStudyDay(StudyLocalTime.LocalDate(studiedOn), now);` satırını yukarıdaki blokla değiştir. `daySessions` yeni eklenen `session`'ı içerdiğinden (önce `AddSessionAsync`/`Complete` ile kaydedildi) toplam doğru olur; emin olmak için `ListCompletedSessionsAsync` tamamlanmış seansları döndürüyor — seans bu noktada `Completed`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter FullyQualifiedName~StudyStreakTests`
Expected: PASS.

- [ ] **Step 5: Build + full test**

Run: `dotnet build EgitimUssu.slnx` + `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: PASS (mevcut testler dahil). `StudyStatistics.TodayEffectiveMinutesAsync` gibi `LocalDate` kullanan yerler değişmedi — yalnız streak `StreakDate` kullanır.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(study): streak günü yalnız eşik aşılınca sayılır + 04:00 sınırı (Ö-A/B3)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Dokümantasyon (KALICI KURAL)

**Files:**
- Modify: `doc/modules/m08_study.md`
- Modify: `doc/roles/ogrenci.md`

- [ ] **Step 1: m08_study.md**

`StudyGoal.StreakThresholdPercent` alanı + streak iş kuralı: "gün, günlük hedefin %`StreakThresholdPercent`'i (varsayılan 60; hedef yoksa 20 dk) tamamlanınca sayılır; gün sınırı 04:00". "Güncelleme: 2026-07-18".

- [ ] **Step 2: ogrenci.md**

§5 iş kurallarına streak eşiği maddesini ekle; §9 kabul kriterlerinde streak satırını güncelle. Tarih güncelle.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "docs: öğrenci streak eşik kuralı (Ö-A) — m08 + rol dokümanı

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review Notu (plan yazarı)

- **Kapsam:** Yalnız B3 (streak eşiği + 04:00). B7 (seans/test düzenle-sil + rollup recompute) bilinçli olarak **Ö-A2**'ye; dondurma **Ö-D**'ye; proaktif uyarı ayrı.
- **Tip tutarlılığı:** `StreakThresholdPercent` (int) ctor→UpdateGoals→command→response→request boyunca aynı ad/tip. `StreakRules.DayCounts`/`EffectiveThresholdMinutes` imzaları Task 1↔Task 3 tutarlı.
- **Bilinmeyen doğrulama:** `AchievementEvaluator` imzası ve `StreakRules`/`StudyLocalTime` görünürlüğü (internal↔public) Task 1/3'te uygulayıcı tarafından koda göre doğrulanır; test erişimi için gerekiyorsa public'e çekilir.
- **Migration:** Tek kolon (`StreakThresholdPercent`, default 60), additive.
