using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Parents.Domain;

/// <summary>
/// Veli profili. Veli her zaman gerçek, kayıtlı bir Identity kullanıcısıdır (UserRole.Parent);
/// bu yüzden <see cref="UserId"/> zorunludur (İş Kuralları 4.1). Bildirim tercihleri düz alanlar
/// olarak tutulur (kod tabanında OwnsOne deseni yoktur; skaler-düz sütun konvansiyonu izlenir).
/// </summary>
public sealed class ParentProfile : AggregateRoot<Guid>
{
    private ParentProfile()
    {
    }

    public ParentProfile(
        Guid id,
        Guid userId,
        string fullName,
        string? contactPhone,
        string? contactEmail,
        DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        FullName = fullName;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;

        // Varsayılan bildirim tercihleri (Faz 2 için açık, öğretmen bağımlı olanlar kapalı başlar).
        NotifyMissedAssignment = true;
        NotifyWeeklyProgressSummary = true;
        NotifyLessonReminders = false;
        NotifyTestResults = true;
        NotifyPayments = false;
        NotificationChannel = ParentNotificationChannel.Push;

        IsActive = true;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new ParentProfileCreatedDomainEvent(Id, UserId, createdOnUtc));
    }

    public Guid UserId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string? ContactPhone { get; private set; }

    public string? ContactEmail { get; private set; }

    /// <summary>E-07: Veli profilini kimliksizleştirir; profil satırı bağ kayıtları için kalır.</summary>
    /// <param name="nowUtc">Anonimleştirme anı (UTC).</param>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData(DateTime nowUtc)
    {
        if (FullName == PersonalDataPlaceholder.FullName && ContactPhone is null && ContactEmail is null)
        {
            return false;
        }

        FullName = PersonalDataPlaceholder.FullName;
        ContactPhone = null;
        ContactEmail = null;
        UpdatedOnUtc = nowUtc;
        return true;
    }

    public bool NotifyMissedAssignment { get; private set; }

    public bool NotifyWeeklyProgressSummary { get; private set; }

    public bool NotifyLessonReminders { get; private set; }

    public bool NotifyTestResults { get; private set; }

    public bool NotifyPayments { get; private set; }

    public ParentNotificationChannel NotificationChannel { get; private set; }

    /// <summary>
    /// ⚠️ <b>LEGACY (P12'de silinecek)</b> — üyelik seviyesi. K-09'dan (P09 Task 2) sonra hak kararları
    /// bu alandan <b>okunmaz</b>; tek kaynak Membership modülü (<c>IEntitlementDirectory</c>). Alan yalnız
    /// geçiş köprüsünün girdisidir (veri göçü riski nedeniyle henüz silinmedi).
    /// </summary>
    public MembershipTier MembershipTier { get; private set; } = MembershipTier.Free;

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void UpdateContact(string fullName, string? contactPhone, string? contactEmail, DateTime updatedOnUtc)
    {
        FullName = fullName.Trim();
        ContactPhone = contactPhone?.Trim();
        ContactEmail = contactEmail?.Trim();
        UpdatedOnUtc = updatedOnUtc;
    }

    public void UpdateNotificationPreferences(
        bool notifyMissedAssignment,
        bool notifyWeeklyProgressSummary,
        bool notifyLessonReminders,
        bool notifyTestResults,
        bool notifyPayments,
        ParentNotificationChannel channel,
        DateTime updatedOnUtc)
    {
        NotifyMissedAssignment = notifyMissedAssignment;
        NotifyWeeklyProgressSummary = notifyWeeklyProgressSummary;
        NotifyLessonReminders = notifyLessonReminders;
        NotifyTestResults = notifyTestResults;
        NotifyPayments = notifyPayments;
        NotificationChannel = channel;
        UpdatedOnUtc = updatedOnUtc;
    }

    /// <summary>Velinin üyelik seviyesini günceller (Veli V-E). Satın alma altyapısı gelene kadar Admin set eder.</summary>
    public void SetMembershipTier(MembershipTier tier, DateTime updatedOnUtc)
    {
        MembershipTier = tier;
        UpdatedOnUtc = updatedOnUtc;
    }
}

