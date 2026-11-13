namespace EgitimUssu.Shared.Contracts;

/// <summary>Veli–çocuk bağı talebinin sonucu (M03-1).</summary>
public enum ParentLinkRequestOutcome
{
    /// <summary>Onay bekleyen (<c>Pending</c>) bağ açıldı.</summary>
    Created = 1,

    /// <summary>Verilen kullanıcı kimliğine ait bir veli profili yok — veli daima kayıtlı kullanıcıdır.</summary>
    ParentNotRegistered = 2,

    /// <summary>Bu veli ile bu öğrenci arasında zaten açık (Pending/Approved) bir bağ var.</summary>
    AlreadyLinked = 3
}

/// <summary>
/// Parents uygular; Students tüketir. Öğretmen mevcut bir veli kullanıcısını öğrenciye bağlamak
/// istediğinde (M03-1) bağın kendisi <b>Parents</b> tarafında açılır: bağ <c>Pending</c> başlar ve
/// mevcut <c>POST /api/parents/children/{linkId}/approve</c> akışıyla (öğrenci/öğretmen/Admin) onaylanır.
/// Onaylandığında <c>ParentChildLinkApproved</c> olayı Students'a düşer ve
/// <c>StudentProfile.ParentUserId</c> orada dolar.
/// </summary>
public interface IParentChildLinkRequestService
{
    Task<ParentLinkRequestOutcome> RequestPendingLinkAsync(
        Guid parentUserId,
        Guid studentId,
        string? childDisplayName,
        CancellationToken cancellationToken);
}
