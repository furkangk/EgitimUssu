using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Membership.Application;

/// <summary>
/// K-09: Üyelik seviyesinden <see cref="Entitlement"/> üretmenin <b>tek</b> yeri. Başka hiçbir modül
/// bu eşlemeyi kendi içinde tekrar etmez; hepsi <see cref="IEntitlementDirectory"/> üzerinden okur.
/// </summary>
public static class EntitlementPolicy
{
    /// <summary>Free üyelikte bir öğretmenin bağlayabileceği en fazla öğrenci sayısı.</summary>
    public const int FreeMaxStudents = 5;

    /// <summary>Aboneliği olmayan / dönemi kapanmış kullanıcının hakları.</summary>
    public static readonly Entitlement Free = For(MembershipTier.Free, null);

    /// <summary>Verilen seviyeye karşılık gelen hakları üretir.</summary>
    /// <param name="tier">Etkin üyelik seviyesi.</param>
    /// <param name="effectiveUntilUtc">Premium'un geçerli olduğu son an (Free için <c>null</c>).</param>
    /// <returns>Hak kaydı.</returns>
    public static Entitlement For(MembershipTier tier, DateTime? effectiveUntilUtc)
        => tier == MembershipTier.Premium
            ? new Entitlement(
                MembershipTier.Premium,
                effectiveUntilUtc,
                MaxStudents: int.MaxValue,
                CanSeeAds: false,
                DetailedAnalytics: true,
                PdfReports: true,
                RichNotifications: true)
            : new Entitlement(
                MembershipTier.Free,
                EffectiveUntilUtc: null,
                MaxStudents: FreeMaxStudents,
                CanSeeAds: true,
                DetailedAnalytics: false,
                PdfReports: false,
                RichNotifications: false);
}

/// <summary>
/// Bir kullanıcının üyelik durumunun okuma görünümü: etkin seviye + dönem + plan.
/// </summary>
/// <param name="Tier">Etkin üyelik seviyesi.</param>
/// <param name="EffectiveUntilUtc">Premium'un geçerli olduğu son an; Free ise <c>null</c>.</param>
/// <param name="Status">Abonelik durumu; abonelik kaydı yoksa <c>null</c>.</param>
/// <param name="PlanCode">Abone olunan planın kararlı kodu; kayıt yoksa <c>null</c>.</param>
public sealed record UserMembershipSnapshot(
    MembershipTier Tier,
    DateTime? EffectiveUntilUtc,
    SubscriptionStatus? Status,
    string? PlanCode);

/// <summary>
/// Üyelik durumunun modül-içi okuma sözleşmesi (<c>GET /api/membership/me</c> için).
/// Modüller-arası okuma <see cref="IEntitlementDirectory"/> ile yapılır; bu sözleşme Membership'e özeldir.
/// </summary>
public interface IMembershipReader
{
    /// <summary>Kullanıcının üyelik durumunu döner.</summary>
    /// <param name="userId">Kullanıcı kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Durum görünümü; abonelik yoksa Free.</returns>
    Task<UserMembershipSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>M17: Oturumdaki kullanıcının üyelik durumu ve hakları.</summary>
public sealed record GetMyMembershipQuery : IQuery<Result<MembershipStatusResponse>>;

/// <summary>Üyelik durumu yanıtı.</summary>
/// <param name="Tier">Etkin seviye (<c>Free</c> | <c>Premium</c>).</param>
/// <param name="EffectiveUntilUtc">Premium'un geçerli olduğu son an; Free ise <c>null</c>.</param>
/// <param name="PlanCode">Abone olunan plan kodu; yoksa <c>null</c>.</param>
/// <param name="Status">Abonelik durumu; yoksa <c>null</c>.</param>
/// <param name="Entitlement">Kullanıcının hakları (mobil paywall ve reklam yerleşimi bunu okur).</param>
public sealed record MembershipStatusResponse(
    string Tier,
    DateTime? EffectiveUntilUtc,
    string? PlanCode,
    string? Status,
    Entitlement Entitlement);

/// <summary>Üyelik modülünün hata kodlarının Application karşılıkları.</summary>
public static class MembershipApplicationErrors
{
    /// <summary>Oturum kimliği çözülemedi.</summary>
    public static readonly Error Forbidden =
        new("shared.forbidden", "Üyelik bilgisine erişmek için oturum açmalısınız.");
}

/// <summary>Oturumdaki kullanıcının üyelik durumunu döner.</summary>
public sealed class GetMyMembershipQueryHandler : IQueryHandler<GetMyMembershipQuery, Result<MembershipStatusResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipReader _reader;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="reader">Üyelik okuma sözleşmesi.</param>
    public GetMyMembershipQueryHandler(ICurrentUser currentUser, IMembershipReader reader)
    {
        _currentUser = currentUser;
        _reader = reader;
    }

    /// <summary>Sorguyu çalıştırır.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Üyelik durumu.</returns>
    public async Task<Result<MembershipStatusResponse>> Handle(
        GetMyMembershipQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<MembershipStatusResponse>.Failure(MembershipApplicationErrors.Forbidden);
        }

        var snapshot = await _reader.GetSnapshotAsync(userId, cancellationToken);

        return Result<MembershipStatusResponse>.Success(new MembershipStatusResponse(
            snapshot.Tier.ToString(),
            snapshot.EffectiveUntilUtc,
            snapshot.PlanCode,
            snapshot.Status?.ToString(),
            EntitlementPolicy.For(snapshot.Tier, snapshot.EffectiveUntilUtc)));
    }
}

/// <summary>
/// <c>GET /api/membership/me</c> yalnız kullanıcının <b>kendi</b> üyeliğini döner (sorgu parametre
/// almaz), bu yüzden kapı tek koşuldur: oturum açılmış olmalı.
/// </summary>
public sealed class GetMyMembershipQueryAuthorizer : IQueryAuthorizer<GetMyMembershipQuery>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    public GetMyMembershipQueryAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Yetkiyi denetler.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(GetMyMembershipQuery query, CancellationToken cancellationToken)
        => Task.FromResult(
            _currentUser.IsAuthenticated && Guid.TryParse(_currentUser.UserId, out _)
                ? Result.Success()
                : Result.Failure(MembershipApplicationErrors.Forbidden));
}