/// <summary>
/// Veli–öğrenci bağı. Onaya dayalı, çoklu çocuk destekli (İş Kuralları 4.2). Bağ <c>Pending</c> doğar,
/// öğrenci ya da öğrencinin öğretmeni (ya da Admin) onaylar. Bir veli birden çok çocuğa bağlanabilir.
/// </summary>
public sealed class ParentChildLink : AggregateRoot<Guid>, IVersioned
{
    public uint Version { get; private set; }

    private static readonly Error InvalidTransition = new(
        "parents.invalid_link_transition",
        "Bu bağ artık onaylanamaz; yalnız onay bekleyen bir bağ onaylanabilir.");

    private ParentChildLink()
    {
    }

    public ParentChildLink(
        Guid id,
        Guid parentUserId,
        Guid studentId,
        string? childDisplayName,
        string? relationship,
        string? inviteCode,
        bool isPrimaryContact,
        DateTime requestedOnUtc)
    {
        Id = id;
        ParentUserId = parentUserId;
        StudentId = studentId;
        ChildDisplayName = childDisplayName?.Trim();
        Relationship = relationship?.Trim();
        InviteCode = inviteCode?.Trim();
        IsPrimaryContact = isPrimaryContact;
        Status = ParentChildLinkStatus.Pending;
        RequestedOnUtc = requestedOnUtc;
        UpdatedOnUtc = requestedOnUtc;

        Raise(new ParentChildLinkRequestedDomainEvent(Id, ParentUserId, StudentId, requestedOnUtc));
    }

    public Guid ParentUserId { get; private set; }

    public Guid StudentId { get; private set; }

    public string? ChildDisplayName { get; private set; }

    public string? Relationship { get; private set; }

    /// <summary>
    /// E-07: Veli–çocuk bağındaki kişisel alanları siler. Bağın <b>kendisi</b> (durum, tarih) kalır:
    /// hangi hesabın hangi öğrenciye erişebildiği yetki geçmişidir, hesap verebilirliğin parçasıdır.
    /// </summary>
    /// <param name="nowUtc">Anonimleştirme anı (UTC).</param>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData(DateTime nowUtc)
    {
        if (ChildDisplayName is null && Relationship is null)
        {
            return false;
        }

        ChildDisplayName = null;
        Relationship = null;
        UpdatedOnUtc = nowUtc;
        return true;
    }

    public string? InviteCode { get; private set; }

    public bool IsPrimaryContact { get; private set; }

    public ParentChildLinkStatus Status { get; private set; }

    public DateTime RequestedOnUtc { get; private set; }

