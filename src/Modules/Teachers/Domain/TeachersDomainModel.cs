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

    public List<TeacherSubject> Subjects { get; private set; } = [];

    public List<TeacherCertificate> Certificates { get; private set; } = [];

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
        string? profilePhotoUrl,
        IReadOnlyCollection<TeacherAvailabilitySlot> availabilitySlots,
        IReadOnlyCollection<TeacherSubject> subjects,
        IReadOnlyCollection<TeacherCertificate> certificates,
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
        ProfilePhotoUrl = profilePhotoUrl;
        UpdatedOnUtc = updatedOnUtc;

        MergeAvailabilitySlots(availabilitySlots);
        MergeSubjects(subjects);
        MergeCertificates(certificates);

        Raise(new TeacherProfileUpdatedDomainEvent(Id, UserId, Subject, updatedOnUtc));
    }

    /// <summary>
    /// Branş listesini doğal anahtara (branş adı) göre birleştirir: eşleşenler korunur (PK değişmez),
    /// listede olmayanlar silinir, yeni olanlar eklenir. Sil-yeniden-ekle deseni EF'te
    /// "orphan update" hatasına yol açtığı için tercih edilmez (A-01).
    /// </summary>
    public void MergeSubjects(IReadOnlyCollection<TeacherSubject> desired)
    {
        var desiredNames = desired
            .Select(subject => subject.Subject)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Subjects.RemoveAll(existing => !desiredNames.Contains(existing.Subject));

        var existingNames = Subjects
            .Select(subject => subject.Subject)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Subjects.AddRange(desired.Where(subject => existingNames.Add(subject.Subject)));
    }

    /// <summary>
    /// Sertifikaları (Başlık, Kurum, Yıl) üçlüsüne göre birleştirir.
    /// </summary>
    public void MergeCertificates(IReadOnlyCollection<TeacherCertificate> desired)
    {
        static string Key(TeacherCertificate certificate)
            => $"{certificate.Title}|{certificate.Institution}|{certificate.Year}".ToUpperInvariant();

        var desiredKeys = desired.Select(Key).ToHashSet(StringComparer.Ordinal);
        Certificates.RemoveAll(existing => !desiredKeys.Contains(Key(existing)));

        var existingKeys = Certificates.Select(Key).ToHashSet(StringComparer.Ordinal);
        Certificates.AddRange(desired.Where(certificate => existingKeys.Add(Key(certificate))));
    }

    /// <summary>
    /// Uygunluk slotlarını (Gün, Başlangıç, Bitiş) üçlüsüne göre birleştirir; eşleşen slotta
    /// yalnız online/yüz yüze bayrakları güncellenir.
    /// </summary>
    public void MergeAvailabilitySlots(IReadOnlyCollection<TeacherAvailabilitySlot> desired)
    {
        static string Key(TeacherAvailabilitySlot slot)
            => $"{(int)slot.DayOfWeek}|{slot.StartTime:HH\\:mm}|{slot.EndTime:HH\\:mm}";

        var desiredByKey = desired.ToDictionary(Key, slot => slot, StringComparer.Ordinal);
        AvailabilitySlots.RemoveAll(existing => !desiredByKey.ContainsKey(Key(existing)));

        foreach (var existing in AvailabilitySlots)
        {
            var match = desiredByKey[Key(existing)];
            existing.SetAvailability(match.IsOnlineAvailable, match.IsInPersonAvailable);
        }

        var existingKeys = AvailabilitySlots.Select(Key).ToHashSet(StringComparer.Ordinal);
        AvailabilitySlots.AddRange(desired.Where(slot => existingKeys.Add(Key(slot))));
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

    public void SetAvailability(bool isOnlineAvailable, bool isInPersonAvailable)
    {
        IsOnlineAvailable = isOnlineAvailable;
        IsInPersonAvailable = isInPersonAvailable;
    }
}

public sealed class TeacherSubject : Entity<Guid>
{
    private TeacherSubject()
    {
    }

    public TeacherSubject(Guid id, Guid teacherProfileId, string subject)
    {
        Id = id;
        TeacherProfileId = teacherProfileId;
        Subject = subject;
    }

    public Guid TeacherProfileId { get; private set; }

    public string Subject { get; private set; } = string.Empty;
}

public sealed class TeacherCertificate : Entity<Guid>
{
    private TeacherCertificate()
    {
    }

    public TeacherCertificate(Guid id, Guid teacherProfileId, string title, string? institution, int? year, string? fileUrl)
    {
        Id = id;
        TeacherProfileId = teacherProfileId;
        Title = title;
        Institution = institution;
        Year = year;
        FileUrl = fileUrl;
    }

    public Guid TeacherProfileId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Institution { get; private set; }

    public int? Year { get; private set; }

    public string? FileUrl { get; private set; }
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
