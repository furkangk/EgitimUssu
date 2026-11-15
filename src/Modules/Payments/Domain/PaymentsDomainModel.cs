using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Payments.Domain;

public sealed class PaymentRecord : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

    private PaymentRecord()
    {
    }

    public PaymentRecord(
        Guid id,
        Guid teacherUserId,
        Guid studentId,
        Guid? relatedLessonSessionId,
        BillingItemType itemType,
        string description,
        string currency,
        decimal expectedAmount,
        decimal collectedAmount,
        DateTime dueDateUtc,
        DateTime? collectedOnUtc,
        PaymentStatus status,
        DateTime? billingPeriodStartUtc,
        DateTime? billingPeriodEndUtc,
        string? notes,
        DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        RelatedLessonSessionId = relatedLessonSessionId;
        ItemType = itemType;
        Description = description;
        Currency = currency;
        ExpectedAmount = expectedAmount;
        CollectedAmount = collectedAmount;
        DueDateUtc = dueDateUtc;
        CollectedOnUtc = collectedOnUtc;
        Status = status;
        BillingPeriodStartUtc = billingPeriodStartUtc;
        BillingPeriodEndUtc = billingPeriodEndUtc;
        Notes = notes;

        Raise(new PaymentRecordCreatedDomainEvent(
            Id,
            TeacherUserId,
            StudentId,
            RelatedLessonSessionId,
            ExpectedAmount,
            Currency,
            Status,
            createdOnUtc));
    }

    public Guid? LessonScheduleId { get; private set; }
    public DateTime? OccurrenceStartUtc { get; private set; }
    public void LinkCancellationOccurrence(Guid scheduleId, DateTime key)
    {
        LessonScheduleId = scheduleId;
        OccurrenceStartUtc = key;
    }
    public bool CancelOutstanding(DateTime nowUtc)
    {
        if (CollectedAmount != 0m || Status is not (PaymentStatus.Pending or PaymentStatus.Overdue)) return false;
        var previous = Status;
        Status = PaymentStatus.Cancelled;
        Raise(new PaymentRecordUpdatedDomainEvent(Id, TeacherUserId, StudentId, previous, Status, 0m, 0m, nowUtc));
        return true;
    }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid? RelatedLessonSessionId { get; private set; }

    public BillingItemType ItemType { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string Currency { get; private set; } = "TRY";

    public decimal ExpectedAmount { get; private set; }

    public decimal CollectedAmount { get; private set; }

    public DateTime DueDateUtc { get; private set; }

    public DateTime? CollectedOnUtc { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTime? BillingPeriodStartUtc { get; private set; }

    public DateTime? BillingPeriodEndUtc { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// E-07: Ödeme kaydını kimliksizleştirir — <b>tutar, para birimi, vade ve durum korunur</b>.
    /// Ticari kayıt yasal saklama süresince (TTK m.82) durmalıdır; silinen yalnız serbest metin
    /// alanlarıdır, çünkü oraya kişi adı yazılmış olabilir.
    /// </summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        var changed = false;

        if (Description != PersonalDataPlaceholder.RemovedContent)
        {
            Description = PersonalDataPlaceholder.RemovedContent;
            changed = true;
        }

        if (Notes is not null)
        {
            Notes = null;
            changed = true;
        }

        return changed;
    }

    /// <summary>M07-1: Kaydın <see cref="PaymentStatus.Overdue"/> işaretlendiği an; hiç işaretlenmediyse <c>null</c>.</summary>
    public DateTime? OverdueMarkedOnUtc { get; private set; }

    public void UpdateManualTracking(
        Guid? relatedLessonSessionId,
        BillingItemType itemType,
        string description,
        string currency,
        decimal expectedAmount,
        decimal collectedAmount,
        DateTime dueDateUtc,
        DateTime? collectedOnUtc,
        PaymentStatus status,
        DateTime? billingPeriodStartUtc,
        DateTime? billingPeriodEndUtc,
        string? notes,
        DateTime updatedOnUtc)
    {
        var previousStatus = Status;
        var previousCollectedAmount = CollectedAmount;

        RelatedLessonSessionId = relatedLessonSessionId;
        ItemType = itemType;
        Description = description;
        Currency = currency;
        ExpectedAmount = expectedAmount;
        CollectedAmount = collectedAmount;
        DueDateUtc = dueDateUtc;
        CollectedOnUtc = collectedOnUtc;
        Status = status;
        BillingPeriodStartUtc = billingPeriodStartUtc;
        BillingPeriodEndUtc = billingPeriodEndUtc;
        Notes = notes;

        Raise(new PaymentRecordUpdatedDomainEvent(
            Id,
            TeacherUserId,
            StudentId,
            previousStatus,
            Status,
            previousCollectedAmount,
            CollectedAmount,
            updatedOnUtc));
    }

    /// <summary>
    /// M07-1: Vadesi geçmiş ve hâlâ açık (<see cref="PaymentStatus.Pending"/> / <see cref="PaymentStatus.PartiallyPaid"/>)
    /// bir kaydı <see cref="PaymentStatus.Overdue"/> işaretler ve <see cref="PaymentBecameOverdueDomainEvent"/> yayar.
    /// Tarama turu her saat çalıştığı için işlem <b>idempotenttir</b>: zaten Overdue/Paid/Cancelled olan kayıt
    /// ya da vadesi gelmemiş kayıt değişmez ve olay üretilmez. Geçiş yapılmaması bir hata değildir —
    /// bu yüzden <c>Result</c> değil <c>bool</c> döner.
    /// </summary>
    /// <param name="nowUtc">Tarama anı (UTC).</param>
    /// <returns>Durum değiştiyse <c>true</c>.</returns>
    public bool MarkOverdue(DateTime nowUtc)
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.PartiallyPaid))
        {
            return false;
        }

        if (DueDateUtc >= nowUtc)
        {
            return false;
        }

        Status = PaymentStatus.Overdue;
        OverdueMarkedOnUtc = nowUtc;

        Raise(new PaymentBecameOverdueDomainEvent(
            Id,
            TeacherUserId,
            StudentId,
            ExpectedAmount - CollectedAmount,
            Currency,
            DueDateUtc,
            nowUtc));

        return true;
    }

    /// <summary>Tam ödeme beyanı yalnızca açık kaydın güncel kalan tutarını kapatabilir.</summary>
    public Result ValidateParentConfirmation(decimal declaredAmount)
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.PartiallyPaid or PaymentStatus.Overdue)
            || ExpectedAmount <= CollectedAmount)
        {
            return Result.Failure(new Error("payments.record_not_outstanding", "Bu ödeme kaydının açık bakiyesi yok."));
        }

        return declaredAmount > 0m && declaredAmount == ExpectedAmount - CollectedAmount
            ? Result.Success()
            : Result.Failure(new Error("payments.declaration_amount_mismatch", "Beyan tutarı ödeme kaydının güncel kalan tutarına eşit olmalıdır."));
    }

    /// <summary>Geçerli veli beyanı öğretmence teyit edilince kaydı tam tahsil edilmiş işaretler (Veli V-G).</summary>
    public Result MarkCollectedByParentConfirmation(decimal declaredAmount, DateTime nowUtc)
    {
        var validation = ValidateParentConfirmation(declaredAmount);
        if (validation.IsFailure)
        {
            return validation;
        }

        var previousStatus = Status;
        var previousCollected = CollectedAmount;

        Status = PaymentStatus.Paid;
        CollectedAmount = ExpectedAmount;
        CollectedOnUtc = nowUtc;

        Raise(new PaymentRecordUpdatedDomainEvent(Id, TeacherUserId, StudentId, previousStatus, Status, previousCollected, CollectedAmount, nowUtc));
        return Result.Success();
    }
}

