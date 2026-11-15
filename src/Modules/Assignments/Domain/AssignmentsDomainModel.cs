using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Domain;

public sealed class Assignment : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

    private Assignment()
    {
    }

    public Assignment(
        Guid id,
        Guid studentId,
        Guid teacherUserId,
        Guid? lessonSessionId,
        string title,
        string? description,
        DateTime? dueDateUtc,
        AssignmentStatus status,
        string? attachmentUrl,
        DateTime createdOnUtc,
        DateTime? completedOnUtc)
    {
        Id = id;
        StudentId = studentId;
        TeacherUserId = teacherUserId;
        LessonSessionId = lessonSessionId;
        Title = title;
        Description = description;
        DueDateUtc = dueDateUtc;
        Status = status;
        AttachmentUrl = attachmentUrl;
        CreatedOnUtc = createdOnUtc;
        CompletedOnUtc = completedOnUtc;

        Raise(new AssignmentCreatedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, CreatedOnUtc));
    }

    private readonly List<AssignmentSubmissionVersion> _submissionVersions = [];
    public IReadOnlyCollection<AssignmentSubmissionVersion> SubmissionVersions => _submissionVersions.AsReadOnly();
    public DateTime? SubmittedOnUtc { get; private set; }
    public bool IsLate { get; private set; }
    public Result CanSubmit() => Status is AssignmentStatus.Pending or AssignmentStatus.InProgress
        or AssignmentStatus.Completed or AssignmentStatus.ReturnedForRevision
        ? Result.Success() : Result.Failure(InvalidTransition);

    public Result Cancel()
    {
        if (Status != AssignmentStatus.Pending) return Result.Failure(InvalidTransition);
        Status = AssignmentStatus.Cancelled;
        return Result.Success();
    }

    private bool IsSubmitted => Status is AssignmentStatus.InProgress or AssignmentStatus.Completed;

    public Guid StudentId { get; private set; }

    public Guid TeacherUserId { get; private set; }

    public Guid? LessonSessionId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime? DueDateUtc { get; private set; }

    public AssignmentStatus Status { get; private set; }

    public string? AttachmentUrl { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    public string? TeacherFeedback { get; private set; }

    /// <summary>M06-4: Öğretmenin verdiği puan (0–100). Puanlanmadıysa <c>null</c>.</summary>
    public int? Score { get; private set; }

    /// <summary>M06-4: Puanın verildiği an.</summary>
    public DateTime? GradedOnUtc { get; private set; }

    /// <summary>M06-3: "Teslime 24 saat kaldı" uyarısının üretildiği an (ödev başına bir kez).</summary>
    public DateTime? DueSoonNotifiedOnUtc { get; private set; }

    /// <summary>M06-3: "Teslim edilmedi" uyarısının üretildiği an (ödev başına bir kez).</summary>
    public DateTime? MissedNotifiedOnUtc { get; private set; }

    /// <summary>Hâlâ öğrenciden teslim bekleyen ödev mi? (Teslim/onay/iptal edilmiş ödev uyarılmaz.)</summary>
    private bool IsOutstanding => Status is AssignmentStatus.Pending or AssignmentStatus.ReturnedForRevision;

    /// <summary>P04: Teslim dosyasının <c>IFileStorage</c> anahtarı (public URL değildir).</summary>
    public string? AttachmentStorageKey { get; private set; }

    /// <summary>Kullanıcıya gösterilecek özgün dosya adı.</summary>
    public string? AttachmentFileName { get; private set; }

    public string? AttachmentContentType { get; private set; }

    /// <summary>
    /// E-07: Ödevin kişisel/serbest metin alanlarını ve ek dosya bağlarını siler.
    /// Kayıt satırı kalır (ders geçmişinin parçası); dosyayı depolamadan silmek çağıranın işidir.
    /// </summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (Title == PersonalDataPlaceholder.RemovedContent && Description is null
            && TeacherFeedback is null && AttachmentStorageKey is null && AttachmentUrl is null
            && _submissionVersions.All(x => !x.HasPersonalData))
        {
            return false;
        }

        Title = PersonalDataPlaceholder.RemovedContent;
        Description = null;
        TeacherFeedback = null;
        AttachmentUrl = null;
        AttachmentStorageKey = null;
        AttachmentFileName = null;
        AttachmentContentType = null;
        AttachmentSizeBytes = null;
        foreach (var version in _submissionVersions) version.AnonymizePersonalData();
        return true;
    }

    public long? AttachmentSizeBytes { get; private set; }

    /// <summary>Dosyasız teslim yalnız ilk atama durumundan yapılır.</summary>
    public Result MarkCompleted(DateTime completedOnUtc, ILocalCalendar calendar)
    {
        if (Status != AssignmentStatus.Pending) return Result.Failure(InvalidTransition);
        RecordSubmission(completedOnUtc, calendar, null, null, null, null, null);
        Status = AssignmentStatus.Completed;
        CompletedOnUtc = completedOnUtc;
        Raise(new AssignmentCompletedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, completedOnUtc));
        return Result.Success();
    }

    /// <summary>Bağlantı teslimini yeni bir sürüm olarak saklar.</summary>
    public Result SubmitWork(string attachmentUrl, DateTime nowUtc, ILocalCalendar calendar)
    {
        var allowed = CanSubmit();
        if (allowed.IsFailure) return allowed;
        RecordSubmission(nowUtc, calendar, attachmentUrl, null, null, null, null);
        Status = AssignmentStatus.InProgress;
        Raise(new AssignmentSubmittedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, nowUtc));
        return Result.Success();
    }

    /// <summary>Dosyayı yeni sürüme bağlar; önceki sürümlerin dosyaları korunur.</summary>
    public Result AttachSubmission(
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTime nowUtc,
        ILocalCalendar calendar)
    {
        var allowed = CanSubmit();
        if (allowed.IsFailure) return allowed;
        RecordSubmission(nowUtc, calendar, $"/api/assignments/{Id}/attachment", storageKey, fileName, contentType, sizeBytes);
        Status = AssignmentStatus.InProgress;
        Raise(new AssignmentSubmittedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, nowUtc));
        return Result.Success();
    }

    private void RecordSubmission(DateTime nowUtc, ILocalCalendar calendar, string? url,
        string? key, string? fileName, string? contentType, long? sizeBytes)
    {
        var dueAtUtc = DeadlineUtc(calendar);
        SubmittedOnUtc = nowUtc;
        IsLate = dueAtUtc is { } deadline && nowUtc > deadline;
        AttachmentUrl = url;
        AttachmentStorageKey = key;
        AttachmentFileName = fileName;
        AttachmentContentType = contentType;
        AttachmentSizeBytes = sizeBytes;
        Score = null;
        GradedOnUtc = null;
        TeacherFeedback = null;
        _submissionVersions.Add(new AssignmentSubmissionVersion(Id,
            _submissionVersions.Count == 0 ? 1 : _submissionVersions.Max(x => x.Number) + 1,
            nowUtc, IsLate, url, key, fileName, contentType, sizeBytes));
    }

    public Result Approve(string? feedback, DateTime nowUtc)
    {
        if (!IsSubmitted) return Result.Failure(InvalidTransition);
        Status = AssignmentStatus.Approved;
        TeacherFeedback = feedback;
        LatestSubmission?.SetEvaluation(Score, GradedOnUtc, feedback);
        Raise(new AssignmentApprovedDomainEvent(Id, StudentId, TeacherUserId, nowUtc));
        return Result.Success();
    }

    public Result ReturnForRevision(string feedback, DateTime nowUtc)
    {
        if (!IsSubmitted) return Result.Failure(InvalidTransition);
        // Eski kayıtta kalmış puan varsa sürümde saklanır, güncel puan revizyonda boşalır.
        LatestSubmission?.SetEvaluation(Score, GradedOnUtc, feedback);
        Score = null;
        GradedOnUtc = null;
        Status = AssignmentStatus.ReturnedForRevision;
        TeacherFeedback = feedback;
        Raise(new AssignmentReturnedDomainEvent(Id, StudentId, TeacherUserId, nowUtc));
        return Result.Success();
    }

    private AssignmentSubmissionVersion? LatestSubmission => _submissionVersions.MaxBy(x => x.Number);

    /// <summary>
    /// M06-4: Onaylanmış ödeve 0–100 arası puan verir; geri bildirim verilmişse günceller.
    /// Geçiş kuralı domain'de tek yerde durur (Öğrenilenler #58): geçersiz istek DB'ye hiç dokunmaz.
    /// </summary>
    /// <param name="score">0–100 arası puan.</param>
    /// <param name="feedback">İsteğe bağlı geri bildirim; boşsa mevcut geri bildirim korunur.</param>
    /// <param name="nowUtc">İşlem anı.</param>
    public Result Grade(int score, string? feedback, DateTime nowUtc)
    {
        if (score is < 0 or > 100)
        {
            return Result.Failure(InvalidScore);
        }

        if (Status != AssignmentStatus.Approved)
        {
            return Result.Failure(GradeRequiresApproval);
        }

        Score = score;
        GradedOnUtc = nowUtc;
        if (!string.IsNullOrWhiteSpace(feedback))
        {
            TeacherFeedback = feedback.Trim();
        }

        LatestSubmission?.SetEvaluation(Score, GradedOnUtc, TeacherFeedback);
        Raise(new AssignmentGradedDomainEvent(Id, StudentId, TeacherUserId, score, nowUtc));
        return Result.Success();
    }

    /// <summary>
    /// M06-3: "Son teslime az kaldı" uyarısını bir kez üretir. Teslim edilmiş, kapanmış, tarihsiz
    /// ya da vadesi çoktan geçmiş ödev için hiçbir şey yapmaz (idempotent).
    /// </summary>
    /// <returns>Uyarı bu çağrıda üretildiyse <c>true</c>.</returns>
    public bool MarkDueSoon(DateTime nowUtc, ILocalCalendar calendar)
    {
        var deadline = DeadlineUtc(calendar);
        if (DueSoonNotifiedOnUtc is not null || SubmittedOnUtc is not null || !IsOutstanding
            || deadline is null || deadline <= nowUtc || deadline > nowUtc.AddHours(24))
        {
            return false;
        }

        DueSoonNotifiedOnUtc = nowUtc;
        Raise(new AssignmentDueSoonDomainEvent(Id, StudentId, TeacherUserId, Title, deadline.Value, nowUtc));
        return true;
    }

    /// <summary>
    /// M06-3: Vadesi geçmiş ve hâlâ teslim edilmemiş ödev için "teslim edilmedi" uyarısını bir kez üretir.
    /// </summary>
    /// <returns>Uyarı bu çağrıda üretildiyse <c>true</c>.</returns>
    public bool MarkMissed(DateTime nowUtc, ILocalCalendar calendar)
    {
        var deadline = DeadlineUtc(calendar);
        if (MissedNotifiedOnUtc is not null || SubmittedOnUtc is not null || !IsOutstanding
            || deadline is null || deadline > nowUtc)
        {
            return false;
        }

        MissedNotifiedOnUtc = nowUtc;
        Raise(new AssignmentMissedDomainEvent(Id, StudentId, TeacherUserId, Title, deadline.Value, nowUtc));
        return true;
    }

    // PostgreSQL ve teslim gecikmesi aynı son mikro-saniyeyi kullanır.
    private DateTime? DeadlineUtc(ILocalCalendar calendar) => DueDateUtc is { } due
        ? calendar.DayRangeUtc(calendar.ToLocalDate(due)).EndUtc.AddTicks(-10)
        : null;

    private static readonly Error InvalidTransition = new("assignments.invalid_transition", "Ödev durumu bu işleme izin vermiyor.");

    private static readonly Error InvalidScore = new("assignments.invalid_score", "Puan 0 ile 100 arasinda olmalidir.");

    private static readonly Error GradeRequiresApproval =
        new("assignments.grade_requires_approval", "Yalnizca onaylanmis odev puanlanabilir.");
}

