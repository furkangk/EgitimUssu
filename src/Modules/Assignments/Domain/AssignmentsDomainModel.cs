using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Domain;

public sealed class Assignment : AggregateRoot<Guid>
{
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

    /// <summary>P04: Teslim dosyasının <c>IFileStorage</c> anahtarı (public URL değildir).</summary>
    public string? AttachmentStorageKey { get; private set; }

    /// <summary>Kullanıcıya gösterilecek özgün dosya adı.</summary>
    public string? AttachmentFileName { get; private set; }

    public string? AttachmentContentType { get; private set; }

    public long? AttachmentSizeBytes { get; private set; }

    public void MarkCompleted(DateTime completedOnUtc)
    {
        Status = AssignmentStatus.Completed;
        CompletedOnUtc = completedOnUtc;

        Raise(new AssignmentCompletedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, completedOnUtc));
    }

    /// <summary>Öğrenci ödev çözümünü (dosya/bağlantı) yükler. Bekleyen ödev "devam ediyor"a geçer.</summary>
    public void SubmitWork(string attachmentUrl, DateTime nowUtc)
    {
        AttachmentUrl = attachmentUrl;
        if (Status is AssignmentStatus.Pending or AssignmentStatus.ReturnedForRevision)
        {
            Status = AssignmentStatus.InProgress;
        }

        Raise(new AssignmentSubmittedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, nowUtc));
    }

    /// <summary>
    /// P04: Ödeve depolanmış bir teslim dosyası bağlar ve <see cref="SubmitWork"/> geçişini uygular.
    /// Önceki dosyanın anahtarını döndürür; çağıran onu depodan siler (yetim dosya kalmasın).
    /// </summary>
    public string? AttachSubmission(
        string storageKey,
        string fileName,
        string contentType,
        long sizeBytes,
        DateTime nowUtc)
    {
        var previousKey = AttachmentStorageKey;

        AttachmentStorageKey = storageKey;
        AttachmentFileName = fileName;
        AttachmentContentType = contentType;
        AttachmentSizeBytes = sizeBytes;

        SubmitWork($"/api/assignments/{Id}/attachment", nowUtc);

        return string.Equals(previousKey, storageKey, StringComparison.Ordinal) ? null : previousKey;
    }

    /// <summary>Öğretmen ödevi onaylar; isteğe bağlı geri bildirim ekler.</summary>
    public void Approve(string? feedback, DateTime nowUtc)
    {
        Status = AssignmentStatus.Approved;
        TeacherFeedback = feedback;
        Raise(new AssignmentApprovedDomainEvent(Id, StudentId, TeacherUserId, nowUtc));
    }

    /// <summary>Öğretmen ödevi geri bildirimle revizyona geri gönderir.</summary>
    public void ReturnForRevision(string feedback, DateTime nowUtc)
    {
        Status = AssignmentStatus.ReturnedForRevision;
        TeacherFeedback = feedback;
        Raise(new AssignmentReturnedDomainEvent(Id, StudentId, TeacherUserId, nowUtc));
    }
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
