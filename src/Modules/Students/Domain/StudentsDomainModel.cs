using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Domain;

public sealed class StudentProfile : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

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
        EnsureOwnershipInvariant(origin, userId, createdByTeacherUserId, parentUserId);

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
    /// P15 / N-26: Veli–çocuk bağı bitince (ret/iptal) birincil veli göstergesini temizler — yalnız alan o veliyi
    /// gösteriyorsa; başka bir velinin bağının bitişi birincil veliyi silmez.
    /// </summary>
    /// <param name="parentUserId">Bağı biten veli.</param>
    /// <param name="updatedOnUtc">Bitiş anı.</param>
    /// <returns>Alan değiştiyse <c>true</c>.</returns>
    public bool UnlinkParent(Guid parentUserId, DateTime updatedOnUtc)
    {
        if (ParentUserId != parentUserId)
        {
            return false;
        }

        ParentUserId = null;
        UpdatedOnUtc = updatedOnUtc;
        return true;
    }

    /// <summary>
    /// Öğretmen davetini kabul eden gerçek öğrenci kullanıcısını profile bağlar (B-06 davet/kabul akışı).
    /// <para>
    /// P15 / N-22, N-23: Profilin sahibi bir kez belirlenir. Aynı kullanıcıya tekrar bağlamak değişiklik
    /// yapmaz; başka bir kullanıcıya ait profil <c>students.profile_already_claimed</c> ile korunur. Bağlama
    /// <see cref="StudentProfileLinkedToUserDomainEvent"/> yayar (Parents'ın öğrenci→kullanıcı eşlemesi, T4).
    /// </para>
    /// </summary>
    /// <param name="userId">Daveti kabul eden öğrenci kullanıcısı.</param>
    /// <param name="updatedOnUtc">Bağlama anı.</param>
    /// <returns>İşlem sonucu.</returns>
    public Result LinkUser(Guid userId, DateTime updatedOnUtc)
    {
        var claimable = EnsureClaimableBy(userId);
        if (claimable.IsFailure || UserId == userId)
        {
            return claimable;
        }

        UserId = userId;
        UpdatedOnUtc = updatedOnUtc;
        Raise(new StudentProfileLinkedToUserDomainEvent(Id, userId, updatedOnUtc) { OccurredOnUtc = updatedOnUtc });
        return Result.Success();
    }

    /// <summary>
    /// P15 / N-23: Bu profil verilen kullanıcı tarafından devralınabilir mi? Başka bir hesaba bağlı ya da
    /// başka bir profile birleşmiş profil devralınamaz (birleştirmenin kaynağı da olamaz).
    /// </summary>
    /// <param name="userId">Devralmak isteyen kullanıcı.</param>
    /// <returns>Devralınabilirse başarı.</returns>
    public Result EnsureClaimableBy(Guid userId)
    {
        if (IsMerged || (UserId is { } owner && owner != userId))
        {
            return Result.Failure(StudentErrors.ProfileAlreadyClaimed);
        }

        return Result.Success();
    }

    /// <summary>
    /// P15 / N-23: Profilin kime ait olduğu istek gövdesinden gelmez. Öğretmenin açtığı profil hesapsız ve
    /// velisiz doğar (hesap davetle, veli onaylı bağla gelir); öğrencinin açtığı profil kendi hesabına aittir,
    /// öğretmen ve veli taşımaz. Uygulama katmanı bunu <c>400 students.field_not_allowed</c> ile reddeder;
    /// buradaki koruma programlama hatasına karşıdır.
    /// </summary>
    private static void EnsureOwnershipInvariant(StudentOrigin origin, Guid? userId, Guid? createdByTeacherUserId, Guid? parentUserId)
    {
        var valid = origin switch
        {
            StudentOrigin.TeacherManaged => userId is null && parentUserId is null && createdByTeacherUserId is not null,
            StudentOrigin.SelfRegistered => userId is not null && parentUserId is null && createdByTeacherUserId is null,
            _ => false
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"{origin} öğrenci profili kimlik alanlarıyla uyumsuz (userId, createdByTeacherUserId, parentUserId).",
                nameof(origin));
        }
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

    // P15 Task 3 (N-22, N-23)
    public static readonly Error InvalidLinkTransition = new("students.invalid_link_transition", "Bu davet artık yanıtlanamaz.");
    public static readonly Error InviteCodeMismatch = new("students.invite_code_mismatch", "Davet kodu eşleşmiyor.");
    public static readonly Error InviteNotForUser = new("students.invite_not_for_user", "Bu davet başka bir kullanıcıya gönderilmiş.");
    public static readonly Error InviteExpired = new("students.invite_expired", "Davet kodunun süresi dolmuş. Öğretmeninden yeni bir kod iste.");
    public static readonly Error LinkAlreadyActive = new("students.link_already_active", "Öğrenci bu öğretmene zaten bağlı; yeni davet gerekmez.");
    public static readonly Error ProfileAlreadyClaimed = new("students.profile_already_claimed", "Bu öğrenci profili başka bir hesaba bağlı.");
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
/// <summary>
/// P15 / N-23: Öğretmenin açtığı profil davet kabulüyle bir öğrenci hesabına bağlandı. Integration yükü
/// <c>StudentProfileLinkedToUserIntegrationEvent</c> (Shared/Contracts). <c>OccurredOnUtc</c> bağlama anına
/// eşitlenir — record parametresi olarak verilmez (taban sınıfla çakışır, CS8907; Öğrenilenler #63).
/// </summary>
public sealed record StudentProfileLinkedToUserDomainEvent(
    Guid StudentId,
    Guid UserId,
    DateTime LinkedOnUtc) : DomainEvent;

