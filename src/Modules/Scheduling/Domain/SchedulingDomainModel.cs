using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Domain;

public sealed class LessonSchedule : AggregateRoot<Guid>
{
    private LessonSchedule()
    {
    }

    public LessonSchedule(
        Guid id,
        Guid teacherUserId,
        Guid studentId,
        string subject,
        ScheduledLessonFormat lessonFormat,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string timeZone,
        string? recurrenceRule,
        LessonScheduleStatus status,
        int reminderOffsetMinutes,
        string? locationLabel,
        string? meetingUrl,
        string? notes,
        DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Subject = subject;
        LessonFormat = lessonFormat;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        TimeZone = timeZone;
        RecurrenceRule = recurrenceRule;
        Status = status;
        ReminderOffsetMinutes = reminderOffsetMinutes;
        LocationLabel = locationLabel;
        MeetingUrl = meetingUrl;
        Notes = notes;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new LessonScheduledDomainEvent(Id, TeacherUserId, StudentId, StartAtUtc, EndAtUtc, ReminderOffsetMinutes, createdOnUtc));
    }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public ScheduledLessonFormat LessonFormat { get; private set; }

    public DateTime StartAtUtc { get; private set; }

    public DateTime EndAtUtc { get; private set; }

    public string TimeZone { get; private set; } = string.Empty;

    public string? RecurrenceRule { get; private set; }

    public LessonScheduleStatus Status { get; private set; }

    public int ReminderOffsetMinutes { get; private set; }

    public string? LocationLabel { get; private set; }

    public string? MeetingUrl { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public DateTime? OriginalStartAtUtc { get; private set; }

    public string? RescheduleNote { get; private set; }

    public CancellationReason? CancellationReason { get; private set; }

    public bool IsChargeable { get; private set; }

    /// <summary>
    /// Yalnizca taslak/planli dersler duzenlenebilir; tamamlanmis veya iptal edilmis ders degistirilemez.
    /// </summary>
    public bool IsEditable => Status is LessonScheduleStatus.Planned or LessonScheduleStatus.Draft;

    /// <summary>
    /// Planli dersin bilgilerini gunceller ve hatirlatmanin yeniden planlanmasi icin olay yayar.
    /// </summary>
    public void UpdateDetails(
        string subject,
        ScheduledLessonFormat lessonFormat,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string timeZone,
        string? recurrenceRule,
        int reminderOffsetMinutes,
        string? locationLabel,
        string? meetingUrl,
        string? notes,
        DateTime updatedOnUtc)
    {
        Subject = subject;
        LessonFormat = lessonFormat;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        TimeZone = timeZone;
        RecurrenceRule = recurrenceRule;
        ReminderOffsetMinutes = reminderOffsetMinutes;
        LocationLabel = locationLabel;
        MeetingUrl = meetingUrl;
        Notes = notes;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonScheduleRescheduledDomainEvent(Id, TeacherUserId, StudentId, StartAtUtc, EndAtUtc, updatedOnUtc));
    }

    /// <summary>
    /// Dersi yeni tarih/saate taşır. Statü Planned kalır (ERTELENDİ geçici bir işaret değil, kayıtlı bir taşımadır).
    /// İlk ertelemede özgün başlangıç saklanır; öğrenci/veli bildirimi için Rescheduled olayı yayılır.
    /// </summary>
    public void Reschedule(DateTime newStartAtUtc, DateTime newEndAtUtc, string? note, DateTime updatedOnUtc)
    {
        OriginalStartAtUtc ??= StartAtUtc;
        StartAtUtc = newStartAtUtc;
        EndAtUtc = newEndAtUtc;
        RescheduleNote = note;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonScheduleRescheduledDomainEvent(Id, TeacherUserId, StudentId, StartAtUtc, EndAtUtc, updatedOnUtc));
    }

    /// <summary>Silme yalnızca oluşturmadan sonraki 24 saat içinde ve ders gelecekteyse mümkündür; aksi halde iptal kullanılır.</summary>
    public bool CanBeDeletedAt(DateTime nowUtc)
        => nowUtc <= CreatedOnUtc.AddHours(24) && StartAtUtc > nowUtc;

    /// <summary>Tekrar serisini verilen tarihten önce sonlandırır (RecurrenceRule'a UNTIL ekler). "Bu ve sonrakiler" iptali için.</summary>
    public void EndSeriesBefore(DateTime cutoffUtc, DateTime updatedOnUtc)
    {
        if (string.IsNullOrWhiteSpace(RecurrenceRule))
        {
            return;
        }

        var until = cutoffUtc.AddDays(-1).ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        RecurrenceRule = RecurrenceRule.Contains("UNTIL=", StringComparison.OrdinalIgnoreCase)
            ? System.Text.RegularExpressions.Regex.Replace(RecurrenceRule, "UNTIL=[^;]*", $"UNTIL={until}")
            : $"{RecurrenceRule};UNTIL={until}";
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Cancel(CancellationReason reason, bool isChargeable, string? cancellationNote, DateTime updatedOnUtc)
    {
        Status = LessonScheduleStatus.Cancelled;
        CancellationReason = reason;
        IsChargeable = isChargeable;
        UpdatedOnUtc = updatedOnUtc;

        if (!string.IsNullOrWhiteSpace(cancellationNote))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? cancellationNote
                : $"{Notes}{Environment.NewLine}{cancellationNote}";
        }

        Raise(new LessonScheduleCancelledDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }

    public void Complete(DateTime updatedOnUtc)
    {
        Status = LessonScheduleStatus.Completed;
        UpdatedOnUtc = updatedOnUtc;

        Raise(new LessonSessionCompletedDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }
}

public enum CancellationReason
{
    TeacherCancelled = 1,
    StudentCancelled = 2,
    Holiday = 3,
    Other = 4
}

public enum ScheduledLessonFormat
{
    InPerson = 1,
    Online = 2,
    Hybrid = 3
}

public enum LessonScheduleStatus
{
    Draft = 1,
    Planned = 2,
    Cancelled = 3,
    Completed = 4
}

public sealed record LessonScheduledDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    int ReminderOffsetMinutes,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record LessonScheduleRescheduledDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    DateTime UpdatedOnUtc) : DomainEvent;

