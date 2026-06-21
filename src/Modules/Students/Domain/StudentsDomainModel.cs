using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Domain;

public sealed class StudentProfile : AggregateRoot<Guid>
{
    private StudentProfile()
    {
    }

    public StudentProfile(
        Guid id,
        Guid? userId,
        Guid? createdByTeacherUserId,
        Guid? parentUserId,
        string fullName,
        string gradeLevel,
        string? contactEmail,
        string? contactPhone,
        string? goalSummary,
        string? levelNotes,
        StudentOrigin origin,
        bool isActive,
        DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        CreatedByTeacherUserId = createdByTeacherUserId;
        ParentUserId = parentUserId;
        FullName = fullName;
        GradeLevel = gradeLevel;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        GoalSummary = goalSummary;
        LevelNotes = levelNotes;
        Origin = origin;
        IsActive = isActive;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new StudentProfileCreatedDomainEvent(Id, UserId, CreatedByTeacherUserId, Origin, createdOnUtc));
    }

    public Guid? UserId { get; private set; }

    public Guid? CreatedByTeacherUserId { get; private set; }

    public Guid? ParentUserId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string GradeLevel { get; private set; } = string.Empty;

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public string? GoalSummary { get; private set; }

    public string? LevelNotes { get; private set; }

    public StudentOrigin Origin { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public List<StudentSubject> Subjects { get; private set; } = [];
}

public sealed class StudentSubject : Entity<Guid>
{
    private StudentSubject()
    {
    }

    public StudentSubject(Guid id, Guid studentProfileId, string subject, string? targetLevel)
    {
        Id = id;
        StudentProfileId = studentProfileId;
        Subject = subject;
        TargetLevel = targetLevel;
    }

    public Guid StudentProfileId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string? TargetLevel { get; private set; }
}

public enum StudentOrigin
{
    TeacherManaged = 1,
    SelfRegistered = 2
}

public sealed record StudentProfileCreatedDomainEvent(
    Guid StudentProfileId,
    Guid? UserId,
    Guid? CreatedByTeacherUserId,
    StudentOrigin Origin,
    DateTime CreatedOnUtc) : DomainEvent;