public sealed record StudentProfilesMergedDomainEvent(
    Guid FromStudentId,
    Guid ToStudentId,
    DateTime OnUtc) : DomainEvent;

public sealed class TeacherStudentLink : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

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

    /// <summary>
    /// Öğrencinin hesabını devralmak (claim) için girdiği tekil davet kodu (Ö-C). P15 / N-20: 8 karakter,
    /// <see cref="SecureCode.Alphabet"/>'ten; eski 6 haneli sayısal kodlar süreleri dolana kadar geçerli kalır.
    /// </summary>
    public string? InviteCode { get; private set; }

    /// <summary>P15 / N-20: Davetin son geçerlilik anı; <c>null</c> = davet yok.</summary>
    public DateTime? InviteExpiresOnUtc { get; private set; }

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

    /// <summary>Daveti gönderilmiş işaretler. Kod <see cref="ISecureCodeGenerator"/> ile üretilip buraya geçilir.</summary>
    /// <param name="inviteCode">Davet kodu (normalize edilerek saklanır).</param>
    /// <param name="targetUserId">Daveti yalnız bu kullanıcı yanıtlayabilir; <c>null</c> = açık davet.</param>
    /// <param name="expiresOnUtc">Son geçerlilik anı.</param>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>İşlem sonucu.</returns>
    public Result MarkInviteSent(string inviteCode, Guid? targetUserId, DateTime expiresOnUtc, DateTime nowUtc)
    {
        // N-22: kurulmuş bağa ikinci davet, ikinci bir claim'in mevcut sahibin üstüne yazmasının kapısıydı.
        if (Status == TeacherStudentLinkStatus.Linked)
        {
            return Result.Failure(StudentErrors.LinkAlreadyActive);
        }

        Status = TeacherStudentLinkStatus.InviteSent;
        InviteCode = SecureCode.Normalize(inviteCode);
        InviteTargetUserId = targetUserId;
        InviteExpiresOnUtc = expiresOnUtc;
        UpdatedOnUtc = nowUtc;
        Raise(new TeacherStudentInviteSentDomainEvent(Id, TeacherUserId, StudentId, targetUserId, nowUtc));
        return Result.Success();
    }

    /// <summary>Davetin süresi dolmuş mu? Süre bilgisi olmayan davet (göç öncesi) dolmuş sayılmaz.</summary>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>Dolmuşsa <c>true</c>.</returns>
    public bool IsInviteExpired(DateTime nowUtc) => InviteExpiresOnUtc is { } expires && nowUtc >= expires;

    /// <summary>
    /// P15 / N-22: Davet yanıtlanabilir mi? Önce <b>kod</b> sınanır — link kimliğini bilen ama kodu bilmeyen
    /// biri bağın durumunu da öğrenemez. Sonra durum (yalnız <c>InviteSent</c>), hedef kullanıcı ve süre.
    /// </summary>
    /// <param name="actingUserId">Daveti yanıtlayan kullanıcı.</param>
    /// <param name="inviteCode">Kullanıcının girdiği kod (normalize edilerek karşılaştırılır).</param>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>Yanıtlanabilirse başarı.</returns>
    public Result CanRespond(Guid actingUserId, string? inviteCode, DateTime nowUtc)
    {
        if (InviteCode is null || !string.Equals(SecureCode.Normalize(inviteCode), InviteCode, StringComparison.Ordinal))
        {
            return Result.Failure(StudentErrors.InviteCodeMismatch);
        }

        if (Status != TeacherStudentLinkStatus.InviteSent)
        {
            return Result.Failure(StudentErrors.InvalidLinkTransition);
        }

        if (InviteTargetUserId is { } target && target != actingUserId)
        {
            return Result.Failure(StudentErrors.InviteNotForUser);
        }

        return IsInviteExpired(nowUtc) ? Result.Failure(StudentErrors.InviteExpired) : Result.Success();
    }

    /// <summary>P15 / N-22: Daveti kabul eder — yalnız <see cref="CanRespond"/> geçerse <c>Linked</c> olur.</summary>
    /// <param name="actingUserId">Daveti kabul eden kullanıcı.</param>
    /// <param name="inviteCode">Davet kodu.</param>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>İşlem sonucu.</returns>
    public Result Accept(Guid actingUserId, string? inviteCode, DateTime nowUtc)
    {
        var respondable = CanRespond(actingUserId, inviteCode, nowUtc);
        if (respondable.IsFailure)
        {
            return respondable;
        }

        Status = TeacherStudentLinkStatus.Linked;
        UpdatedOnUtc = nowUtc;
        Raise(new TeacherStudentLinkAcceptedDomainEvent(Id, TeacherUserId, StudentId, nowUtc));
        return Result.Success();
    }

    /// <summary>
    /// P15 / N-22: Daveti reddeder — kabulle aynı kanıtı ister. Eskiden açık davetin link kimliğini bilen
    /// herkes kurulmuş (<c>Linked</c>) bağı bile <c>Rejected</c>'a çekip öğretmenin erişimini düşürebiliyordu.
    /// </summary>
    /// <param name="actingUserId">Daveti reddeden kullanıcı.</param>
    /// <param name="inviteCode">Davet kodu.</param>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>İşlem sonucu.</returns>
    public Result Reject(Guid actingUserId, string? inviteCode, DateTime nowUtc)
    {
        var respondable = CanRespond(actingUserId, inviteCode, nowUtc);
        if (respondable.IsFailure)
        {
            return respondable;
        }

        Status = TeacherStudentLinkStatus.Rejected;
        UpdatedOnUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>P15 / N-28: Profil birleşmesinde bağı kanonik öğrenci kimliğine taşır.</summary>
    /// <param name="canonicalStudentId">Kanonik profilin kimliği.</param>
    /// <param name="nowUtc">Şu an.</param>
    public void ReassignStudent(Guid canonicalStudentId, DateTime nowUtc)
    {
        StudentId = canonicalStudentId;
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>P15 / N-28: Birleşmede düşen bağın anlaşılmış ücretini, kendi ücreti yoksa devralır.</summary>
    /// <param name="dropped">Birleşmede düşen bağ.</param>
    /// <param name="nowUtc">Şu an.</param>
    public void AdoptRateFrom(TeacherStudentLink dropped, DateTime nowUtc)
    {
        if (AgreedRateAmount is null && dropped.AgreedRateAmount is { } amount)
        {
            SetRate(amount, dropped.Currency, nowUtc);
        }
    }
}

/// <summary>
/// P15 / N-28: Profil birleştirme kuralları. Manuel (öğretmenin açtığı) profil kanonik profile birleşirken
/// öğretmen bağları kanonik kimliğe taşınır. Aynı öğretmenin iki tarafta da bağı varsa <b>biri</b> kalır
/// (<c>(TeacherUserId, StudentId)</c> benzersiz): arşivli olan düşer; ikisi de yaşıyorsa daha güçlü durum
/// (kabul edilen davet = <c>Linked</c>) kalır; eşitlikte kanonik taraftaki mevcut bağ korunur. Kalan bağ,
/// ücreti yoksa düşenin ücretini devralır.
/// </summary>
public static class StudentMergePolicy
{
    /// <summary>Bağları taşır ve silinmesi gerekenleri döndürür.</summary>
    /// <param name="fromLinks">Birleşen (manuel) profilin bağları.</param>
    /// <param name="canonicalLinks">Kanonik profilin bağları.</param>
    /// <param name="canonicalStudentId">Kanonik profilin kimliği.</param>
    /// <param name="acceptingLinkId">Birleşmeyi tetikleyen, kabul edilmek üzere olan bağ (sıralamada <c>Linked</c> sayılır).</param>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>Depodan silinecek bağlar.</returns>
    public static IReadOnlyList<TeacherStudentLink> MergeTeacherLinks(
        IReadOnlyCollection<TeacherStudentLink> fromLinks,
        IReadOnlyCollection<TeacherStudentLink> canonicalLinks,
        Guid canonicalStudentId,
        Guid? acceptingLinkId,
        DateTime nowUtc)
    {
        var dropped = new List<TeacherStudentLink>();
        foreach (var incoming in fromLinks)
        {
            var existing = canonicalLinks.FirstOrDefault(link => link.TeacherUserId == incoming.TeacherUserId);
            if (existing is null)
            {
                incoming.ReassignStudent(canonicalStudentId, nowUtc);
                continue;
            }

            var incomingWins = Rank(incoming, acceptingLinkId) > Rank(existing, acceptingLinkId);
            var (winner, loser) = incomingWins ? (incoming, existing) : (existing, incoming);
            winner.AdoptRateFrom(loser, nowUtc);
            if (incomingWins)
            {
                incoming.ReassignStudent(canonicalStudentId, nowUtc);
            }

            dropped.Add(loser);
        }

        return dropped;
    }

    private static int Rank(TeacherStudentLink link, Guid? acceptingLinkId)
    {
        var status = link.Id == acceptingLinkId ? TeacherStudentLinkStatus.Linked : link.Status;
        var statusRank = status switch
        {
            TeacherStudentLinkStatus.Linked => 3,
            TeacherStudentLinkStatus.InviteSent or TeacherStudentLinkStatus.Manual => 2,
            _ => 0
        };

        return (link.IsArchived ? 0 : 10) + statusRank;
    }
}

/// <summary>P15 / N-20 (K-24): Davet kodu kuralları.</summary>
public static class InvitePolicy
{
    /// <summary>Öğrenci ve veli davetlerinin geçerlilik süresi (gün).</summary>
    public const int ExpiryDays = 7;
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

    public StudentParentInvite(Guid id, Guid studentId, Guid teacherUserId, string inviteCode, string? childDisplayName, DateTime createdOnUtc, DateTime expiresOnUtc)
    {
        Id = id;
        StudentId = studentId;
        TeacherUserId = teacherUserId;
        InviteCode = SecureCode.Normalize(inviteCode);
        ChildDisplayName = childDisplayName?.Trim();
        Status = ParentInviteStatus.Pending;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>P15 / N-20: Davetin son geçerlilik anı.</summary>
    public DateTime ExpiresOnUtc { get; private set; }

    /// <summary>Davetin süresi dolmuş mu?</summary>
    /// <param name="nowUtc">Şu an.</param>
    /// <returns>Dolmuşsa <c>true</c>.</returns>
    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresOnUtc;

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

    /// <summary>P15 / N-28: Profil birleşmesinde bekleyen daveti kanonik öğrenci kimliğine taşır.</summary>
    /// <param name="canonicalStudentId">Kanonik profilin kimliği.</param>
    public void ReassignStudent(Guid canonicalStudentId)
    {
        StudentId = canonicalStudentId;
    }

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