public sealed record LessonScheduleCancelledDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CancelledOnUtc) : DomainEvent;

public sealed record LessonSessionCompletedDomainEvent(
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CompletedOnUtc) : DomainEvent;

public sealed class TimeOffBlock : AggregateRoot<Guid>
{
    private TimeOffBlock() { }

    public TimeOffBlock(
        Guid id,
        Guid teacherUserId,
        TimeOffType type,
        string title,
        DateTime startAtUtc,
        DateTime endAtUtc,
        bool isAllDay,
        DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        Type = type;
        Title = title;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        IsAllDay = isAllDay;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid TeacherUserId { get; private set; }
    public TimeOffType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime StartAtUtc { get; private set; }
    public DateTime EndAtUtc { get; private set; }
    public bool IsAllDay { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}

public enum TimeOffType
{
    Holiday = 1,
    Leave = 2,
    Official = 3,
    Other = 4
}

public sealed class LessonOccurrenceException : Entity<Guid>
{
    private LessonOccurrenceException() { }

    public LessonOccurrenceException(
        Guid id,
        Guid seriesLessonScheduleId,
        DateTime originalStartAtUtc,
        OccurrenceExceptionAction action,
        DateTime? overrideStartAtUtc,
        DateTime? overrideEndAtUtc,
        string? note,
        DateTime createdOnUtc)
    {
        Id = id;
        SeriesLessonScheduleId = seriesLessonScheduleId;
        OriginalStartAtUtc = originalStartAtUtc;
        Action = action;
        OverrideStartAtUtc = overrideStartAtUtc;
        OverrideEndAtUtc = overrideEndAtUtc;
        Note = note;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid SeriesLessonScheduleId { get; private set; }
    public DateTime OriginalStartAtUtc { get; private set; }
    public OccurrenceExceptionAction Action { get; private set; }
    public DateTime? OverrideStartAtUtc { get; private set; }
    public DateTime? OverrideEndAtUtc { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
}

public enum OccurrenceExceptionAction
{
    Skipped = 1,
    Cancelled = 2,
    Rescheduled = 3
}

/// <summary>
/// Öğrencinin bir dersin ertelenmesi için açtığı hafif talep (Ö-F). Öğrenci dersi kendisi değiştirmez;
/// yalnızca neden + alternatif tarih önerir. Öğretmen kabul ederse mevcut Reschedule akışı çalışır, redde talep kapanır.
/// Durum makinesi: Pending → (Accepted | Rejected); yalnızca Pending'den geçiş yapılır.
/// </summary>
public sealed class LessonChangeRequest : AggregateRoot<Guid>
{
    private LessonChangeRequest() { }

    public LessonChangeRequest(
        Guid id,
        Guid lessonScheduleId,
        Guid studentId,
        Guid teacherUserId,
        string reason,
        DateTime? proposedStartAtUtc,
        DateTime? proposedEndAtUtc,
        DateTime createdOnUtc)
    {
        Id = id;
        LessonScheduleId = lessonScheduleId;
        StudentId = studentId;
        TeacherUserId = teacherUserId;
        Reason = reason;
        ProposedStartAtUtc = proposedStartAtUtc;
        ProposedEndAtUtc = proposedEndAtUtc;
        Status = LessonChangeRequestStatus.Pending;
        CreatedOnUtc = createdOnUtc;

        Raise(new LessonChangeRequestedDomainEvent(Id, LessonScheduleId, StudentId, TeacherUserId, createdOnUtc));
    }

    public Guid LessonScheduleId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime? ProposedStartAtUtc { get; private set; }
    public DateTime? ProposedEndAtUtc { get; private set; }
    public LessonChangeRequestStatus Status { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ResolvedOnUtc { get; private set; }

    /// <summary>Öğretmen talebi kabul eder. Yalnızca bekleyen talepte; aksi halde geçersiz işlem.</summary>
    public void Accept(DateTime nowUtc) => Resolve(LessonChangeRequestStatus.Accepted, nowUtc);

    /// <summary>Öğretmen talebi reddeder. Yalnızca bekleyen talepte; aksi halde geçersiz işlem.</summary>
    public void Reject(DateTime nowUtc) => Resolve(LessonChangeRequestStatus.Rejected, nowUtc);

    private void Resolve(LessonChangeRequestStatus status, DateTime nowUtc)
    {
        if (Status != LessonChangeRequestStatus.Pending)
        {
            throw new InvalidOperationException("Yalnızca bekleyen (Pending) erteleme talebi sonuçlandırılabilir.");
        }

        Status = status;
        ResolvedOnUtc = nowUtc;

        Raise(new LessonChangeRequestResolvedDomainEvent(Id, LessonScheduleId, StudentId, TeacherUserId, status, nowUtc));
    }
}

public enum LessonChangeRequestStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3
}

public sealed record LessonChangeRequestedDomainEvent(
    Guid RequestId,
    Guid LessonScheduleId,
    Guid StudentId,
    Guid TeacherUserId,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record LessonChangeRequestResolvedDomainEvent(
    Guid RequestId,
    Guid LessonScheduleId,
    Guid StudentId,
    Guid TeacherUserId,
    LessonChangeRequestStatus Status,
    DateTime ResolvedOnUtc) : DomainEvent;
