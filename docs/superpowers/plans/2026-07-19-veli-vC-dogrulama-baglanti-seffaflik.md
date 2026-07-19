# Veli V-C — Bağlantı Şeffaflığı + Birincil Veli Kısıtı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** "Sessizce bağlanma yok" ilkesini kod düzeyinde uygula: (1) bir öğrenciye **yalnızca tek birincil veli** olabilir — ikinci veli birincil olmak isterse mevcut birincil veli (veya admin) onaylamadıkça birincil olamaz; (2) bir bağlantı onaylandığında **şeffaflık bildirimi olayı** yayılır ("X hesabı veli olarak bağlandı") — bu olay V-E'deki veli-bildirim işleyicisi tarafından çocuğa ve mevcut veliye teslim edilir.

**Architecture:** Değişiklik Parents modülünde yoğunlaşır. Birincil-veli tekilliği domain + repository (`ListApprovedLinksForStudentAsync`) + handler seviyesinde uygulanır. Şeffaflık için mevcut `ParentChildLinkApprovedDomainEvent`'e ek olarak yeni bir `ParentLinkConnectionNoticeDomainEvent` yayılır (alıcılar: `StudentId` = çocuk, `ExistingPrimaryParentUserId?` = mevcut veli). Teslim V-E'ye aittir (bu plan yalnız olayı üretir — Parents bağımsız çalışır). **Karar (2026-07-19):** doğrulama seviyesi = bildirim şeffaflığı + birincil veli (öğretmen teyidi bu dilimde YOK).

**Tech Stack:** .NET 9, EF Core (`parents` şeması), CQRS, xUnit.

