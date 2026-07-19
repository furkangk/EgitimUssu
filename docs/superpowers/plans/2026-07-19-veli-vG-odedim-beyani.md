# Veli V-G — "Ödedim" Beyanı (Öğretmen Teyitli) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Veli bir ödeme kaydı için "ödedim" **beyan** edebilsin → öğretmene bildirim → öğretmen **teyit** edince kayıt "tahsil edildi" olsun. Para transferi değil, mutabakat kaydı (PRD "platform para tahsil etmez" kuralı korunur).

**Architecture:** Mevcut `LessonChangeRequest` (talep→sonuçla) deseninin ödeme karşılığı: yeni `ParentPaymentDeclaration : AggregateRoot<Guid>` (Payments modülünde). Veli `DeclarePaymentPaidCommand` (veli-yetkili) açar → `ParentPaymentDeclaredDomainEvent` (öğretmen bildirimi V-E'ye); öğretmen `ConfirmPaymentDeclarationCommand` (öğretmen-yetkili) → `PaymentRecord.MarkCollectedByParentConfirmation(...)` çağrılır (Status=Paid, CollectedAmount=ExpectedAmount, CollectedOnUtc). Veli yetkisi yeni `IParentAccessDirectory` (`Shared.Contracts`, Parents uygular) ile doğrulanır. **Karar (2026-07-19):** beyan öğretmen teyidine bağlı (direkt kapanmaz).

**Tech Stack:** .NET 9, EF Core (`payments` şeması), CQRS, xUnit. Cross-module: `IParentAccessDirectory` (parent↔öğrenci onaylı bağ kontrolü).

## Global Constraints
- Migration (Payments): `dotnet ef migrations add AddParentPaymentDeclarations --project src/Modules/Payments/Infrastructure --startup-project src/API.Host --context PaymentsDbContext`
- Build: `dotnet build EgitimUssu.slnx` · Test: `dotnet test tests/Unit/EgitimUssu.Tests.Unit.csproj`
- Commit sonu: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Reuse: `LessonChangeRequest` durum makinesi deseni; `PaymentRecord.UpdateManualTracking` mantığı (yeni dedike metot ile).

## File Structure
- `src/Shared/Contracts/ParentAccessContract.cs` *(yeni)* — `IParentAccessDirectory.IsApprovedParentOfStudentAsync`.
- `src/Modules/Parents/Infrastructure/ParentAccessDirectory.cs` *(yeni)* + Parents DI.
- `src/Modules/Payments/Domain/PaymentsDomainModel.cs` — `PaymentRecord.MarkCollectedByParentConfirmation` + `ParentPaymentDeclaration` aggregate + `enum ParentPaymentDeclarationStatus` + eventler.
- `src/Modules/Payments/Application/ParentPaymentDeclarationFeatures.cs` *(yeni)* — komutlar/handler/repo/authorizer.
- `src/Modules/Payments/Infrastructure/*` — repo, DbContext DbSet+config, DI, migration.
- `src/Modules/Payments/API/PaymentsModule.cs` — endpoint'ler.
- Test: `tests/Unit/ParentPaymentDeclarationTests.cs`.

---

### Task 1: PaymentRecord — teyitli tahsil metodu

**Files:** `src/Modules/Payments/Domain/PaymentsDomainModel.cs`, Test: `tests/Unit/ParentPaymentDeclarationTests.cs` (yeni).

**Interfaces:**
- Produces: `PaymentRecord.MarkCollectedByParentConfirmation(DateTime nowUtc)` — `Status=Paid`, `CollectedAmount=ExpectedAmount`, `CollectedOnUtc=nowUtc`, `PaymentRecordUpdatedDomainEvent` yayar (mevcut event; snapshot projeksiyonu bozulmaz).

- [ ] **Step 1: Failing test**:

```csharp
using EgitimUssu.Modules.Payments.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentPaymentDeclarationTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private static PaymentRecord NewPending()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            BillingItemType.LessonFee, "Ders", "TRY", expectedAmount: 500m, collectedAmount: 0m,
            dueDateUtc: Now, collectedOnUtc: null, PaymentStatus.Pending,
            billingPeriodStartUtc: null, billingPeriodEndUtc: null, notes: null, createdOnUtc: Now);

    [Fact]
    public void MarkCollectedByParentConfirmation_SetsPaidFull()
    {
        var p = NewPending();
        p.MarkCollectedByParentConfirmation(Now.AddDays(1));
        Assert.Equal(PaymentStatus.Paid, p.Status);
        Assert.Equal(p.ExpectedAmount, p.CollectedAmount);
        Assert.Equal(Now.AddDays(1), p.CollectedOnUtc);
    }
}
```

> Not: `PaymentRecord` ctor argüman sırasını gerçek imzayla doğrula (teacherUserId, studentId, relatedLessonSessionId, itemType, description, currency, expectedAmount, collectedAmount, dueDateUtc, collectedOnUtc, status, billingPeriodStartUtc, billingPeriodEndUtc, notes, createdOnUtc).

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — `PaymentsDomainModel.cs`, `UpdateManualTracking`'ten sonra ekle:

```csharp
    /// <summary>Veli beyanı öğretmence teyit edilince kaydı tam tahsil edilmiş işaretler (Veli V-G).</summary>
    public void MarkCollectedByParentConfirmation(DateTime nowUtc)
    {
        var previousStatus = Status;
        var previousCollected = CollectedAmount;

        Status = PaymentStatus.Paid;
        CollectedAmount = ExpectedAmount;
        CollectedOnUtc = nowUtc;

        Raise(new PaymentRecordUpdatedDomainEvent(Id, TeacherUserId, StudentId, previousStatus, Status, previousCollected, CollectedAmount, nowUtc));
    }
```

- [ ] **Step 4: Run → PASS.** (Commit Task 2 ile birlikte.)

---

### Task 2: `ParentPaymentDeclaration` aggregate

**Files:** `PaymentsDomainModel.cs`, Test: `ParentPaymentDeclarationTests.cs` (ekleme).

**Interfaces:**
- Produces: `ParentPaymentDeclaration : AggregateRoot<Guid>` (`Id, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, DeclaredAmount, Note?, Status, CreatedOnUtc, ResolvedOnUtc?`). `enum ParentPaymentDeclarationStatus { Declared=1, Confirmed=2, Rejected=3 }`. `Confirm(now)`/`Reject(now)` yalnız `Declared`'dan (aksi `InvalidOperationException`). Ctor `ParentPaymentDeclaredDomainEvent`; sonuçta `ParentPaymentDeclarationResolvedDomainEvent`.

- [ ] **Step 1: Failing tests** — `ParentPaymentDeclarationTests.cs`'e ekle:

```csharp
    [Fact]
    public void Declare_RaisesDeclaredEvent_AndPending()
    {
        var d = new ParentPaymentDeclaration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, "havale", Now);
        Assert.Equal(ParentPaymentDeclarationStatus.Declared, d.Status);
        Assert.Contains(d.DomainEvents, e => e is ParentPaymentDeclaredDomainEvent);
    }

    [Fact]
    public void Confirm_OnlyFromDeclared()
    {
        var d = new ParentPaymentDeclaration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, null, Now);
        d.Reject(Now);
        Assert.Throws<InvalidOperationException>(() => d.Confirm(Now));
    }
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — `PaymentsDomainModel.cs` sonuna ekle:

```csharp
public sealed class ParentPaymentDeclaration : AggregateRoot<Guid>
{
    private ParentPaymentDeclaration() { }

    public ParentPaymentDeclaration(Guid id, Guid paymentRecordId, Guid parentUserId, Guid teacherUserId, Guid studentId, decimal declaredAmount, string? note, DateTime createdOnUtc)
    {
        Id = id;
        PaymentRecordId = paymentRecordId;
        ParentUserId = parentUserId;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        DeclaredAmount = declaredAmount;
        Note = note?.Trim();
        Status = ParentPaymentDeclarationStatus.Declared;
        CreatedOnUtc = createdOnUtc;

        Raise(new ParentPaymentDeclaredDomainEvent(Id, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, DeclaredAmount, createdOnUtc));
    }

    public Guid PaymentRecordId { get; private set; }
    public Guid ParentUserId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public Guid StudentId { get; private set; }
    public decimal DeclaredAmount { get; private set; }
    public string? Note { get; private set; }
    public ParentPaymentDeclarationStatus Status { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ResolvedOnUtc { get; private set; }

    public void Confirm(DateTime nowUtc) => Resolve(ParentPaymentDeclarationStatus.Confirmed, nowUtc);
    public void Reject(DateTime nowUtc) => Resolve(ParentPaymentDeclarationStatus.Rejected, nowUtc);

    private void Resolve(ParentPaymentDeclarationStatus status, DateTime nowUtc)
    {
        if (Status != ParentPaymentDeclarationStatus.Declared)
        {
            throw new InvalidOperationException("Yalnızca beyan edilmiş (Declared) bir ödeme beyanı sonuçlandırılabilir.");
        }

        Status = status;
        ResolvedOnUtc = nowUtc;
        Raise(new ParentPaymentDeclarationResolvedDomainEvent(Id, PaymentRecordId, ParentUserId, TeacherUserId, StudentId, status, nowUtc));
    }
}

public enum ParentPaymentDeclarationStatus { Declared = 1, Confirmed = 2, Rejected = 3 }

public sealed record ParentPaymentDeclaredDomainEvent(Guid DeclarationId, Guid PaymentRecordId, Guid ParentUserId, Guid TeacherUserId, Guid StudentId, decimal DeclaredAmount, DateTime CreatedOnUtc) : DomainEvent;
public sealed record ParentPaymentDeclarationResolvedDomainEvent(Guid DeclarationId, Guid PaymentRecordId, Guid ParentUserId, Guid TeacherUserId, Guid StudentId, ParentPaymentDeclarationStatus Status, DateTime ResolvedOnUtc) : DomainEvent;
```

- [ ] **Step 4: Run → PASS ; Commit** `feat(payments): ödeme beyanı + teyitli tahsil domaini (Veli V-G)`.

---

### Task 3: `IParentAccessDirectory` + Application + Infra + migration

**Files:** `src/Shared/Contracts/ParentAccessContract.cs` (yeni), `src/Modules/Parents/Infrastructure/ParentAccessDirectory.cs` (yeni) + Parents DI, `ParentPaymentDeclarationFeatures.cs` (yeni), Payments DbContext/repo/DI, migration.

**Interfaces:**
- Produces: `IParentAccessDirectory.IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken) → bool`.
- Produces: `DeclarePaymentPaidCommand(Guid ParentUserId, Guid PaymentRecordId, decimal DeclaredAmount, string? Note)`, `ConfirmPaymentDeclarationCommand(Guid DeclarationId)`, `RejectPaymentDeclarationCommand(Guid DeclarationId)` → `Result<ParentPaymentDeclarationResponse>`. `IParentPaymentDeclarationRepository` (Add/GetById/ListForTeacher/SaveChanges).

- [ ] **Step 1: Contract + Parents impl** — `src/Shared/Contracts/ParentAccessContract.cs`:

```csharp
namespace EgitimUssu.Shared.Contracts;

// Parents uygular; diğer modüller (Payments) velinin bir öğrencinin ONAYLI velisi olup olmadığını doğrular.
public interface IParentAccessDirectory
{
    Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken);
}
```

`ParentAccessDirectory.cs` (Parents.Infrastructure): `ParentsDbContext.ParentChildLinks.AnyAsync(l => l.ParentUserId == parentUserId && l.StudentId == studentId && l.Status == Approved)`. Parents DI: `services.AddScoped<IParentAccessDirectory, ParentAccessDirectory>();`.

- [ ] **Step 2: Application** — `ParentPaymentDeclarationFeatures.cs`:
  - `DeclarePaymentPaidCommandHandler`: `IPaymentRecordRepository.GetByIdAsync(PaymentRecordId)` → yoksa `payments.record_not_found`; beyanı oluştur (`TeacherUserId`/`StudentId` ödeme kaydından), kaydet. Validator: `DeclaredAmount > 0`.
  - `ConfirmPaymentDeclarationCommandHandler`: beyanı bul → yoksa `payments.declaration_not_found`; `Declared` değilse `payments.declaration_not_pending`; `declaration.Confirm(now)`; ödeme kaydını yükle + `payment.MarkCollectedByParentConfirmation(now)`; kaydet.
  - `RejectPaymentDeclarationCommandHandler`: benzer, `declaration.Reject(now)` (ödeme kaydına dokunmaz).
  - Authorizer'lar: `DeclarePaymentPaidCommand` → veli-yetkili: `IParentAccessDirectory.IsApprovedParentOfStudentAsync(currentUser, payment.StudentId)`; `Confirm/Reject` → öğretmen-yetkili (`PaymentRecordAuthorizer.CanManageTeacher(declaration.TeacherUserId)` deseni). Yeni hata kodları `payments.declaration_not_found`(404), `payments.declaration_not_pending`(409).
  - `IParentPaymentDeclarationRepository`: `AddAsync`, `GetByIdAsync`, `ListForTeacherAsync(teacherUserId, onlyPending)`, `SaveChangesAsync`.

- [ ] **Step 3: Infra** — repo impl; `PaymentsDbContext`: `DbSet<ParentPaymentDeclaration>` + config (table `parent_payment_declarations`, `Note` maxlen 500, `Status` string maxlen 16, `DeclaredAmount` numeric(18,2), index `{TeacherUserId, Status}`, index `{PaymentRecordId}`); DI kayıtları; migration `AddParentPaymentDeclarations`.

- [ ] **Step 4: Build + migration + test** — `dotnet build`; migration üret; `dotnet test`. Beklenen: 0 hata, testler PASS.

- [ ] **Step 5: Commit** `feat(payments): ödeme beyanı oluştur/teyit/red + repo + IParentAccessDirectory (Veli V-G)`.

---

### Task 4: API endpoint'leri

**Files:** `src/Modules/Payments/API/PaymentsModule.cs`.

- [ ] **Step 1:** Endpoint'ler:
  - `POST /api/payments/records/{paymentRecordId:guid}/declare-paid` → `DeclarePaidRequest(decimal DeclaredAmount, string? Note)`, `currentUser.UserId` = ParentUserId → `DeclarePaymentPaidCommand`.
  - `GET /api/payments/teachers/{teacherUserId:guid}/payment-declarations?onlyPending=` → liste.
  - `POST /api/payments/payment-declarations/{declarationId:guid}/confirm` → `ConfirmPaymentDeclarationCommand`.
  - `POST /api/payments/payment-declarations/{declarationId:guid}/reject` → `RejectPaymentDeclarationCommand`.
  `ToHttpResult`'a `payments.declaration_not_found`→404, `payments.declaration_not_pending`→409 ekle.

- [ ] **Step 2: Build + test + commit** `feat(payments): ödeme beyanı endpoint'leri (Veli V-G)`.

