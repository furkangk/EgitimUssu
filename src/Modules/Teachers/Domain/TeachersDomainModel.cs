using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Teachers.Domain;

public sealed class TeacherProfile : AggregateRoot<Guid>
{
    private TeacherProfile()
    {
    }

    public TeacherProfile(
        Guid id,
        Guid userId,
        string fullName,
        string subject,
        string city,
        string district,
        string? biography,
        string? headline,
        TeacherLessonFormat lessonFormat,
        int experienceYears,
        string educationLevel,
        decimal hourlyRateAmount,
        string currency,
        bool isVerified,
        string? profilePhotoUrl,
        DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        FullName = fullName;
        Subject = subject;
        City = city;
        District = district;
        Biography = biography;
        Headline = headline;
        LessonFormat = lessonFormat;
        ExperienceYears = experienceYears;
        EducationLevel = educationLevel;
        HourlyRateAmount = hourlyRateAmount;
        Currency = currency;
        IsVerified = isVerified;
        ProfilePhotoUrl = profilePhotoUrl;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new TeacherProfileCreatedDomainEvent(Id, UserId, Subject, City, District, createdOnUtc));
    }

    public Guid UserId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string District { get; private set; } = string.Empty;

    public string? Biography { get; private set; }

    public string? Headline { get; private set; }

    public TeacherLessonFormat LessonFormat { get; private set; }

    public int ExperienceYears { get; private set; }

    public string EducationLevel { get; private set; } = string.Empty;

    public decimal HourlyRateAmount { get; private set; }

    public string Currency { get; private set; } = "TRY";

    public bool IsVerified { get; private set; }

    public string? ProfilePhotoUrl { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public List<TeacherAvailabilitySlot> AvailabilitySlots { get; private set; } = [];

    public void Update(
        string fullName,
        string subject,
        string city,
        string district,
        string? biography,
        string? headline,
        TeacherLessonFormat lessonFormat,
        int experienceYears,
        string educationLevel,
        decimal hourlyRateAmount,
        string currency,
        bool isVerified,
        string? profilePhotoUrl,
        IReadOnlyCollection<TeacherAvailabilitySlot> availabilitySlots,
        DateTime updatedOnUtc)
    {
        FullName = fullName;
        Subject = subject;
        City = city;
        District = district;
        Biography = biography;
        Headline = headline;
        LessonFormat = lessonFormat;
        ExperienceYears = experienceYears;
        EducationLevel = educationLevel;
        HourlyRateAmount = hourlyRateAmount;
        Currency = currency;
        IsVerified = isVerified;
        ProfilePhotoUrl = profilePhotoUrl;
        UpdatedOnUtc = updatedOnUtc;

        AvailabilitySlots.Clear();
        AvailabilitySlots.AddRange(availabilitySlots);

        Raise(new TeacherProfileUpdatedDomainEvent(Id, UserId, Subject, updatedOnUtc));
    }
}

public sealed class TeacherAvailabilitySlot : Entity<Guid>
{
    private TeacherAvailabilitySlot()
    {
    }

    public TeacherAvailabilitySlot(
        Guid id,
        Guid teacherProfileId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        bool isOnlineAvailable,
        bool isInPersonAvailable)
    {
        Id = id;
        TeacherProfileId = teacherProfileId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        IsOnlineAvailable = isOnlineAvailable;
        IsInPersonAvailable = isInPersonAvailable;
    }

    public Guid TeacherProfileId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public bool IsOnlineAvailable { get; private set; }

    public bool IsInPersonAvailable { get; private set; }
}

public enum TeacherLessonFormat
{
    InPerson = 1,
    Online = 2,
    Hybrid = 3
}

public sealed record TeacherProfileCreatedDomainEvent(
    Guid TeacherProfileId,
    Guid UserId,
    string Subject,
    string City,
    string District,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record TeacherProfileUpdatedDomainEvent(
    Guid TeacherProfileId,
    Guid UserId,
    string Subject,
    DateTime UpdatedOnUtc) : DomainEvent;
