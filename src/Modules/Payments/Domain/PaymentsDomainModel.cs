using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Payments.Domain;

public sealed class PaymentRecord : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

    public const decimal MaximumAmount = 9999999999999999.99m;
    public static bool IsValidAmount(decimal amount) => amount >= 0m && amount <= MaximumAmount && decimal.Round(amount, 2) == amount;
    private readonly List<PaymentTransaction> _transactions = [];
    public IReadOnlyCollection<PaymentTransaction> Transactions => _transactions;

    public Result AddCollection(Guid transactionId, decimal amount, PaymentMethod method, DateTime occurredOnUtc, string? note, DateTime nowUtc)
        => RecordTransaction(transactionId, PaymentTransactionKind.Collection, amount, method, occurredOnUtc, note, nowUtc);

    public Result Refund(Guid transactionId, decimal amount, PaymentMethod method, DateTime occurredOnUtc, string? note, DateTime nowUtc)
        => RecordTransaction(transactionId, PaymentTransactionKind.Refund, amount, method, occurredOnUtc, note, nowUtc);

    private Result RecordTransaction(Guid transactionId, PaymentTransactionKind kind, decimal amount, PaymentMethod method, DateTime occurredOnUtc, string? note, DateTime nowUtc)
    {
        if (Status == PaymentStatus.Cancelled)
            return Result.Failure(new Error("payments.record_cancelled", "İptal edilmiş ödeme kaydına hareket eklenemez."));
        if (transactionId == Guid.Empty || amount <= 0m || !IsValidAmount(amount)
            || !Enum.IsDefined(method) || occurredOnUtc == default || occurredOnUtc > nowUtc || note?.Length > 1000)
            return Result.Failure(new Error("payments.invalid_request", "Tahsilat bilgileri eksik veya hatalı."));
        if (kind == PaymentTransactionKind.Refund && amount > CollectedAmount)
            return Result.Failure(new Error("payments.refund_exceeds_collection", "İade net tahsilattan fazla olamaz."));
        if (kind == PaymentTransactionKind.Collection && CollectedAmount > MaximumAmount - amount)
            return Result.Failure(new Error("payments.invalid_request", "Net tahsilat üst sınırı aşamaz."));
        if (_transactions.Any(x => x.Id == transactionId))
            return Result.Failure(new Error("payments.transaction_exists", "Bu hareket zaten kaydedilmiş."));
        var previousStatus = Status;
        var previousCollected = CollectedAmount;
        _transactions.Add(new PaymentTransaction(transactionId, Id, kind, amount, method, occurredOnUtc, note?.Trim()));
        CollectedAmount = _transactions.Sum(x => x.Kind == PaymentTransactionKind.Collection ? x.Amount : -x.Amount);
        CollectedOnUtc = _transactions.Where(x => x.Kind == PaymentTransactionKind.Collection).Max(x => (DateTime?)x.OccurredOnUtc);
        RecalculateStatus();
        Raise(new PaymentTransactionRecordedIntegrationEvent(Id, transactionId, TeacherUserId, kind, amount, Currency, occurredOnUtc));
        Raise(new PaymentRecordUpdatedDomainEvent(Id, TeacherUserId, StudentId, previousStatus, Status, previousCollected, CollectedAmount, nowUtc));
        return Result.Success();
    }

    public Result Cancel(DateTime nowUtc)
    {
        if (CollectedAmount != 0m)
            return Result.Failure(new Error("payments.collection_requires_refund", "İptal etmeden önce tahsilatı iade edin."));
        if (Status == PaymentStatus.Cancelled) return Result.Success();
        var previous = Status;
        Status = PaymentStatus.Cancelled;
        Raise(new PaymentRecordUpdatedDomainEvent(Id, TeacherUserId, StudentId, previous, Status, 0m, 0m, nowUtc));
        return Result.Success();
    }

    public Result UpdateDetails(string description, decimal expectedAmount, DateTime dueDateUtc, string? notes, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length > 200 || expectedAmount < 0m
            || !IsValidAmount(expectedAmount) || dueDateUtc == default || notes?.Length > 1000)
            return Result.Failure(new Error("payments.invalid_request", "Ödeme ayrıntıları eksik veya hatalı."));
        var previous = Status;
        Description = description.Trim();
        ExpectedAmount = expectedAmount;
        if (DueDateUtc != dueDateUtc)
        {
            OverdueMarkedOnUtc = null;
            if (Status == PaymentStatus.Overdue) Status = PaymentStatus.Pending;
        }
        DueDateUtc = dueDateUtc;
        Notes = notes?.Trim();
        if (Status != PaymentStatus.Cancelled) RecalculateStatus();
        Raise(new PaymentRecordUpdatedDomainEvent(Id, TeacherUserId, StudentId, previous, Status, CollectedAmount, CollectedAmount, nowUtc));
        return Result.Success();
    }

    private void RecalculateStatus()
    {
        Status = CollectedAmount >= ExpectedAmount ? PaymentStatus.Paid
            : Status == PaymentStatus.Overdue ? PaymentStatus.Overdue
            : CollectedAmount > 0m ? PaymentStatus.PartiallyPaid : PaymentStatus.Pending;
    }

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
        if (!IsValidAmount(expectedAmount) || !IsValidAmount(collectedAmount))
            throw new ArgumentException("Ödeme tutarı geçerli ve iki ondalıklı olmalıdır.");
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
        Status = status == PaymentStatus.Cancelled ? PaymentStatus.Cancelled
            : collectedAmount >= expectedAmount ? PaymentStatus.Paid
            : collectedAmount > 0m ? PaymentStatus.PartiallyPaid : PaymentStatus.Pending;
        if (collectedAmount > 0m)
        {
            _transactions.Add(new PaymentTransaction(id, id, PaymentTransactionKind.Collection, collectedAmount,
                PaymentMethod.Other, collectedOnUtc ?? createdOnUtc, null));
            Raise(new PaymentTransactionRecordedIntegrationEvent(id, id, teacherUserId, PaymentTransactionKind.Collection,
                collectedAmount, currency, collectedOnUtc ?? createdOnUtc));
        }
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

        foreach (var transaction in _transactions) changed |= transaction.AnonymizePersonalData();
        return changed;
    }

    /// <summary>M07-1: Kaydın <see cref="PaymentStatus.Overdue"/> işaretlendiği an; hiç işaretlenmediyse <c>null</c>.</summary>
    public DateTime? OverdueMarkedOnUtc { get; private set; }

    /// <summary>
    /// M07-1: Vadesi geçmiş ve hâlâ açık (<see cref="PaymentStatus.Pending"/> / <see cref="PaymentStatus.PartiallyPaid"/>)
    /// bir kaydı <see cref="PaymentStatus.Overdue"/> işaretler ve <see cref="PaymentBecameOverdueDomainEvent"/> yayar.
    /// Tarama turu her saat çalıştığı için işlem <b>idempotenttir</b>: zaten Overdue/Paid/Cancelled olan kayıt
    /// ya da vadesi gelmemiş kayıt değişmez ve olay üretilmez. Geçiş yapılmaması bir hata değildir —
    /// bu yüzden <c>Result</c> değil <c>bool</c> döner.
    /// </summary>
    /// <param name="nowUtc">Tarama anı (UTC).</param>
    /// <param name="calendar">Yerel vade gününün takvimi.</param>
    /// <returns>Durum değiştiyse <c>true</c>.</returns>
    public bool MarkOverdue(DateTime nowUtc, ILocalCalendar calendar)
    {
        if (OverdueMarkedOnUtc.HasValue || Status is not (PaymentStatus.Pending or PaymentStatus.PartiallyPaid))
        {
            return false;
        }

        if (calendar.ToLocalDate(DueDateUtc) >= calendar.Today())
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
    public Result MarkCollectedByParentConfirmation(decimal declaredAmount, DateTime nowUtc, Guid? transactionId = null)
    {
        var validation = ValidateParentConfirmation(declaredAmount);
        if (validation.IsFailure)
        {
            return validation;
        }

        return AddCollection(transactionId ?? Id, declaredAmount, PaymentMethod.ParentDeclaration, nowUtc, null, nowUtc);
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
public sealed class ParentPaymentDeclaration : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

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

public enum PaymentTransactionKind { Collection = 1, Refund = 2 }
public enum PaymentMethod { Cash = 1, BankTransfer = 2, Card = 3, ParentDeclaration = 4, Other = 5, Migrated = 99 }

public sealed class PaymentTransaction : Entity<Guid>
{
    private PaymentTransaction() { }
    public PaymentTransaction(Guid id, Guid paymentRecordId, PaymentTransactionKind kind, decimal amount, PaymentMethod method, DateTime occurredOnUtc, string? note)
    {
        Id = id; PaymentRecordId = paymentRecordId; Kind = kind; Amount = amount;
        Method = method; OccurredOnUtc = occurredOnUtc; Note = note;
    }
    public bool AnonymizePersonalData()
    {
        if (Note is null) return false;
        Note = null;
        return true;
    }
    public Guid PaymentRecordId { get; private set; }
    public PaymentTransactionKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string? Note { get; private set; }
}

public sealed record PaymentTransactionRecordedIntegrationEvent : DomainEvent
{
    public PaymentTransactionRecordedIntegrationEvent(Guid paymentRecordId, Guid transactionId, Guid teacherUserId,
        PaymentTransactionKind kind, decimal amount, string currency, DateTime occurredOnUtc)
    {
        PaymentRecordId = paymentRecordId;
        TransactionId = transactionId;
        TeacherUserId = teacherUserId;
        Kind = kind;
        Amount = amount;
        Currency = currency;
        OccurredOnUtc = occurredOnUtc;
    }
    public Guid PaymentRecordId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid TeacherUserId { get; init; }
    public PaymentTransactionKind Kind { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
}
