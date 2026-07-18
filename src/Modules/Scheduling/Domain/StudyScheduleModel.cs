using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Domain;

/// <summary>
/// Öğrencinin kendi oluşturduğu kişisel ders/çalışma programı girdisi. Öğretmenin planladığı
/// <see cref="LessonSchedule"/>'dan bağımsızdır: öğrenci bir öğretmeni olmadan da kendi haftalık
/// programını (ör. her Pazartesi 15:00-16:00 Matematik) oluşturabilir. Sahiplik <c>StudentId</c>
/// üzerindendir; yalnızca girdinin sahibi öğrenci (veya admin) yönetebilir.
/// </summary>
public sealed class StudyScheduleEntry : AggregateRoot<Guid>
{
    private StudyScheduleEntry()
    {
    }

    public StudyScheduleEntry(
        Guid id,
        Guid studentId,
        string subject,
        string? topic,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string timeZone,
        string? recurrenceRule,
        int reminderOffsetMinutes,
        string? colorHex,
        string? notes,
        DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        Subject = subject;
        Topic = topic;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        TimeZone = timeZone;
        RecurrenceRule = recurrenceRule;
        ReminderOffsetMinutes = reminderOffsetMinutes;
        ColorHex = colorHex;
        Notes = notes;
        Status = StudyScheduleEntryStatus.Active;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new StudyScheduleEntryScheduledDomainEvent(
            Id, StudentId, Subject, StartAtUtc, EndAtUtc, ReminderOffsetMinutes, createdOnUtc));
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string? Topic { get; private set; }

    public DateTime StartAtUtc { get; private set; }

    public DateTime EndAtUtc { get; private set; }

    public string TimeZone { get; private set; } = string.Empty;

    /// <summary>iCal benzeri tekrar kuralı (ör. <c>FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=...</c>). Boşsa tek seferlik.</summary>
    public string? RecurrenceRule { get; private set; }

    public int ReminderOffsetMinutes { get; private set; }

    /// <summary>Takvimde renk kodlaması için hex (ör. <c>#20A4A9</c>). Opsiyonel.</summary>
    public string? ColorHex { get; private set; }

    public string? Notes { get; private set; }

    public StudyScheduleEntryStatus Status { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public bool IsActive => Status == StudyScheduleEntryStatus.Active;

    public void UpdateDetails(
        string subject,
        string? topic,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string timeZone,
        string? recurrenceRule,
        int reminderOffsetMinutes,
        string? colorHex,
        string? notes,
        DateTime updatedOnUtc)
    {
        Subject = subject;
        Topic = topic;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        TimeZone = timeZone;
        RecurrenceRule = recurrenceRule;
        ReminderOffsetMinutes = reminderOffsetMinutes;
        ColorHex = colorHex;
        Notes = notes;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new StudyScheduleEntryRescheduledDomainEvent(
            Id, StudentId, Subject, StartAtUtc, EndAtUtc, ReminderOffsetMinutes, updatedOnUtc));
    }

    public void Cancel(DateTime updatedOnUtc)
    {
        Status = StudyScheduleEntryStatus.Cancelled;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new StudyScheduleEntryCancelledDomainEvent(Id, StudentId, updatedOnUtc));
    }
}

public enum StudyScheduleEntryStatus
{
    Active = 1,
    Cancelled = 2
}

public sealed record StudyScheduleEntryScheduledDomainEvent(
    Guid StudyScheduleEntryId,
    Guid StudentId,
    string Subject,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    int ReminderOffsetMinutes,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record StudyScheduleEntryRescheduledDomainEvent(
    Guid StudyScheduleEntryId,
    Guid StudentId,
    string Subject,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    int ReminderOffsetMinutes,
    DateTime UpdatedOnUtc) : DomainEvent;

public sealed record StudyScheduleEntryCancelledDomainEvent(
    Guid StudyScheduleEntryId,
    Guid StudentId,
    DateTime CancelledOnUtc) : DomainEvent;