## Global Constraints
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Migration gerekmez (yeni alan yok; yalnız davranış + yeni domain event).
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- **Bağımlılık:** Şeffaflık bildiriminin fiili teslimi V-E planına aittir. Bu plan yalnız olayı yayar; V-E olmadan da derlenir/test geçer (olay outbox'a yazılır, dinleyeni V-E gelince eklenir).

## File Structure
- `src/Modules/Parents/Domain/ParentsDomainModel.cs` — `ParentLinkConnectionNoticeDomainEvent` + `Approve` imza değişikliği (mevcut birincil bilgisini alır).
- `src/Modules/Parents/Application/ParentFeatures.cs` — `ApproveChildLinkCommandHandler` birincil-veli kontrolü; `IParentRepository.ListApprovedLinksForStudentAsync`; yeni hata `parents.primary_exists`.
- `src/Modules/Parents/Infrastructure/ParentRepository.cs` — yeni repo metodu.
- `src/Modules/Parents/API/ParentsModule.cs` — `ToHttpResult`'a `parents.primary_exists`→409.
- Test: `tests/Unit/ParentPrimaryLinkTests.cs`.

---

### Task 1: Domain — bağlantı şeffaflık olayı

**Files:**
- Modify: `src/Modules/Parents/Domain/ParentsDomainModel.cs`
- Test: `tests/Unit/ParentPrimaryLinkTests.cs` *(yeni)*

**Interfaces:**
- Produces: `ParentLinkConnectionNoticeDomainEvent(Guid LinkId, Guid StudentId, Guid ConnectedParentUserId, Guid? ExistingPrimaryParentUserId, bool IsPrimaryContact, DateTime ConnectedOnUtc) : DomainEvent`.
- Produces: `ParentChildLink.Approve(Guid approvedByUserId, Guid? existingPrimaryParentUserId, DateTime nowUtc)` — onayda hem mevcut `ParentChildLinkApprovedDomainEvent` hem yeni `ParentLinkConnectionNoticeDomainEvent` yayar.

- [ ] **Step 1: Write the failing test** — `tests/Unit/ParentPrimaryLinkTests.cs`:

```csharp
using EgitimUssu.Modules.Parents.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentPrimaryLinkTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Approve_RaisesConnectionNotice_WithExistingPrimary()
    {
        var existingPrimary = Guid.NewGuid();
        var link = new ParentChildLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ayşe", "baba", null, false, Now);

        link.Approve(approvedByUserId: existingPrimary, existingPrimaryParentUserId: existingPrimary, Now);

        Assert.Contains(link.DomainEvents, e => e is ParentLinkConnectionNoticeDomainEvent);
        var notice = link.DomainEvents.OfType<ParentLinkConnectionNoticeDomainEvent>().Single();
        Assert.Equal(existingPrimary, notice.ExistingPrimaryParentUserId);
    }
}
```

- [ ] **Step 2: Run → FAIL**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ParentPrimaryLinkTests"`
Expected: FAIL — `Approve` üç argümanlı imza yok; `ParentLinkConnectionNoticeDomainEvent` yok.

- [ ] **Step 3: Add the event** — `ParentsDomainModel.cs`, mevcut `ParentChildLinkRevokedDomainEvent` kaydından sonra ekle:

```csharp
public sealed record ParentLinkConnectionNoticeDomainEvent(
    Guid LinkId,
    Guid StudentId,
    Guid ConnectedParentUserId,
    Guid? ExistingPrimaryParentUserId,
    bool IsPrimaryContact,
    DateTime ConnectedOnUtc) : DomainEvent;
```

- [ ] **Step 4: Change `Approve` signature** — `ParentChildLink.Approve`'u değiştir (mevcut çağrı yalnız `ApproveChildLinkCommandHandler`'da; Task 2'de güncellenecek):

```csharp
    public void Approve(Guid approvedByUserId, Guid? existingPrimaryParentUserId, DateTime nowUtc)
    {
        if (Status == ParentChildLinkStatus.Approved)
        {
            return;
        }

        Status = ParentChildLinkStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        LinkedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;

        Raise(new ParentChildLinkApprovedDomainEvent(Id, ParentUserId, StudentId, IsPrimaryContact, nowUtc));
        Raise(new ParentLinkConnectionNoticeDomainEvent(
            Id, StudentId, ParentUserId, existingPrimaryParentUserId, IsPrimaryContact, nowUtc));
    }
```

- [ ] **Step 5: Run → PASS**

Run: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj --filter "FullyQualifiedName~ParentPrimaryLinkTests"`
Expected: PASS. (Not: `ApproveChildLinkCommandHandler` derlemesi kırılır — Task 2'de düzeltilir; tam derleme Task 2 sonunda yeşil olur.)

- [ ] **Step 6: Commit** *(Task 2 ile birlikte; derleme Task 2'de tamamlanır — bu adımda commit atlanır)*

---

### Task 2: Birincil-veli tekilliği + handler

**Files:**
- Modify: `src/Modules/Parents/Application/ParentFeatures.cs`
- Modify: `src/Modules/Parents/Infrastructure/ParentRepository.cs`
- Modify: `src/Modules/Parents/API/ParentsModule.cs`
- Test: `tests/Unit/ParentPrimaryLinkTests.cs` (ekleme)

**Interfaces:**
- Consumes: `ParentChildLink.Approve(approvedByUserId, existingPrimaryParentUserId, nowUtc)` (Task 1).
- Produces: `IParentRepository.ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken) → IReadOnlyCollection<ParentChildLink>`; `ParentErrors.PrimaryExists = "parents.primary_exists"`.

- [ ] **Step 1: Add repo method to interface + impl** — `ParentFeatures.cs` `IParentRepository`'ye ekle:

```csharp
    Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken cancellationToken);
```

`ParentRepository.cs`'e implementasyon ekle:

```csharp
    public async Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken cancellationToken)
        => await _dbContext.ParentChildLinks
            .Where(l => l.StudentId == studentId && l.Status == ParentChildLinkStatus.Approved)
            .ToArrayAsync(cancellationToken);
```

(Gerekliyse `using EgitimUssu.Modules.Parents.Domain;` ve `Microsoft.EntityFrameworkCore` zaten var.)

- [ ] **Step 2: Add error** — `ParentErrors`'a ekle:

```csharp
    public static readonly Error PrimaryExists = new("parents.primary_exists", "Bu çocuğun zaten bir birincil velisi var; birincil bağ için mevcut birincil velinin (veya yöneticinin) onayı gerekir.");
```

- [ ] **Step 3: Update `ApproveChildLinkCommandHandler`** — mevcut birincil veliyi bul, birincil-veli kuralını uygula, `Approve`'a mevcut birincil bilgisini geçir:

```csharp
    public async Task<Result<ChildLinkResponse>> Handle(ApproveChildLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetLinkByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.LinkNotFound);
        }

        var approvedLinks = await _repository.ListApprovedLinksForStudentAsync(link.StudentId, cancellationToken);
        var existingPrimary = approvedLinks.FirstOrDefault(l => l.IsPrimaryContact && l.Id != link.Id);

        // Birincil-veli tekilliği: bu bağ birincil olacaksa ve zaten bir birincil veli varsa,
        // onaylayan kişi mevcut birincil veli değilse reddet (Admin authorizer'da zaten geçebilir).
        if (link.IsPrimaryContact && existingPrimary is not null && existingPrimary.ParentUserId != command.ApprovedByUserId)
        {
            return Result<ChildLinkResponse>.Failure(ParentErrors.PrimaryExists);
        }

        link.Approve(command.ApprovedByUserId, existingPrimary?.ParentUserId, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<ChildLinkResponse>.Success(link.ToResponse(null));
    }