public sealed class AssignmentSubmissionVersion
{
    private AssignmentSubmissionVersion() { }

    internal AssignmentSubmissionVersion(Guid assignmentId, int number, DateTime submittedOnUtc, bool isLate,
        string? attachmentUrl, string? storageKey, string? fileName, string? contentType, long? sizeBytes)
    {
        AssignmentId = assignmentId;
        Number = number;
        SubmittedOnUtc = submittedOnUtc;
        IsLate = isLate;
        AttachmentUrl = attachmentUrl;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    internal void SetEvaluation(int? score, DateTime? gradedOnUtc, string? feedback)
    {
        Score = score;
        GradedOnUtc = gradedOnUtc;
        TeacherFeedback = feedback;
    }

    internal bool HasPersonalData => AttachmentUrl is not null || StorageKey is not null
        || FileName is not null || ContentType is not null || TeacherFeedback is not null;

    internal void AnonymizePersonalData()
    {
        AttachmentUrl = null;
        StorageKey = null;
        FileName = null;
        ContentType = null;
        SizeBytes = null;
        TeacherFeedback = null;
    }

    public Guid AssignmentId { get; private set; }
    public int Number { get; private set; }
    public DateTime SubmittedOnUtc { get; private set; }
    public bool IsLate { get; private set; }
    public string? AttachmentUrl { get; private set; }
    public string? StorageKey { get; private set; }
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }
    public int? Score { get; private set; }
    public DateTime? GradedOnUtc { get; private set; }
    public string? TeacherFeedback { get; private set; }
}