---

### Task 5: Dokümantasyon
- [ ] `doc/modules/m07_payments.md`: `ParentPaymentDeclaration` + endpoint'ler + "veli beyan eder, öğretmen teyit eder → tahsil edildi" kuralı (para transferi değil).
- [ ] `doc/modules/00_genel_bakis.md` endpoint envanteri; `doc/modules/veri_modeli.md` ER + enum + `IParentAccessDirectory`; `doc/roles/veli.md` V-07.4 satırı (artık "ödedim" beyanı var).
- [ ] commit `docs: veli ödedim beyanı (Veli V-G)`.

## Self-Review
- **Spec coverage:** Spec V-G "veli beyan → öğretmen teyit → tahsil" → Task 1-4 karşılıyor. Öğretmen teyidi zorunlu (karar 2026-07-19).
- **Bağımlılık:** Öğretmene beyan bildiriminin teslimi V-E'ye ait (olay yayılır; V-E dinler). V-G bağımsız derlenir/test geçer.
- **Placeholder:** Task 3 Application ayrıntısı, kanıtlanmış `LessonChangeRequest` handler desenine yönlendiriliyor; novel domain (aggregate + `MarkCollectedByParentConfirmation`) + contract tam verildi.
- **Type consistency:** `PaymentRecordUpdatedDomainEvent` mevcut imzayla yayılır (Parents ödeme projeksiyonu `PreviousCollectedAmount`/`CurrentCollectedAmount` bekler — korunur).