    public DateTime? LinkedOnUtc { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public bool IsApproved => Status == ParentChildLinkStatus.Approved;

    /// <summary>
    /// Bekleyen bağı onaylar. P15 / N-26: yalnız <c>Pending</c>'den — reddedilmiş ya da iptal edilmiş bağ yeniden
    /// onaylanamaz (veli yeni talep açar), onaylı bağın ikinci onayı da reddedilir (bağlanma anı ve olaylar tekrarlanmaz).
    /// </summary>
    /// <param name="approvedByUserId">Onaylayan kullanıcı (öğrenci, Admin ya da davet kodunu kullanan veli).</param>
    /// <param name="existingPrimaryParentUserId">Bildirim için mevcut birincil veli (varsa).</param>
    /// <param name="nowUtc">Onay anı.</param>
    /// <returns>Geçiş geçersizse <c>parents.invalid_link_transition</c>.</returns>
    public Result Approve(Guid approvedByUserId, Guid? existingPrimaryParentUserId, DateTime nowUtc)
    {
        if (Status != ParentChildLinkStatus.Pending)
        {
            return Result.Failure(InvalidTransition);
        }

        Status = ParentChildLinkStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        LinkedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;

        Raise(new ParentChildLinkApprovedDomainEvent(Id, ParentUserId, StudentId, IsPrimaryContact, nowUtc));
        Raise(new ParentLinkConnectionNoticeDomainEvent(
            Id, StudentId, ParentUserId, existingPrimaryParentUserId, IsPrimaryContact, nowUtc));
        return Result.Success();
    }

    /// <summary>Bekleyen ya da onaylı bağı reddeder; yaşayan bağ biterken <see cref="ParentChildLinkEndedDomainEvent"/> yayar.</summary>
    /// <param name="rejectedByUserId">Reddeden kullanıcı.</param>
    /// <param name="nowUtc">Ret anı.</param>
    public void Reject(Guid rejectedByUserId, DateTime nowUtc)
    {
        if (Status is ParentChildLinkStatus.Rejected or ParentChildLinkStatus.Revoked)
        {
            return;
        }

        Status = ParentChildLinkStatus.Rejected;
        ApprovedByUserId = rejectedByUserId;
        UpdatedOnUtc = nowUtc;

        Raise(new ParentChildLinkEndedDomainEvent(Id, ParentUserId, StudentId, ParentChildLinkEndReason.Rejected, nowUtc));
    }

    /// <summary>
    /// Bağı iptal eder. Bitiş olayı yalnız <b>yaşayan</b> (<c>Pending</c>/<c>Approved</c>) bağ biterken yayılır: eski bir
    /// ret kaydını iptale çekmek, aynı velinin daha yeni onaylı bağını etkileyecek bir olay üretmemeli.
    /// </summary>
    /// <param name="nowUtc">İptal anı.</param>
    public void Revoke(DateTime nowUtc)
    {
        if (Status == ParentChildLinkStatus.Revoked)
        {
            return;
        }

        var wasLive = Status is ParentChildLinkStatus.Pending or ParentChildLinkStatus.Approved;
        Status = ParentChildLinkStatus.Revoked;
        UpdatedOnUtc = nowUtc;

        if (wasLive)
        {
            Raise(new ParentChildLinkEndedDomainEvent(Id, ParentUserId, StudentId, ParentChildLinkEndReason.Revoked, nowUtc));
        }
    }

    public void SetChildDisplayName(string? childDisplayName, DateTime nowUtc)
    {
        ChildDisplayName = childDisplayName?.Trim();
        UpdatedOnUtc = nowUtc;
    }
}

public enum ParentChildLinkStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Revoked = 4
}

public enum ParentNotificationChannel
{
    Push = 1,
    Email = 2,
    Both = 3
}

public sealed record ParentProfileCreatedDomainEvent(
    Guid ParentProfileId,
    Guid UserId,
    DateTime CreatedOnUtc) : DomainEvent;

public sealed record ParentChildLinkRequestedDomainEvent(
    Guid LinkId,
    Guid ParentUserId,
    Guid StudentId,
    DateTime RequestedOnUtc) : DomainEvent;

public sealed record ParentChildLinkApprovedDomainEvent(
    Guid LinkId,
    Guid ParentUserId,
    Guid StudentId,
    bool IsPrimaryContact,
    DateTime ApprovedOnUtc) : DomainEvent;

/// <summary>
/// P15 / N-26: Yaşayan bir bağ bitti (ret ya da iptal). Integration yükü <c>ParentChildLinkEndedIntegrationEvent</c>
/// (Shared/Contracts); Students profilin <c>ParentUserId</c>'sini temizler. Tüketicisi olmayan eski
/// <c>ParentChildLinkRejected</c>/<c>Revoked</c> olaylarının yerini aldı (nedeni <see cref="Reason"/> taşır).
/// </summary>
public sealed record ParentChildLinkEndedDomainEvent(
    Guid LinkId,
    Guid ParentUserId,
    Guid StudentId,
    ParentChildLinkEndReason Reason,
    DateTime EndedOnUtc) : DomainEvent;

/// <summary>
/// "Sessizce bağlanma yok" (Veli V-C): bir veli–çocuk bağı onaylandığında şeffaflık için yayılır.
/// Alıcılar (V-E bildirim motoru teslim eder): <c>StudentId</c> = çocuk ve varsa
/// <c>ExistingPrimaryParentUserId</c> = mevcut birincil veli — "X hesabı veli olarak bağlandı".
/// </summary>
public sealed record ParentLinkConnectionNoticeDomainEvent(
    Guid LinkId,
    Guid StudentId,
    Guid ConnectedParentUserId,
    Guid? ExistingPrimaryParentUserId,
    bool IsPrimaryContact,
    DateTime ConnectedOnUtc) : DomainEvent;