public sealed class LessonNote : AggregateRoot<Guid>
{
    private LessonNote()
    {
    }

    public LessonNote(
        Guid id,
        Guid lessonSessionId,
        Guid teacherUserId,
        Guid studentId,
        string summary,
        string? coveredTopics,
        string? recommendations,
        LessonNoteVisibility visibility,
        DateTime createdOnUtc)
    {
        Id = id;
        LessonSessionId = lessonSessionId;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Summary = summary;
        CoveredTopics = coveredTopics;
        Recommendations = recommendations;
        Visibility = visibility;
        CreatedOnUtc = createdOnUtc;

        Raise(new LessonNoteCreatedDomainEvent(Id, LessonSessionId, TeacherUserId, StudentId, CreatedOnUtc));
    }

    public Guid LessonSessionId { get; private set; }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? CoveredTopics { get; private set; }

    public string? Recommendations { get; private set; }

    /// <summary>E-07: Ders notundaki serbest metinleri siler.</summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (Summary == PersonalDataPlaceholder.RemovedContent
            && CoveredTopics is null && Recommendations is null)
        {
            return false;
        }

        Summary = PersonalDataPlaceholder.RemovedContent;
        CoveredTopics = null;
        Recommendations = null;
        return true;
    }

    public LessonNoteVisibility Visibility { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public void Update(
        string summary,
        string? coveredTopics,
        string? recommendations,
        LessonNoteVisibility visibility)
    {
        Summary = summary;
        CoveredTopics = coveredTopics;
        Recommendations = recommendations;
        Visibility = visibility;
    }
}

