using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Parents.Domain;

/// <summary>
/// Çocuk (öğrenci) başına denormalize gelişim özeti. Diğer modüllerin integration event'leri ile beslenir
/// (M05 LessonSessions, M06 Assignments, M07 Payments; M08 Study geldiğinde çalışma verisi eklenir).
/// Veli paneli bu tabloyu okur — doğrudan cross-module DB erişimi yoktur (modül sınırı kuralı).
/// </summary>
public sealed class ChildProgressSnapshot : Entity<Guid>
{
    private ChildProgressSnapshot()
    {
    }

    public ChildProgressSnapshot(Guid id, Guid studentId, DateTime updatedOnUtc)
    {
        Id = id;
        StudentId = studentId;
        Currency = "TRY";
        UpdatedOnUtc = updatedOnUtc;
    }

    public Guid StudentId { get; private set; }

    // M05 LessonSessions
    public int PlannedLessonCount { get; private set; }

    public int CompletedLessonCount { get; private set; }

    public DateTime? LastLessonCompletedAtUtc { get; private set; }

    // M06 Assignments
    public int TotalAssignmentCount { get; private set; }

    public int OpenAssignmentCount { get; private set; }

    public int CompletedAssignmentCount { get; private set; }

    // M07 Payments
    public string Currency { get; private set; } = "TRY";

    public decimal ExpectedPaymentTotal { get; private set; }

    public decimal CollectedPaymentTotal { get; private set; }

    public decimal OutstandingPaymentTotal { get; private set; }

    public DateTime? LastPaymentUpdatedAtUtc { get; private set; }

    // M08 Study (henüz veri yok — modül geldiğinde beslenecek)
    public int WeeklyStudyMinutes { get; private set; }

    public int StudyStreakDays { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void RegisterPlannedLesson(DateTime updatedOnUtc)
    {
        PlannedLessonCount++;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void RegisterCompletedLesson(DateTime completedAtUtc, DateTime updatedOnUtc)
    {
        CompletedLessonCount++;
        if (LastLessonCompletedAtUtc is null || completedAtUtc > LastLessonCompletedAtUtc)
        {
            LastLessonCompletedAtUtc = completedAtUtc;
        }

        UpdatedOnUtc = updatedOnUtc;
    }

    public void RegisterAssignmentCreated(DateTime updatedOnUtc)
    {
        TotalAssignmentCount++;
        OpenAssignmentCount++;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void RegisterAssignmentCompleted(DateTime updatedOnUtc)
    {
        CompletedAssignmentCount++;
        if (OpenAssignmentCount > 0)
        {
            OpenAssignmentCount--;
        }

        UpdatedOnUtc = updatedOnUtc;
    }

    public void RegisterPaymentCreated(decimal expectedAmount, string currency, bool isSettled, DateTime updatedOnUtc)
    {
        Currency = string.IsNullOrWhiteSpace(currency) ? Currency : currency;
        ExpectedPaymentTotal += expectedAmount;
        if (!isSettled)
        {
            OutstandingPaymentTotal += expectedAmount;
        }
        else
        {
            CollectedPaymentTotal += expectedAmount;
        }

        LastPaymentUpdatedAtUtc = updatedOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void RegisterPaymentUpdated(decimal collectedDelta, DateTime updatedOnUtc)
    {
        if (collectedDelta != 0)
        {
            CollectedPaymentTotal += collectedDelta;
            OutstandingPaymentTotal -= collectedDelta;
            if (OutstandingPaymentTotal < 0)
            {
                OutstandingPaymentTotal = 0;
            }
        }

        LastPaymentUpdatedAtUtc = updatedOnUtc;
        UpdatedOnUtc = updatedOnUtc;
    }
}

/// <summary>
/// Öğrenci profili → login kullanıcı eşlemesi. Yalnızca <c>StudentProfileCreatedDomainEvent</c> ile beslenir.
/// Veli–çocuk bağ onayında "öğrenci kendi bağını onaylıyor mu" yetki kontrolü için kullanılır (fail-closed).
/// </summary>
public sealed class KnownStudent : Entity<Guid>
{
    private KnownStudent()
    {
    }

    public KnownStudent(Guid id, Guid studentId, Guid? userId, DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        UserId = userId;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;
    }

    public Guid StudentId { get; private set; }

    public Guid? UserId { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void SetUserId(Guid? userId, DateTime updatedOnUtc)
    {
        UserId = userId;
        UpdatedOnUtc = updatedOnUtc;
    }
}
