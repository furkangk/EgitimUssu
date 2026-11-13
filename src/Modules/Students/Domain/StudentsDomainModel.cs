using EgitimUssu.Shared.Contracts;
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
        DateTime createdOnUtc,
        TargetExam targetExam = TargetExam.None,
        DateTime? dateOfBirth = null)
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
        TargetExam = targetExam;
        DateOfBirth = dateOfBirth;
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

    /// <summary>Bu profil başka bir kanonik profile birleştirildiyse true olur (Ö-C claim/merge).</summary>
    public bool IsMerged { get; private set; }

    /// <summary>Birleştirme sonrası kanonik (hedef) öğrenci profilinin kimliği; birleşmediyse null.</summary>
    public Guid? MergedIntoStudentId { get; private set; }

    /// <summary>Öğrencinin hedeflediği sınav; net formülü ve deneme türetimlerinde kullanılır (S-03.9).</summary>
    public TargetExam TargetExam { get; private set; }

    /// <summary>
    /// ⚠️ <b>LEGACY (P12'de silinecek)</b> — üyelik seviyesi. K-09'dan (P09 Task 2) sonra hak kararları
    /// bu alandan <b>okunmaz</b>; tek kaynak Membership modülü (<c>IEntitlementDirectory</c>). Alan yalnız
    /// geçiş köprüsünün girdisidir (veri göçü riski nedeniyle henüz silinmedi).
    /// </summary>
    public MembershipTier MembershipTier { get; private set; } = MembershipTier.Free;

    /// <summary>Öğrencinin doğum tarihi (opsiyonel). Yaş türetimi + veli claim eşleşmesi için temel (Veli V-A).</summary>
    public DateTime? DateOfBirth { get; private set; }

    /// <summary>
    /// E-07: Öğrenci profilini kimliksizleştirir.
    /// <para>
    /// Satır silinmez — ders, ödev ve ödeme kayıtları bu <c>StudentId</c>'ye bağlıdır ve bir kısmı
    /// yasal saklama süresince durmak zorundadır. Kişiyi gösteren alanlar temizlenir; sınıf düzeyi
    /// gibi kişiyi tek başına göstermeyen alanlar istatistik bütünlüğü için kalır.
    /// </para>
    /// </summary>
    /// <param name="nowUtc">Anonimleştirme anı (UTC).</param>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData(DateTime nowUtc)
    {
        if (FullName == PersonalDataPlaceholder.FullName && ContactEmail is null && ContactPhone is null
            && GoalSummary is null && LevelNotes is null && DateOfBirth is null)
        {
            return false;
        }

        FullName = PersonalDataPlaceholder.FullName;
        ContactEmail = null;
        ContactPhone = null;
        GoalSummary = null;
        LevelNotes = null;
        DateOfBirth = null;
        UpdatedOnUtc = nowUtc;
        return true;
    }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public List<StudentSubject> Subjects { get; private set; } = [];

    public void Update(
        string fullName,
        string gradeLevel,
        string? contactEmail,
        string? contactPhone,
        string? goalSummary,
        string? levelNotes,
        bool isActive,
        DateTime updatedOnUtc,
        TargetExam targetExam = TargetExam.None,
        DateTime? dateOfBirth = null)
    {
        FullName = fullName.Trim();
        GradeLevel = gradeLevel.Trim();
        ContactEmail = contactEmail?.Trim();
        ContactPhone = contactPhone?.Trim();
        GoalSummary = goalSummary?.Trim();
        LevelNotes = levelNotes?.Trim();
        IsActive = isActive;
        TargetExam = targetExam;
        DateOfBirth = dateOfBirth;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>Öğrencinin hedef sınavını günceller (S-03.9).</summary>
    public void SetTargetExam(TargetExam targetExam, DateTime updatedOnUtc)
    {
        TargetExam = targetExam;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>Öğrencinin üyelik seviyesini günceller (Ö-D). Premium satın alma / iptal akışında çağrılır.</summary>
    public void SetMembershipTier(MembershipTier tier, DateTime updatedOnUtc)
    {
        MembershipTier = tier;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>
    /// Onaylı veli–çocuk bağı sonucunda birincil veliyi ilişkilendirir (M09 ParentChildLinkApproved akışı).
    /// </summary>
    public void LinkParent(Guid parentUserId, DateTime updatedOnUtc)
    {
        ParentUserId = parentUserId;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>
    /// Öğretmen davetini kabul eden gerçek öğrenci kullanıcısını profile bağlar (B-06 davet/kabul akışı).
    /// </summary>
    public void LinkUser(Guid userId, DateTime updatedOnUtc)
    {
        UserId = userId;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>
    /// M03-2: Profili baştan güncellemeden tek bir branş ekler. Aynı branş (büyük/küçük harf
    /// duyarsız) zaten ekliyse <c>students.duplicate_subject</c> döner — kural burada, tek yerde durur.
    /// </summary>
    public Result<StudentSubject> AddSubject(Guid subjectId, string subject, string? targetLevel, DateTime updatedOnUtc)
    {
        var normalized = subject?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return Result<StudentSubject>.Failure(StudentErrors.InvalidSubject);
        }

        if (Subjects.Any(existing => string.Equals(existing.Subject, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<StudentSubject>.Failure(StudentErrors.DuplicateSubject);
        }

        var entity = new StudentSubject(subjectId, Id, normalized, targetLevel?.Trim());
        Subjects.Add(entity);
        UpdatedOnUtc = updatedOnUtc;
        return Result<StudentSubject>.Success(entity);
    }

    /// <summary>M03-2: Tek bir branşı kaldırır. Branş bu profile ait değilse <c>students.subject_not_found</c>.</summary>
    public Result RemoveSubject(Guid subjectId, DateTime updatedOnUtc)
    {
        var existing = Subjects.FirstOrDefault(subject => subject.Id == subjectId);
        if (existing is null)
        {
            return Result.Failure(StudentErrors.SubjectNotFound);
        }

        Subjects.Remove(existing);
        UpdatedOnUtc = updatedOnUtc;
        return Result.Success();
    }

    /// <summary>
    /// Bu (manuel) profili, öğrencinin mevcut kanonik self-profil'ine birleştirir (Ö-C claim/merge).
    /// Profil pasifleştirilir ve modüller-arası <c>StudentId</c> yeniden atamasını tetikleyecek
    /// <see cref="StudentProfilesMergedDomainEvent"/> yayılır (Outbox → integration event).
    /// </summary>
    public void MarkMerged(Guid canonicalStudentId, DateTime updatedOnUtc)
    {
        IsMerged = true;
        MergedIntoStudentId = canonicalStudentId;
        IsActive = false;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new StudentProfilesMergedDomainEvent(Id, canonicalStudentId, updatedOnUtc));
    }
}

/// <summary>Students modülünün domain kuralı hataları (Application ve API aynı kodları taşır).</summary>
public static class StudentErrors
{
    public static readonly Error InvalidSubject = new("students.invalid_subject", "Branş adı boş olamaz.");
    public static readonly Error DuplicateSubject = new("students.duplicate_subject", "Bu branş zaten ekli.");
    public static readonly Error SubjectNotFound = new("students.subject_not_found", "Branş bulunamadı.");
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

/// <summary>Öğrencinin hedeflediği sınav türü. Net formülü ceza katsayısını buradan türetir (LGS /3, TYT/AYT /4, School yanlış götürmez).</summary>
public enum TargetExam
{
    None = 0,
    LGS = 1,
    TYT = 2,
    AYT = 3,
    YDS = 4,
    School = 5,
    Other = 6
}

public sealed record StudentProfileCreatedDomainEvent(
    Guid StudentProfileId,
    Guid? UserId,
    Guid? CreatedByTeacherUserId,
    StudentOrigin Origin,
    DateTime CreatedOnUtc) : DomainEvent;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge) yayılır. Diğer modüller bu olayı
/// tüketerek <c>FromStudentId</c>'ye ait kayıtlarını kanonik <c>ToStudentId</c>'ye yeniden atar.
/// </summary>
public sealed record StudentProfilesMergedDomainEvent(
    Guid FromStudentId,
    Guid ToStudentId,
    DateTime OnUtc) : DomainEvent;

public sealed class TeacherStudentLink : AggregateRoot<Guid>
{
    private TeacherStudentLink()
    {
    }

    public TeacherStudentLink(Guid id, Guid teacherUserId, Guid studentId, TeacherStudentLinkStatus status, DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        StudentId = studentId;
        Status = status;
        Currency = "TRY";
        IsArchived = false;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;
    }

    public Guid TeacherUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public decimal? AgreedRateAmount { get; private set; }

    public string Currency { get; private set; } = "TRY";

    public TeacherStudentLinkStatus Status { get; private set; }

    public bool IsArchived { get; private set; }

    public Guid? InviteTargetUserId { get; private set; }

    /// <summary>Öğrencinin hesabını devralmak (claim) için girdiği tekil davet kodu (6 haneli, rakam) (Ö-C).</summary>
    public string? InviteCode { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void SetRate(decimal amount, string currency, DateTime updatedOnUtc)
    {
        AgreedRateAmount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim();
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Archive(DateTime updatedOnUtc)
    {
        IsArchived = true;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Unarchive(DateTime updatedOnUtc)
    {
        IsArchived = false;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void MarkInviteSent(string inviteCode, Guid? targetUserId, DateTime updatedOnUtc)
    {
        Status = TeacherStudentLinkStatus.InviteSent;
        InviteCode = inviteCode;
        InviteTargetUserId = targetUserId;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new TeacherStudentInviteSentDomainEvent(Id, TeacherUserId, StudentId, targetUserId, updatedOnUtc));
    }

    /// <summary>6 haneli rakamsal davet kodu üretir (Ö-C). Handler tarafından üretilip <see cref="MarkInviteSent"/>'e geçilir.</summary>
    public static string GenerateInviteCode()
        => Random.Shared.Next(0, 1_000_000).ToString("D6");

    public void Accept(DateTime updatedOnUtc)
    {
        Status = TeacherStudentLinkStatus.Linked;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new TeacherStudentLinkAcceptedDomainEvent(Id, TeacherUserId, StudentId, updatedOnUtc));
    }

    public void Reject(DateTime updatedOnUtc)
    {
        Status = TeacherStudentLinkStatus.Rejected;
        UpdatedOnUtc = updatedOnUtc;
    }
}

public enum TeacherStudentLinkStatus
{
    Manual = 1,
    InviteSent = 2,
    Linked = 3,
    Rejected = 4,
    Disconnected = 5
}

public sealed record TeacherStudentInviteSentDomainEvent(
    Guid LinkId,
    Guid TeacherUserId,
    Guid StudentId,
    Guid? TargetUserId,
    DateTime OnUtc) : DomainEvent;

public sealed record TeacherStudentLinkAcceptedDomainEvent(
    Guid LinkId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime OnUtc) : DomainEvent;

/// <summary>
/// Öğretmenin bir öğrenci için ürettiği veli davet kodu (Veli V-D). Veli kodu girerek claim eder ("veli onayı");
/// claim, Parents tarafında ParentChildLink oluşturup onaylar. Durum: Pending → Claimed.
/// </summary>
public sealed class StudentParentInvite : AggregateRoot<Guid>
{
    private StudentParentInvite() { }

    public StudentParentInvite(Guid id, Guid studentId, Guid teacherUserId, string inviteCode, string? childDisplayName, DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        TeacherUserId = teacherUserId;
        InviteCode = inviteCode;
        ChildDisplayName = childDisplayName?.Trim();
        Status = ParentInviteStatus.Pending;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid StudentId { get; private set; }
    public Guid TeacherUserId { get; private set; }
    public string InviteCode { get; private set; } = string.Empty;
    public string? ChildDisplayName { get; private set; }

    /// <summary>E-07: Davetteki çocuk adını siler; davet kaydının kendisi (durum/tarih) kalır.</summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (ChildDisplayName is null)
        {
            return false;
        }

        ChildDisplayName = null;
        return true;
    }
    public ParentInviteStatus Status { get; private set; }
    public Guid? ClaimedByParentUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ClaimedOnUtc { get; private set; }

    public void Claim(Guid parentUserId, DateTime nowUtc)
    {
        if (Status != ParentInviteStatus.Pending)
        {
            throw new InvalidOperationException("Davet zaten kullanılmış.");
        }

        Status = ParentInviteStatus.Claimed;
        ClaimedByParentUserId = parentUserId;
        ClaimedOnUtc = nowUtc;
    }

    /// <summary>
    /// A-05 telafisi: tüketilmiş daveti yeniden kullanılabilir hâle getirir. Bağ kurulamadığında
    /// çağrılır — davet Students şemasında, bağ Parents şemasında yaşadığı ve ikisini saran bir
    /// transaction olmadığı için, başarısız bir akışın veliyi kodundan etmemesi gerekir.
    /// </summary>
    /// <param name="nowUtc">Şu an (durum geçişini doğrulamak için).</param>
    public void ReleaseClaim(DateTime nowUtc)
    {
        if (Status != ParentInviteStatus.Claimed)
        {
            return;
        }

        Status = ParentInviteStatus.Pending;
        ClaimedByParentUserId = null;
        ClaimedOnUtc = null;
    }
}

public enum ParentInviteStatus
{
    Pending = 1,
    Claimed = 2
}