public enum AssignmentStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Approved = 5,
    ReturnedForRevision = 6
}

public enum LessonNoteVisibility
{
    Private = 1,
    Student = 2,
    StudentAndParent = 3
}

public sealed record AssignmentCreatedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    Guid? LessonSessionId,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record AssignmentCompletedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    Guid? LessonSessionId,
    DateTime CompletedOnUtc) : DomainEvent;

public sealed record AssignmentSubmittedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    Guid? LessonSessionId,
    DateTime SubmittedOnUtc) : DomainEvent;

public sealed record AssignmentApprovedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    DateTime OnUtc) : DomainEvent;

public sealed record AssignmentReturnedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    DateTime OnUtc) : DomainEvent;

/// <summary>M06-4: Ödev puanlandı.</summary>
public sealed record AssignmentGradedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    int Score,
    DateTime GradedOnUtc) : DomainEvent;

/// <summary>M06-3: Ödevin son teslimine az kaldı (ödev başına bir kez).</summary>
public sealed record AssignmentDueSoonDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    string Title,
    DateTime DueDateUtc,
    DateTime NotifiedOnUtc) : DomainEvent;

/// <summary>M06-3: Ödevin son teslim tarihi geçti, teslim edilmedi (ödev başına bir kez).</summary>
public sealed record AssignmentMissedDomainEvent(
    Guid AssignmentId,
    Guid StudentId,
    Guid TeacherUserId,
    string Title,
    DateTime DueDateUtc,
    DateTime NotifiedOnUtc) : DomainEvent;

