using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Payments.Domain;

public sealed class PaymentRecord : AggregateRoot<Guid>
{
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
}

public enum BillingItemType
{
    LessonFee = 1,
    MonthlyPackage = 2,
    ManualAdjustment = 3
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

public sealed record PaymentRecordUpdatedDomainEvent(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    Guid StudentId,
    PaymentStatus PreviousStatus,
    PaymentStatus CurrentStatus,
    decimal PreviousCollectedAmount,
    decimal CurrentCollectedAmount,
    DateTime UpdatedOnUtc) : DomainEvent;
