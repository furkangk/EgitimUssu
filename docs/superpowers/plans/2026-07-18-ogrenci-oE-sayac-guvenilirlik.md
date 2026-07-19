# Öğrenci Ö-E — Sayaç Güvenilirliği (Offline/Kurtarma) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use `- [ ]`.

**Goal:** Çalışma sayacı offline/arka planda güvenilir olsun; çökme sonrası takılı seans kurtarılabilsin; unutulmuş seans tespit edilsin (B-02/AKIŞ 4). Bu dilim **API tarafını** sağlar (mobil ayrı iş).

**Architecture:** M08 Study'ye additive. `Complete`/`Pause` opsiyonel **istemci-otoriter süre** (`clientEffectiveMinutes`) kabul eder; sunucu makul üst sınırla (elapsed + tolerans) doğrular. Takılı `Running` seans için `recover` endpoint'i. `Running` + uzun süre → `staleWarning`.

**Tech Stack:** .NET 9, EF Core, xUnit, CQRS, `IClock`.

## Global Constraints
- Build `dotnet build EgitimUssu.slnx` · Test `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Sahiplik: yeni komutlar mevcut `StudySessionOwnershipAuthorizer` desenine (kimlik=SessionId).

## File Structure
- `src/Modules/Study/Domain/StudyDomainModel.cs` — `StudySession.Complete/Pause` opsiyonel `clientEffectiveMinutes`; `RecoverStuck(...)`; `IsStale(nowUtc)`.
- `src/Modules/Study/Application/StudySessionFeatures.cs` — komut alanları + `RecoverStudySessionCommand` + `GetActiveSessionQuery`.
- `src/Modules/Study/Application/StudyContracts.cs` — `StudySessionResponse`'a `bool IsStale` (opsiyonel) + `ActiveSessionResponse`.
- `src/Modules/Study/API/StudyModule.cs` — `recover` + `active-session` endpoint'leri.
- Test: `tests/Unit/StudySessionReliabilityTests.cs`.

---

### Task 1: İstemci-otoriter süre + `IsStale`

**Files:** `StudyDomainModel.cs`, `tests/Unit/StudySessionReliabilityTests.cs`.

**Interfaces:** Produces: `StudySession.Complete(nowUtc, personalNote, int? clientEffectiveMinutes)` — `clientEffectiveMinutes` verilirse ve `0 < value ≤ elapsedMinutes + tolerans(2)` ise `EffectiveMinutes` bunu kullanır; aksi halde mevcut sunucu-hesabı. `StudySession.IsStale(nowUtc)` → `Status==Running && (nowUtc - (LastResumedAtUtc ?? StartedAtUtc)) > 6 saat`.

- [ ] **Step 1: Failing test** `StudySessionReliabilityTests`:
```csharp
using EgitimUssu.Modules.Study.Domain;
namespace EgitimUssu.Tests.Unit;
public sealed class StudySessionReliabilityTests
{
    private static readonly DateTime Start = new(2026,7,20,9,0,0,DateTimeKind.Utc);
    [Fact]
    public void Complete_UsesClientMinutes_WhenPlausible()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Start);
        // 40 dk sonra tamamla; istemci 38 dk bildirdi (offline birikmiş) → 38 kabul (≤ 40+2)
        s.Complete(Start.AddMinutes(40), null, clientEffectiveMinutes: 38);
        Assert.Equal(38, s.EffectiveMinutes);
    }
    [Fact]
    public void Complete_RejectsInflatedClientMinutes()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Start);
        s.Complete(Start.AddMinutes(40), null, clientEffectiveMinutes: 999); // > elapsed+2 → sunucu hesabı (~40)
        Assert.True(s.EffectiveMinutes <= 41);
    }
    [Fact]
    public void IsStale_TrueAfter6h()
    {
        var s = StudySession.StartStopwatch(Guid.NewGuid(), Guid.NewGuid(), "Mat", null, false, false, Start);
        Assert.True(s.IsStale(Start.AddHours(7)));
        Assert.False(s.IsStale(Start.AddHours(1)));
    }
}
```
- [ ] **Step 2: Run → FAIL** (`Complete` 3. param + `IsStale` yok).
- [ ] **Step 3:** `Complete(nowUtc, personalNote, int? clientEffectiveMinutes = null)` — mevcut hesap yapıldıktan sonra: `if (clientEffectiveMinutes is int c && c > 0 && c <= EffectiveMinutes + 2) EffectiveMinutes = c;` (EffectiveMinutes burada sunucu-hesabı = elapsed'e yakın). `IsStale(nowUtc)` metodu.
- [ ] **Step 4: Run → PASS + commit** `feat(study): istemci-otoriter süre + IsStale (Ö-E/B8)`.

---

### Task 2: `recover` + `active-session` endpoint'leri

**Files:** `StudySessionFeatures.cs`, `StudyContracts.cs`, `StudyModule.cs`, `DependencyInjection.cs`; test.

**Interfaces:** Produces: `StudySession.RecoverStuck(effectiveMinutes, nowUtc)` — `Running/Paused` seansı `Completed` yapar, `EffectiveMinutes = max(0, effectiveMinutes)`. `RecoverStudySessionCommand(SessionId, int EffectiveMinutes)`. `GetActiveSessionQuery(StudentId)` → `ActiveSessionResponse?(Session, IsStale)`. Endpoint `POST /students/{studentId}/sessions/{sessionId}/recover`, `GET /students/{studentId}/active-session`.

- [ ] **Step 1: Failing test** — handler seviyesinde: takılı `Running` seans `recover` ile `Completed` + verilen dakika; `active-session` seansı + `IsStale` döndürür (sahte repo).
- [ ] **Step 2: Run → FAIL.**
- [ ] **Step 3:** `RecoverStuck` domain + `RecoverStudySessionCommand`/handler (`GetSessionAsync` → `RecoverStuck` → `StudyCompletionService.RecordCompletedAsync`? Hayır — recover şüpheli süre; rollup/streak'e dahil et ama `RecordCompletedAsync` çağır). `GetActiveSessionQuery`/handler (`GetActiveSessionAsync` → response + `IsStale(now)`). Ownership authorizer'a `RecoverStudySessionCommand` + `GetActiveSessionQuery` ekle.
- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5:** `StudyContracts` `ActiveSessionResponse`; `StudyModule` endpoint'leri + DI; `Complete`/`Pause` API request'lerine opsiyonel `clientEffectiveMinutes`.
- [ ] **Step 6:** build+test+commit `feat(study): takılı seans kurtarma + aktif seans sorgusu (Ö-E/B8)`.

---

### Task 3: Dokümantasyon
- [ ] `m08_study.md`: istemci-otoriter süre + recover + stale kuralı; `doc/roles/ogrenci.md` §9 sayaç güvenilirliği kabul kriteri. `00_genel_bakis.md` yeni endpoint'ler. Tarih 2026-07-18.
- [ ] commit `docs: öğrenci sayaç güvenilirliği (Ö-E)`.

## Self-Review
- Domain testli (istemci süre doğrulama + stale). Migration yok (davranış + opsiyonel alanlar). Şişirme koruması: `clientEffectiveMinutes ≤ elapsed+2`.
- **Kapsam:** API tarafı; asıl arka plan/offline mantığı **mobil** — ayrı iş (bu plan mobil kapsamaz).