public sealed record LessonNoteCreatedDomainEvent(
    Guid LessonNoteId,
    Guid LessonSessionId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CreatedOnUtc) : DomainEvent;

/// <summary>M06-1: Ders kaynağının türü.</summary>
public enum LessonResourceKind
{
    File = 1,
    Link = 2
}

/// <summary>
/// M06-1 (P04): Öğretmenin paylaştığı ders materyali — yüklenmiş bir dosya ya da bir bağlantı.
/// <see cref="StudentId"/> <c>null</c> ise öğretmenin tüm öğrencilerine açıktır.
/// Silme **yumuşaktır** (<see cref="Archive"/>): geçmiş derse ait kaynak kaybolmasın diye kayıt durur.
/// </summary>
public sealed class LessonResource : AggregateRoot<Guid>
{
    private LessonResource()
    {
    }

    private LessonResource(
        Guid id,
        Guid teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        string title,
        string? description,
        LessonResourceKind kind,
        DateTime createdOnUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Kaynak basligi zorunludur.", nameof(title));
        }

        Id = id;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        LessonSessionId = lessonSessionId;
        Title = title.Trim();
        Description = description?.Trim();
        Kind = kind;
        IsActive = true;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid TeacherUserId { get; private set; }

    /// <summary><c>null</c> = öğretmenin tüm öğrencilerine açık.</summary>
    public Guid? StudentId { get; private set; }

    public Guid? LessonSessionId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public LessonResourceKind Kind { get; private set; }

    /// <summary><see cref="LessonResourceKind.File"/> için <c>IFileStorage</c> anahtarı.</summary>
    public string? StorageKey { get; private set; }

    public string? FileName { get; private set; }

    public string? ContentType { get; private set; }

    public long? SizeBytes { get; private set; }

    /// <summary><see cref="LessonResourceKind.Link"/> için dış bağlantı.</summary>
    public string? Url { get; private set; }

    /// <summary>E-07: Ders kaynağının başlığını, açıklamasını ve dosya bağını siler.</summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (Title == PersonalDataPlaceholder.RemovedContent && Description is null
            && StorageKey is null && Url is null)
        {
            return false;
        }

        Title = PersonalDataPlaceholder.RemovedContent;
        Description = null;
        StorageKey = null;
        FileName = null;
        ContentType = null;
        Url = null;
        return true;
    }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? ArchivedOnUtc { get; private set; }

    public static LessonResource CreateFile(
        Guid id,
        Guid teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        string title,
        string? description,
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTime createdOnUtc)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Dosya kaynagi icin depolama anahtari zorunludur.", nameof(storageKey));
        }

        return new LessonResource(
            id, teacherUserId, studentId, lessonSessionId, title, description, LessonResourceKind.File, createdOnUtc)
        {
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes
        };
    }

    public static LessonResource CreateLink(
        Guid id,
        Guid teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        string title,
        string? description,
        string url,
        DateTime createdOnUtc)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Baglanti kaynagi icin URL zorunludur.", nameof(url));
        }

        return new LessonResource(
            id, teacherUserId, studentId, lessonSessionId, title, description, LessonResourceKind.Link, createdOnUtc)
        {
            Url = url.Trim()
        };
    }

    /// <summary>Yumuşak silme: kaynak listelerden düşer ama kayıt (ve dosya) korunur.</summary>
    public void Archive(DateTime nowUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        ArchivedOnUtc = nowUtc;
    }
}