```

> Not: Admin onayında `ApprovedByUserId` admin'in kullanıcı kimliğidir; kural admin'i de kısıtlar. İstenirse authorizer zaten admin'e izin verir ama iş kuralı birincil çakışmasını korur — admin bir birincil veli varken ikinci birincil oluşturamaz (veri tutarlılığı). Bu bilinçli.

- [ ] **Step 4: HTTP eşlemesi** — `ParentsModule.cs` `ToHttpResult` switch'ine ekle:

```csharp
            "parents.primary_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
```

- [ ] **Step 5: Add handler test** — `ParentPrimaryLinkTests.cs`'e ekle (sahte repo ile ikinci birincil reddini doğrula):

```csharp
    [Fact]
    public async Task Approve_SecondPrimary_ByNonPrimary_Fails()
    {
        var studentId = Guid.NewGuid();
        var firstPrimaryParent = Guid.NewGuid();
        var firstLink = new ParentChildLink(Guid.NewGuid(), firstPrimaryParent, studentId, "Ayşe", "anne", null, true, Now);
        firstLink.Approve(firstPrimaryParent, null, Now);

        var secondParent = Guid.NewGuid();
        var secondLink = new ParentChildLink(Guid.NewGuid(), secondParent, studentId, "Ayşe", "baba", null, true, Now);

        var repo = new FakeRepo(secondLink, new[] { firstLink });
        var handler = new ApproveChildLinkCommandHandler(repo, new FixedClock(Now));

        // İkinci bağı, mevcut birincil olmayan biri (kendisi) onaylatmaya çalışır:
        var result = await handler.Handle(new ApproveChildLinkCommand(secondLink.Id, secondParent), default);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.primary_exists", result.Error.Code);
    }
```

`FakeRepo` (yalnız gerekli üyeleri gerçek davranışla; diğerleri throw/empty) + `FixedClock : IClock` yardımcılarını test dosyasına ekle. `IParentRepository`'nin tüm üyeleri implement edilmeli; `GetLinkByIdAsync` → secondLink, `ListApprovedLinksForStudentAsync` → firstLink; diğerleri boş/`null`.

- [ ] **Step 6: Build + test → PASS**

Run: `dotnet build EgitimUssu.slnx` then `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
Expected: `0 Hata`; tüm testler PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Parents/ tests/Unit/ParentPrimaryLinkTests.cs
git commit -m "feat(parents): birincil veli tekilliği + bağlantı şeffaflık olayı (Veli V-C)"
```

---

### Task 3: Dokümantasyon
- [ ] `doc/modules/m09_parents.md`: "sessizce bağlanma yok" ilkesi; `ParentLinkConnectionNoticeDomainEvent` (alıcı: çocuk + mevcut veli, teslim V-E'de); birincil-veli tekilliği kuralı + `parents.primary_exists` (409). Tarih 2026-07-19.
- [ ] `doc/roles/veli.md`: birincil veli + bağlantı bildirimi satırı; yetki matrisi #14 ("haberdar edilmeden bağlanmaz") ✅.
- [ ] `doc/modules/veri_modeli.md`: yeni domain event satırı.
- [ ] commit: `docs: veli bağlantı şeffaflığı + birincil veli (Veli V-C)`

## Self-Review
- **Spec coverage:** Spec V-C "sessizce bağlanma yok + birincil veli" → Task 1 (olay) + Task 2 (tekillik) karşılıyor. Öğretmen teyidi bilinçle dışta (karar 2026-07-19).
- **Bağımlılık:** Şeffaflık bildiriminin teslimi V-E'ye ait; bu plan yalnız olayı yayar ve bağımsız derlenir/test geçer.
- **Placeholder:** Yok; `FakeRepo`/`FixedClock` test yardımcıları için gerçek `IParentRepository`/`IClock` imzalarına uy talimatı verildi.
- **Type consistency:** `Approve` yeni imzası (üç parametre) tek çağrı yerinde (`ApproveChildLinkCommandHandler`) güncellendi; domain testleri yeni imzayı kullanır.
