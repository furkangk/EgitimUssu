using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.LessonSessions.Domain;

public sealed class LessonSession : AggregateRoot<Guid>
{
    private LessonSession()
    {
    }

    public LessonSession(
        Guid id,
        Guid? lessonScheduleId,
        Guid teacherUserId,
        Guid studentId,
        string subject,
        DateTime plannedStartAtUtc,
        DateTime? actualStartAtUtc,
        DateTime? actualEndAtUtc,
        int? durationMinutes,
        StudentAttendanceStatus attendanceStatus,
        LessonSessionStatus status,
        string topicTitle,
        string? coveredContent,
        string? teacherNotes,
        DateTime createdOnUtc,
        DateTime? completedOnUtc,
        DateTime? occurrenceStartUtc = null)
    {
        Id = id;
        LessonScheduleId = lessonScheduleId;
        OccurrenceStartUtc = lessonScheduleId.HasValue ? occurrenceStartUtc ?? plannedStartAtUtc : null;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Subject = subject;
        PlannedStartAtUtc = plannedStartAtUtc;
        ActualStartAtUtc = actualStartAtUtc;
        ActualEndAtUtc = actualEndAtUtc;
        DurationMinutes = durationMinutes;
        AttendanceStatus = attendanceStatus;
        Status = status;
        TopicTitle = topicTitle;
        CoveredContent = coveredContent;
        TeacherNotes = teacherNotes;
        CreatedOnUtc = createdOnUtc;
        CompletedOnUtc = completedOnUtc;

        Raise(new LessonSessionCreatedDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, PlannedStartAtUtc, createdOnUtc));
    }

    public Guid? LessonScheduleId { get; private set; }

    public DateTime? OccurrenceStartUtc { get; private set; }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public DateTime PlannedStartAtUtc { get; private set; }

    public DateTime? ActualStartAtUtc { get; private set; }

    public DateTime? ActualEndAtUtc { get; private set; }

    public int? DurationMinutes { get; private set; }

    public StudentAttendanceStatus AttendanceStatus { get; private set; }

    public LessonSessionStatus Status { get; private set; }

    public string TopicTitle { get; private set; } = string.Empty;

    public string? CoveredContent { get; private set; }

    public string? TeacherNotes { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    public bool IsChargeable { get; private set; }

    /// <summary>M05-4: Çevrimiçi dersin toplantı bağlantısı (Zoom/Meet vb.). Planlı dersten türetilirken kopyalanır.</summary>
    public string? MeetingUrl { get; private set; }

    /// <summary>M05-4: Ders kaydının bağlantısı (varsa). Ders sonrasında elle girilir.</summary>
    public string? RecordingUrl { get; private set; }

    /// <summary>M05-1: İptal nedeni (serbest metin, opsiyonel). Yalnız <see cref="LessonSessionStatus.Cancelled"/> durumunda anlamlıdır.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// E-07: Ders oturumundaki serbest metinleri ve bağlantıları siler.
    /// Süre, tarih, katılım ve durum <b>korunur</b>: ders kaydı ücretlendirmenin dayanağıdır ve
    /// yasal saklama süresince kimliksizleşmiş hâlde durmalıdır.
    /// </summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (TopicTitle == PersonalDataPlaceholder.RemovedContent && CoveredContent is null
            && TeacherNotes is null && MeetingUrl is null && RecordingUrl is null
            && CancellationReason is null)
        {
            return false;
        }

        TopicTitle = PersonalDataPlaceholder.RemovedContent;
        CoveredContent = null;
        TeacherNotes = null;
        MeetingUrl = null;
        RecordingUrl = null;
        CancellationReason = null;
        return true;
    }

    /// <summary>
    /// M05-1: Dersi başlatır — yalnız <see cref="LessonSessionStatus.Planned"/> durumundan
    /// <see cref="LessonSessionStatus.InProgress"/> durumuna geçilebilir.
    /// </summary>
    /// <param name="actualStartAtUtc">Gerçek başlangıç zamanı (UTC).</param>
    /// <returns>Geçiş geçerli değilse <c>lesson_sessions.invalid_transition</c>.</returns>
    public Result Start(DateTime actualStartAtUtc)
    {
        if (Status != LessonSessionStatus.Planned)
        {
            return Result.Failure(InvalidTransition);
        }

        ActualStartAtUtc = actualStartAtUtc;
        Status = LessonSessionStatus.InProgress;
        return Result.Success();
    }

    /// <summary>
    /// M05-1: Dersi iptal eder — yalnız <see cref="LessonSessionStatus.Planned"/> veya
    /// <see cref="LessonSessionStatus.InProgress"/> durumundan iptal edilebilir; tamamlanmış ders iptal edilemez.
    /// </summary>
    /// <param name="reason">İptal nedeni (boş/boşluk ise <c>null</c> yazılır).</param>
    /// <param name="nowUtc">İşlem zamanı (UTC).</param>
    /// <returns>Geçiş geçerli değilse <c>lesson_sessions.invalid_transition</c>.</returns>
    public Result Cancel(string? reason, DateTime nowUtc)
    {
        if (Status is not (LessonSessionStatus.Planned or LessonSessionStatus.InProgress))
        {
            return Result.Failure(InvalidTransition);
        }

        _ = nowUtc;
        Status = LessonSessionStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return Result.Success();
    }

    /// <summary>M05-4: Toplantı bağlantısını yazar; boş/boşluk değer bağlantıyı temizler.</summary>
    /// <param name="meetingUrl">Toplantı bağlantısı.</param>
    public void SetMeetingUrl(string? meetingUrl)
        => MeetingUrl = string.IsNullOrWhiteSpace(meetingUrl) ? null : meetingUrl.Trim();

    /// <summary>M05-4: Kayıt bağlantısını yazar; boş/boşluk değer bağlantıyı temizler.</summary>
    /// <param name="recordingUrl">Kayıt bağlantısı.</param>
    public void SetRecordingUrl(string? recordingUrl)
        => RecordingUrl = string.IsNullOrWhiteSpace(recordingUrl) ? null : recordingUrl.Trim();

    /// <summary>
    /// Dersi tamamlanmış duruma getirir. İptal edilmiş ders tamamlanamaz; tamamlanmış ders
    /// **yeniden** tamamlanabilir (öğretmenin konu/not düzeltmesi).
    /// </summary>
    /// <param name="actualStartAtUtc">Gerçek başlangıç (UTC).</param>
    /// <param name="actualEndAtUtc">Gerçek bitiş (UTC) — başlangıçtan sonra olmalı.</param>
    /// <param name="attendanceStatus">Yoklama durumu.</param>
    /// <param name="topicTitle">İşlenen konu başlığı.</param>
    /// <param name="coveredContent">İşlenen içerik.</param>
    /// <param name="teacherNotes">Öğretmen notu.</param>
    /// <param name="isChargeable">Ücretlendirilecek mi.</param>
    /// <param name="completedOnUtc">Tamamlama zamanı (UTC).</param>
    /// <returns><c>lesson_sessions.invalid_transition</c> · <c>lesson_sessions.invalid_time_range</c> ya da başarı.</returns>
    public Result Complete(
        DateTime actualStartAtUtc,
        DateTime actualEndAtUtc,
        StudentAttendanceStatus attendanceStatus,
        string topicTitle,
        string? coveredContent,
        string? teacherNotes,
        bool isChargeable,
        DateTime completedOnUtc)
    {
        if (Status == LessonSessionStatus.Cancelled)
        {
            return Result.Failure(InvalidTransition);
        }

        if (Status != LessonSessionStatus.Completed && PlannedStartAtUtc > completedOnUtc)
            return Result.Failure(new Error("lesson_sessions.future_occurrence", "Gelecekteki ders henüz tamamlanamaz."));

        if (actualEndAtUtc <= actualStartAtUtc)
        {
            return Result.Failure(InvalidTimeRange);
        }

        var firstCompletion = Status != LessonSessionStatus.Completed;
        ActualStartAtUtc = actualStartAtUtc;
        ActualEndAtUtc = actualEndAtUtc;
        DurationMinutes = (int)Math.Ceiling((actualEndAtUtc - actualStartAtUtc).TotalMinutes);
        AttendanceStatus = attendanceStatus;
        Status = LessonSessionStatus.Completed;
        TopicTitle = topicTitle;
        CoveredContent = coveredContent;
        TeacherNotes = teacherNotes;
        IsChargeable = isChargeable;
        if (firstCompletion)
        {
            CompletedOnUtc = completedOnUtc;
            Raise(new LessonSessionCompletedDomainEvent(Id, LessonScheduleId, TeacherUserId, StudentId, completedOnUtc));
        }
        return Result.Success();
    }

    private static readonly Error InvalidTransition =
        new("lesson_sessions.invalid_transition", "Ders oturumunun mevcut durumu bu işleme izin vermiyor.");

    private static readonly Error InvalidTimeRange =
        new("lesson_sessions.invalid_time_range", "Ders bitiş saati başlangıç saatinden sonra olmalıdır.");
}

public enum StudentAttendanceStatus
{
    Unknown = 1,
    Attended = 2,
    Late = 3,
    Absent = 4
}

public enum LessonSessionStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public sealed record LessonSessionCreatedDomainEvent(
    Guid LessonSessionId,
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime PlannedStartAtUtc,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record LessonSessionCompletedDomainEvent(
    Guid LessonSessionId,
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CompletedOnUtc) : DomainEvent;