public enum BillingItemType
{
    LessonFee = 1,
    MonthlyPackage = 2,
    ManualAdjustment = 3,
    LateCancellation = 4
}

public enum PaymentStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

public sealed record PaymentRecordCreatedDomainEvent(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    Guid StudentId,
    Guid? RelatedLessonSessionId,
    decimal ExpectedAmount,
    string Currency,
    PaymentStatus Status,
    DateTime CreatedOnUtc) : DomainEvent;

/// <summary>
/// M07-1: Bir ödeme kaydı vadesi geçtiği için <see cref="PaymentStatus.Overdue"/> oldu.
/// Notifications tüketip öğretmene (ve Premium veliye) bildirim üretir.
/// </summary>
public sealed record PaymentBecameOverdueDomainEvent(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    Guid StudentId,
    decimal OutstandingAmount,
    string Currency,
    DateTime DueDateUtc,
    DateTime MarkedOnUtc) : DomainEvent;

public sealed record PaymentRecordUpdatedDomainEvent(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    Guid StudentId,
    PaymentStatus PreviousStatus,
    PaymentStatus CurrentStatus,
    decimal PreviousCollectedAmount,
    decimal CurrentCollectedAmount,
    DateTime UpdatedOnUtc) : DomainEvent;

/// <summary>
/// Velinin bir ödeme kaydı için "ödedim" beyanı (Veli V-G). Para transferi değil, mutabakat kaydı: veli beyan eder →
/// öğretmene bildirim → öğretmen teyit edince <see cref="PaymentRecord.MarkCollectedByParentConfirmation"/> çağrılır.
/// Durum: Declared → Confirmed / Rejected.
/// </summary>
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

    /// <summary>E-07: Veli beyanındaki serbest metni siler; tutar ve durum korunur.</summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (Note is null)
        {
            return false;
        }

        Note = null;
        return true;
    }
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
