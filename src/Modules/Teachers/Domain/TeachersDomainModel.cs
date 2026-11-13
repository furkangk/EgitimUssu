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
        string? profilePhotoKey,
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
        ProfilePhotoKey = profilePhotoKey;
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

    /// <summary>
    /// M02-4: Profil arama sonuçlarında görünür mü. Pasif öğretmen listelenmez ama verisi silinmez
    /// (öğrenci/ders geçmişi korunur) — kullanıcı istediğinde tekrar aktifleştirebilir.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// P04: Profil fotoğrafının <c>IFileStorage</c> anahtarı (public URL değil).
    /// Dosya yalnız <c>GET /api/teachers/profiles/{userId}/photo</c> üzerinden yetkiyle sunulur.
    /// </summary>
    public string? ProfilePhotoKey { get; private set; }

    /// <summary>
    /// E-07: Öğretmen profilini kimliksizleştirir ve <b>aramadan çıkarır</b>.
    /// <para>
    /// Profil satırı silinmez: ders ve ödeme kayıtları ona referans verir. Ama kişiyi gösteren her
    /// alan (ad, biyografi, vitrin başlığı, fotoğraf) kaybolur ve profil pasifleşir — silinmiş bir
    /// öğretmenin arama sonuçlarında görünmesi, silme talebinin en görünür ihlali olurdu.
    /// </para>
    /// <para>Fotoğraf <b>dosyasını</b> depolamadan silmek çağıranın işidir; burada yalnız anahtar düşürülür.</para>
    /// </summary>
    /// <param name="nowUtc">Anonimleştirme anı (UTC).</param>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData(DateTime nowUtc)
    {
        if (FullName == PersonalDataPlaceholder.FullName && !IsActive
            && Biography is null && Headline is null && ProfilePhotoKey is null)
        {
            return false;
        }

        FullName = PersonalDataPlaceholder.FullName;
        Biography = null;
        Headline = null;
        ProfilePhotoKey = null;
        City = string.Empty;
        District = string.Empty;
        IsActive = false;
        UpdatedOnUtc = nowUtc;
        return true;
    }

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
        UpdatedOnUtc = updatedOnUtc;

        MergeAvailabilitySlots(availabilitySlots);
        MergeSubjects(subjects);
        MergeCertificates(certificates);

        Raise(new TeacherProfileUpdatedDomainEvent(Id, UserId, Subject, updatedOnUtc));
    }

    /// <summary>
    /// M02-1: Doğrulama rozetini verir veya geri alır. Değer değişmediyse olay yayılmaz
    /// (aynı isteğin tekrarı gereksiz bildirim üretmesin).
    /// </summary>
    /// <param name="isVerified">Yeni doğrulama durumu.</param>
    /// <param name="updatedOnUtc">Güncelleme zamanı (UTC).</param>
    public void SetVerification(bool isVerified, DateTime updatedOnUtc)
    {
        if (IsVerified == isVerified)
        {
            return;
        }

        IsVerified = isVerified;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new TeacherVerifiedDomainEvent(Id, UserId, isVerified, updatedOnUtc));
    }

    /// <summary>M02-4: Profili arama sonuçlarından çıkarır (veri silinmez). Zaten pasifse hiçbir şey yapmaz.</summary>
    /// <param name="updatedOnUtc">Güncelleme zamanı (UTC).</param>
    public void Deactivate(DateTime updatedOnUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>M02-4: Profili tekrar aramaya açar. Zaten aktifse hiçbir şey yapmaz.</summary>
    /// <param name="updatedOnUtc">Güncelleme zamanı (UTC).</param>
    public void Activate(DateTime updatedOnUtc)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>
    /// P04 (M02-2): Profil fotoğrafını depolama anahtarına bağlar. **Önceki anahtarı döndürür** —
    /// çağıran onu depodan siler, böylece yetim dosya kalmaz.
    /// </summary>
    public string? SetProfilePhoto(string storageKey, DateTime updatedOnUtc)
    {
        var previousKey = ProfilePhotoKey;

        ProfilePhotoKey = storageKey;
        UpdatedOnUtc = updatedOnUtc;

        return string.Equals(previousKey, storageKey, StringComparison.Ordinal) ? null : previousKey;
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

    /// <summary>E-07: Sertifika bilgisini siler (dosya anahtarı dahil; dosyayı çağıran siler).</summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (Title == PersonalDataPlaceholder.RemovedContent && Institution is null && FileUrl is null)
        {
            return false;
        }

        Title = PersonalDataPlaceholder.RemovedContent;
        Institution = null;
        FileUrl = null;
        return true;
    }
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

/// <summary>M02-1: Öğretmenin doğrulama rozeti değişti (Admin işlemi).</summary>
/// <param name="TeacherProfileId">Profil kimliği.</param>
/// <param name="UserId">Öğretmenin kullanıcı kimliği.</param>
/// <param name="IsVerified">Yeni doğrulama durumu.</param>
/// <param name="VerifiedOnUtc">İşlemin uygulandığı zaman (UTC).</param>
public sealed record TeacherVerifiedDomainEvent(
    Guid TeacherProfileId,
    Guid UserId,
    bool IsVerified,
    DateTime VerifiedOnUtc) : DomainEvent;
