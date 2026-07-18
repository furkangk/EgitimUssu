# Öğrenci Ö-D — Free/Premium Yönetimi Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Öğrenci tarafı Free/Premium **kapılarını** uygulamak (doküman §14.3): Free = çekirdek alışkanlık (kronometre, test, streak tam, son 30 gün geçmiş, temel haftalık analiz); Premium = derinlik (sınırsız geçmiş, aylık analiz, hedef net/puan takibi, konu zayıflık, streak dondurma).

**Architecture:** Öğrencinin üyelik seviyesi `MembershipTier` olarak tutulur (M17 çekirdeği; başlangıçta Study içinde hafif bir alan + `Shared/Contracts` `IMembershipDirectory` okuması). İlgili Study query/command'ları tier'a göre kısıtlanır. **Karar:** Free geniş tutulur (streak tam + 30 gün) — büyüme önce; Premium yalnız derinlik.

**Tech Stack:** .NET 9, EF Core, xUnit, CQRS.

## Global Constraints
- Build `dotnet build EgitimUssu.slnx` · Test `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Modül izolasyonu: Study, tier'ı `Shared/Contracts` sözleşmesinden okur.

## File Structure
- `src/Shared/Contracts/IMembershipDirectory.cs` *(yeni)* — `Task<MembershipTier> GetTierAsync(Guid userId, ct)`; `enum MembershipTier { Free=1, Premium=2 }`.
- `src/Modules/Students/*` veya yeni `Membership` — tier depolama + `IMembershipDirectory` implementasyonu (başlangıçta Students `StudentProfile.MembershipTier` alanı; M17 gelince taşınır).
- `src/Modules/Study/Application/MembershipGate.cs` *(yeni)* — saf kapı mantığı.
- `src/Modules/Study/Application/*` — geçmiş/analiz/hedef query'lerinde gate.
- Test: `tests/Unit/MembershipGateTests.cs`.

---

### Task 1: `MembershipTier` + `IMembershipDirectory` sözleşmesi + depolama

**Files:** `Shared/Contracts/IMembershipDirectory.cs`, `Students/Domain/StudentsDomainModel.cs` (`MembershipTier` alanı), `Students/Infrastructure/*` (directory impl + config), migration; `tests/Unit/StudentProfileTests.cs`.

**Interfaces:** Produces: `enum MembershipTier { Free=1, Premium=2 }`; `IMembershipDirectory.GetTierAsync(Guid userId, ct)`; `StudentProfile.MembershipTier` (default Free) + `SetMembershipTier(...)`.

- [ ] **Step 1: Failing test** — profilin `MembershipTier` default `Free`, `SetMembershipTier(Premium)` ile değişir.
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `MembershipTier` enum (Shared/Contracts) + `StudentProfile.MembershipTier` + `SetMembershipTier`. `IMembershipDirectory` sözleşmesi + Students impl (`UserId`→profil→tier).
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** config `HasConversion<string>()`; migration `AddStudentMembershipTier` (default `Free`); DI (`IMembershipDirectory`→Students impl).
- [ ] **Step 6:** commit `feat(students): öğrenci MembershipTier + IMembershipDirectory (Ö-D)`.

---

### Task 2: `MembershipGate` saf kapı mantığı

**Files:** `Study/Application/MembershipGate.cs` (create), `tests/Unit/MembershipGateTests.cs`.

**Interfaces:** Produces: `MembershipGate.HistoryWindowDays(MembershipTier) → int?` (Free→30, Premium→null[sınırsız]); `MembershipGate.Allows(MembershipTier, PremiumFeature) → bool` (`enum PremiumFeature { MonthlyAnalysis, TargetTracking, TopicWeakness, StreakFreeze, PdfReport }`).

- [ ] **Step 1: Failing test** `MembershipGateTests`:
```csharp
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Shared.Contracts;
namespace EgitimUssu.Tests.Unit;
public sealed class MembershipGateTests
{
    [Fact] public void Free_History30_Premium_Unlimited()
    {
        Assert.Equal(30, MembershipGate.HistoryWindowDays(MembershipTier.Free));
        Assert.Null(MembershipGate.HistoryWindowDays(MembershipTier.Premium));
    }
    [Theory]
    [InlineData(MembershipTier.Free, PremiumFeature.MonthlyAnalysis, false)]
    [InlineData(MembershipTier.Premium, PremiumFeature.MonthlyAnalysis, true)]
    [InlineData(MembershipTier.Free, PremiumFeature.StreakFreeze, false)]
    public void Allows_ByTier(MembershipTier tier, PremiumFeature f, bool expected)
        => Assert.Equal(expected, MembershipGate.Allows(tier, f));
}
```
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `MembershipGate` + `PremiumFeature` enum. `HistoryWindowDays`: Free→30, Premium→null. `Allows`: Free hepsi false; Premium hepsi true.
- [ ] **Step 4: Run → PASS + commit** `feat(study): MembershipGate kapı mantığı (Ö-D)`.

---

### Task 3: Kapıları query'lere uygula

**Files:** `Study/Application/StudySessionFeatures.cs` (`ListStudySessions`/`WeeklySummary` — 30 gün penceresi Free), `StudyProgressFeatures.cs` (aylık analiz/hedef takip gate), `StudyModule.cs`.

**Interfaces:** Consumes: `IMembershipDirectory`, `MembershipGate`. Free geçmiş sorgusunda `FromUtc` alt sınırı `now - 30 gün`'e clamp; Premium sınırsız. Aylık analiz/hedef net-puan takip endpoint'leri Free'de `study.premium_required` (402/403) döner.

- [ ] **Step 1:** `ListStudySessionsQueryHandler` + geçmiş listeleyen handler'lara `IMembershipDirectory` enjekte et; `HistoryWindowDays` null değilse `fromUtc = Max(fromUtc, now - window)`.
- [ ] **Step 2:** Aylık analiz + hedef net/puan takibi + streak dondurma (Ö-A dondurma özelliği) endpoint'lerinde `MembershipGate.Allows` kontrolü; değilse `Result.Failure(new Error("study.premium_required", "Bu özellik Premium'a özeldir."))`. `StudyModule.ToHttpResult` bu kodu 402/403'e eşle.
- [ ] **Step 3:** Bir handler için birim/entegrasyon testi (Free 30 gün clamp; Premium sınırsız).
- [ ] **Step 4:** build+test+commit `feat(study): Free/Premium kapıları (geçmiş/analiz/hedef) (Ö-D/B10)`.

---

### Task 4: Dokümantasyon
- [ ] `m08_study.md` Free/Premium kapı tablosu (§14.3 uyarlanmış); `doc/roles/ogrenci.md` §8 üyelik etkisi güncelle; `veri_modeli.md` `MembershipTier`. `m17_membership.md` çekirdek not. Tarih 2026-07-18.
- [ ] commit `docs: öğrenci Free/Premium kapıları (Ö-D)`.

## Self-Review
- `MembershipGate` saf + testli. Tier `Shared/Contracts` üzerinden okunur (izolasyon). Free geniş (streak tam + 30 gün) — kullanıcı kararıyla uyumlu.
- **Bağımlılık:** Streak dondurma kapısı Ö-A dondurma özelliğine bağlı; o gelmeden dondurma gate'i no-op bırakılabilir.
- M17 tam modülü ayrı; bu dilim tier depolamayı Students'ta hafif tutar, sonra taşınabilir.
