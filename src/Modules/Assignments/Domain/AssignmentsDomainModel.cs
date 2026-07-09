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
        if (Status == AssignmentStatus.Pending)
        {
            Status = AssignmentStatus.InProgress;
        }

        Raise(new AssignmentSubmittedDomainEvent(Id, StudentId, TeacherUserId, LessonSessionId, nowUtc));
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
        DateTime createdOnUtc)
    {
        Id = id;
        LessonSessionId = lessonSessionId;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Summary = summary;
        CoveredTopics = coveredTopics;
        Recommendations = recommendations;
        CreatedOnUtc = createdOnUtc;

        Raise(new LessonNoteCreatedDomainEvent(Id, LessonSessionId, TeacherUserId, StudentId, CreatedOnUtc));
    }

    public Guid LessonSessionId { get; private set; }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? CoveredTopics { get; private set; }

    public string? Recommendations { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public void Update(
        string summary,
        string? coveredTopics,
        string? recommendations)
    {
        Summary = summary;
        CoveredTopics = coveredTopics;
        Recommendations = recommendations;
    }
}

public enum AssignmentStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
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

public sealed record LessonNoteCreatedDomainEvent(
    Guid LessonNoteId,
    Guid LessonSessionId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CreatedOnUtc) : DomainEvent;
