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

// ---- M17-1/M17-3: Plan kataloğu, deneme ve kampanya (P09 Task 3) ------------------------------

/// <summary>Kampanya sabitleri — kural tek yerde durur, handler'lar sayı gömmez.</summary>
public static class MembershipCampaigns
{
    /// <summary>"Arkadaşını getir": davet edene ve gelene verilen ay sayısı.</summary>
    public const int ReferralRewardMonths = 1;

    /// <summary>Plan bir role atanabilir mi (Admin muaf değil — plan yalnız satılabilir rollere açılır).</summary>
    public static bool IsSellableRole(string role)
        => role is "Teacher" or "Student" or "Parent";
}

/// <summary>Membership modülünün yazma tarafının depo sözleşmesi.</summary>
public interface IMembershipRepository
{
    /// <summary>Etkin planları listeler; <paramref name="role"/> verilirse yalnız o role ait olanları.</summary>
    Task<IReadOnlyList<SubscriptionPlan>> ListActivePlansAsync(string? role, CancellationToken cancellationToken);

    /// <summary>Plan kodunu (kararlı anahtar) çözer.</summary>
    Task<SubscriptionPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Planı kimliğiyle çözer (izlenen — güncelleme için).</summary>
    Task<SubscriptionPlan?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>Yeni plan ekler.</summary>
    Task AddPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken);

    /// <summary>Kullanıcının (tek) abonelik kaydını izlenen olarak getirir.</summary>
    Task<UserSubscription?> GetSubscriptionByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Yeni abonelik kaydı ekler.</summary>
    Task AddSubscriptionAsync(UserSubscription subscription, CancellationToken cancellationToken);

    /// <summary>Kullanıcının referans kodunu getirir; yoksa <c>null</c>.</summary>
    Task<ReferralCode?> GetReferralCodeByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Kodu metniyle çözer.</summary>
    Task<ReferralCode?> GetReferralCodeByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Yeni referans kodu ekler.</summary>
    Task AddReferralCodeAsync(ReferralCode referralCode, CancellationToken cancellationToken);

    /// <summary>Kullanıcı daha önce herhangi bir referans ödülü aldı mı.</summary>
    Task<bool> HasRedeemedAsync(Guid redeemerUserId, CancellationToken cancellationToken);

    /// <summary>Kullanımı kaydeder.</summary>
    Task AddRedemptionAsync(ReferralRedemption redemption, CancellationToken cancellationToken);

    /// <summary>Değişiklikleri yazar.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Application katmanının hata kodları (domain kuralı olmayan, akış düzeyi hatalar).</summary>
public static class MembershipPlanErrors
{
    public static readonly Error PlanNotFound =
        new("membership.plan_not_found", "Plan bulunamadı.");

    public static readonly Error PlanCodeConflict =
        new("membership.plan_code_conflict", "Bu plan kodu zaten kullanılıyor.");

    public static readonly Error InvalidPlan =
        new("membership.invalid_plan", "Plan bilgileri geçersiz.");

    public static readonly Error TrialNotAvailable =
        new("membership.trial_not_available", "Bu plan ücretsiz deneme sunmuyor.");

    public static readonly Error SubscriptionNotFound =
        new("membership.subscription_not_found", "İptal edilecek bir aboneliğiniz yok.");

    public static readonly Error InvalidReferral =
        new("membership.invalid_referral", "Referans kodu geçersiz.");

    public static readonly Error SelfReferral =
        new("membership.self_referral", "Kendi referans kodunuzu kullanamazsınız.");

    public static readonly Error ReferralAlreadyUsed =
        new("membership.referral_already_used", "Referans kodu hakkınızı zaten kullandınız.");
}

/// <summary>Satılabilir plan kaydının istemci görünümü.</summary>
/// <param name="Id">Plan kimliği (Admin güncellemesi için).</param>
/// <param name="Code">Kararlı plan kodu.</param>
/// <param name="Role">Hitap ettiği rol.</param>
/// <param name="Tier">Üyelik seviyesi.</param>
/// <param name="Period">Faturalama periyodu.</param>
/// <param name="Price">Fiyat.</param>
/// <param name="Currency">Para birimi.</param>
/// <param name="TrialDays">Ücretsiz deneme günü (0 = yok).</param>
/// <param name="IsActive">Satışta mı.</param>
public sealed record SubscriptionPlanResponse(
    Guid Id,
    string Code,
    string Role,
    string Tier,
    string Period,
    decimal Price,
    string Currency,
    int TrialDays,
    bool IsActive);

/// <summary>Kullanıcının referans kodu.</summary>
/// <param name="Code">Paylaşılacak kod.</param>
/// <param name="RedemptionCount">Kaç kişi kullandı.</param>
/// <param name="RewardMonths">Kullanan ve kullandıran için ödül ayı.</param>
public sealed record ReferralCodeResponse(string Code, int RedemptionCount, int RewardMonths);

/// <summary>
/// M17-1: Satıştaki planlar. <b>Anonim</b> erişilebilir: paywall ekranı, kullanıcı giriş yapmadan da
/// fiyatları göstermek zorundadır.
/// </summary>
/// <param name="Role">Filtre; boş bırakılırsa tüm roller.</param>
public sealed record ListPlansQuery(string? Role)
    : IQuery<Result<IReadOnlyList<SubscriptionPlanResponse>>>, IAllowAnonymous;

/// <summary>M17-1: Admin — yeni plan tanımlar.</summary>
public sealed record CreateSubscriptionPlanCommand(
    string Code,
    string Role,
    string Tier,
    string Period,
    decimal Price,
    string? Currency,
    int TrialDays,
    bool IsActive) : ICommand<Result<SubscriptionPlanResponse>>;

/// <summary>
/// M17-1: Admin — planın fiyat/deneme/satış durumunu günceller.
/// Kod, rol ve periyot kararlı anahtar oldukları için değiştirilemez.
/// </summary>
public sealed record UpdateSubscriptionPlanCommand(
    Guid PlanId,
    decimal Price,
    string? Currency,
    int TrialDays,
    bool IsActive) : ICommand<Result<SubscriptionPlanResponse>>;

/// <summary>M17-3: Seçilen planın ücretsiz denemesini başlatır (kullanıcı başına bir kez).</summary>
/// <param name="PlanCode">Denenecek planın kararlı kodu.</param>
public sealed record StartTrialCommand(string PlanCode) : ICommand<Result<MembershipStatusResponse>>;

/// <summary>M17-3: Aboneliği iptal eder; erişim dönem sonuna kadar sürer.</summary>
public sealed record CancelSubscriptionCommand : ICommand<Result<MembershipStatusResponse>>;

/// <summary>M17-3: Kullanıcının referans kodunu döner, yoksa üretir.</summary>
public sealed record EnsureReferralCodeCommand : ICommand<Result<ReferralCodeResponse>>;

/// <summary>M17-3: Referans kodunu kullanır; her iki tarafa bir ay premium yazar.</summary>
/// <param name="Code">Girilen kod.</param>
public sealed record RedeemReferralCommand(string Code) : ICommand<Result<MembershipStatusResponse>>;

/// <summary>
/// Abonelik kaydından <see cref="MembershipStatusResponse"/> üretmenin tek yeri — üç komut da
/// (deneme, iptal, referans) aynı yanıtı döndüğü için biçim tek noktada durur.
/// </summary>
public static class MembershipStatusFactory
{
    /// <summary>Yanıtı üretir.</summary>
    /// <param name="subscription">Abonelik kaydı.</param>
    /// <param name="planCode">Plan kodu (plansız kampanya aboneliğinde <c>null</c>).</param>
    /// <param name="nowUtc">Şimdiki an.</param>
    /// <returns>Üyelik durumu yanıtı.</returns>
    public static MembershipStatusResponse From(
        UserSubscription subscription, string? planCode, DateTime nowUtc)
    {
        var tier = subscription.EffectiveTier(nowUtc);
        var effectiveUntil = tier == MembershipTier.Premium ? subscription.CurrentPeriodEndUtc : (DateTime?)null;

        return new MembershipStatusResponse(
            tier.ToString(),
            effectiveUntil,
            planCode,
            subscription.Status.ToString(),
            EntitlementPolicy.For(tier, effectiveUntil));
    }

    /// <summary>Plan kaydından istemci görünümü üretir.</summary>
    /// <param name="plan">Plan.</param>
    /// <returns>Görünüm.</returns>
    public static SubscriptionPlanResponse From(SubscriptionPlan plan)
        => new(
            plan.Id, plan.Code, plan.Role, plan.Tier.ToString(), plan.Period.ToString(),
            plan.Price, plan.Currency, plan.TrialDays, plan.IsActive);
}

/// <summary>Satıştaki planları döner.</summary>
public sealed class ListPlansQueryHandler
    : IQueryHandler<ListPlansQuery, Result<IReadOnlyList<SubscriptionPlanResponse>>>
{
    private readonly IMembershipRepository _repository;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Üyelik deposu.</param>
    public ListPlansQueryHandler(IMembershipRepository repository) => _repository = repository;

    /// <summary>Sorguyu çalıştırır.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Plan listesi.</returns>
    public async Task<Result<IReadOnlyList<SubscriptionPlanResponse>>> Handle(
        ListPlansQuery query, CancellationToken cancellationToken)
    {
        var role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Trim();
        var plans = await _repository.ListActivePlansAsync(role, cancellationToken);

        return Result<IReadOnlyList<SubscriptionPlanResponse>>.Success(
            plans.Select(MembershipStatusFactory.From).ToArray());
    }
}

/// <summary>Admin — yeni plan tanımlar.</summary>
public sealed class CreateSubscriptionPlanCommandHandler
    : ICommandHandler<CreateSubscriptionPlanCommand, Result<SubscriptionPlanResponse>>
{
    private readonly IMembershipRepository _repository;
    private readonly IIdGenerator _idGenerator;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Üyelik deposu.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    public CreateSubscriptionPlanCommandHandler(IMembershipRepository repository, IIdGenerator idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Oluşan plan.</returns>
    public async Task<Result<SubscriptionPlanResponse>> Handle(
        CreateSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        var code = command.Code?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code)
            || !MembershipCampaigns.IsSellableRole(command.Role)
            || !Enum.TryParse<MembershipTier>(command.Tier, out var tier)
            || !Enum.TryParse<BillingPeriod>(command.Period, out var period)
            || command.Price <= 0
            || command.TrialDays < 0)
        {
            return Result<SubscriptionPlanResponse>.Failure(MembershipPlanErrors.InvalidPlan);
        }

        if (await _repository.GetPlanByCodeAsync(code, cancellationToken) is not null)
        {
            return Result<SubscriptionPlanResponse>.Failure(MembershipPlanErrors.PlanCodeConflict);
        }

        var plan = new SubscriptionPlan(
            _idGenerator.New(), code, command.Role, tier, period, command.Price,
            string.IsNullOrWhiteSpace(command.Currency) ? "TRY" : command.Currency.ToUpperInvariant(),
            command.TrialDays, command.IsActive);

        await _repository.AddPlanAsync(plan, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<SubscriptionPlanResponse>.Success(MembershipStatusFactory.From(plan));
    }
}

/// <summary>Admin — plan fiyat/deneme/satış durumunu günceller.</summary>
public sealed class UpdateSubscriptionPlanCommandHandler
    : ICommandHandler<UpdateSubscriptionPlanCommand, Result<SubscriptionPlanResponse>>
{
    private readonly IMembershipRepository _repository;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Üyelik deposu.</param>
    public UpdateSubscriptionPlanCommandHandler(IMembershipRepository repository) => _repository = repository;

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncellenmiş plan.</returns>
    public async Task<Result<SubscriptionPlanResponse>> Handle(
        UpdateSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        if (command.Price <= 0 || command.TrialDays < 0)
        {
            return Result<SubscriptionPlanResponse>.Failure(MembershipPlanErrors.InvalidPlan);
        }

        var plan = await _repository.GetPlanByIdAsync(command.PlanId, cancellationToken);
        if (plan is null)
        {
            return Result<SubscriptionPlanResponse>.Failure(MembershipPlanErrors.PlanNotFound);
        }

        plan.UpdatePricing(
            command.Price,
            string.IsNullOrWhiteSpace(command.Currency) ? plan.Currency : command.Currency.ToUpperInvariant(),
            command.TrialDays,
            command.IsActive);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<SubscriptionPlanResponse>.Success(MembershipStatusFactory.From(plan));
    }
}

/// <summary>M17-3: Ücretsiz denemeyi başlatır.</summary>
public sealed class StartTrialCommandHandler
    : ICommandHandler<StartTrialCommand, Result<MembershipStatusResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipRepository _repository;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Üyelik deposu.</param>
    /// <param name="clock">Saat.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    public StartTrialCommandHandler(
        ICurrentUser currentUser, IMembershipRepository repository, IClock clock, IIdGenerator idGenerator)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncel üyelik durumu.</returns>
    public async Task<Result<MembershipStatusResponse>> Handle(
        StartTrialCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<MembershipStatusResponse>.Failure(MembershipApplicationErrors.Forbidden);
        }

        var code = command.PlanCode?.Trim().ToLowerInvariant() ?? string.Empty;
        var plan = await _repository.GetPlanByCodeAsync(code, cancellationToken);
        if (plan is null)
        {
            return Result<MembershipStatusResponse>.Failure(MembershipPlanErrors.PlanNotFound);
        }

        if (!plan.IsActive || plan.TrialDays <= 0)
        {
            return Result<MembershipStatusResponse>.Failure(MembershipPlanErrors.TrialNotAvailable);
        }

        var now = _clock.UtcNow;
        var subscription = await _repository.GetSubscriptionByUserAsync(userId, cancellationToken);
        if (subscription is null)
        {
            subscription = new UserSubscription(_idGenerator.New(), userId, plan.Id, now);
            await _repository.AddSubscriptionAsync(subscription, cancellationToken);
        }
        else
        {
            // Kampanya ödülüyle açılmış plansız abonelik denemeye geçerken plana bağlanır.
            subscription.SwitchPlan(plan.Id);
        }

        var started = subscription.StartTrial(plan.TrialDays, now);
        if (!started.IsSuccess)
        {
            return Result<MembershipStatusResponse>.Failure(started.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<MembershipStatusResponse>.Success(
            MembershipStatusFactory.From(subscription, plan.Code, now));
    }
}

/// <summary>M17-3: Aboneliği iptal eder (erişim dönem sonuna kadar sürer).</summary>
public sealed class CancelSubscriptionCommandHandler
    : ICommandHandler<CancelSubscriptionCommand, Result<MembershipStatusResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Üyelik deposu.</param>
    /// <param name="clock">Saat.</param>
    public CancelSubscriptionCommandHandler(
        ICurrentUser currentUser, IMembershipRepository repository, IClock clock)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncel üyelik durumu.</returns>
    public async Task<Result<MembershipStatusResponse>> Handle(
        CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<MembershipStatusResponse>.Failure(MembershipApplicationErrors.Forbidden);
        }

        var subscription = await _repository.GetSubscriptionByUserAsync(userId, cancellationToken);
        if (subscription is null)
        {
            return Result<MembershipStatusResponse>.Failure(MembershipPlanErrors.SubscriptionNotFound);
        }

        var now = _clock.UtcNow;
        var cancelled = subscription.Cancel(now);
        if (!cancelled.IsSuccess)
        {
            return Result<MembershipStatusResponse>.Failure(cancelled.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<MembershipStatusResponse>.Success(
            MembershipStatusFactory.From(subscription, await PlanCodeAsync(subscription, cancellationToken), now));
    }

    private async Task<string?> PlanCodeAsync(UserSubscription subscription, CancellationToken cancellationToken)
        => subscription.PlanId is { } planId
            ? (await _repository.GetPlanByIdAsync(planId, cancellationToken))?.Code
            : null;
}

/// <summary>M17-3: Kullanıcının referans kodunu döner; yoksa üretip kaydeder.</summary>
public sealed class EnsureReferralCodeCommandHandler
    : ICommandHandler<EnsureReferralCodeCommand, Result<ReferralCodeResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipRepository _repository;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Üyelik deposu.</param>
    /// <param name="clock">Saat.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    public EnsureReferralCodeCommandHandler(
        ICurrentUser currentUser, IMembershipRepository repository, IClock clock, IIdGenerator idGenerator)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kullanıcının referans kodu.</returns>
    public async Task<Result<ReferralCodeResponse>> Handle(
        EnsureReferralCodeCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<ReferralCodeResponse>.Failure(MembershipApplicationErrors.Forbidden);
        }

        var existing = await _repository.GetReferralCodeByOwnerAsync(userId, cancellationToken);
        if (existing is null)
        {
            existing = new ReferralCode(
                _idGenerator.New(), userId, ReferralCodeFactory.For(userId), _clock.UtcNow);
            await _repository.AddReferralCodeAsync(existing, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return Result<ReferralCodeResponse>.Success(new ReferralCodeResponse(
            existing.Code, existing.RedemptionCount, MembershipCampaigns.ReferralRewardMonths));
    }
}

/// <summary>
/// M17-3: Referans kodunu kullanır. Ödül <b>her iki tarafa</b> yazılır ve mevcut premium'u uzatır.
/// </summary>
public sealed class RedeemReferralCommandHandler
    : ICommandHandler<RedeemReferralCommand, Result<MembershipStatusResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMembershipRepository _repository;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Üyelik deposu.</param>
    /// <param name="clock">Saat.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    public RedeemReferralCommandHandler(
        ICurrentUser currentUser, IMembershipRepository repository, IClock clock, IIdGenerator idGenerator)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kodu girenin güncel üyelik durumu.</returns>
    public async Task<Result<MembershipStatusResponse>> Handle(
        RedeemReferralCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var redeemerUserId))
        {
            return Result<MembershipStatusResponse>.Failure(MembershipApplicationErrors.Forbidden);
        }

        var code = ReferralCodeFactory.Normalize(command.Code ?? string.Empty);
        var referralCode = await _repository.GetReferralCodeByCodeAsync(code, cancellationToken);
        if (referralCode is null || !referralCode.IsActive)
        {
            return Result<MembershipStatusResponse>.Failure(MembershipPlanErrors.InvalidReferral);
        }

        if (referralCode.OwnerUserId == redeemerUserId)
        {
            return Result<MembershipStatusResponse>.Failure(MembershipPlanErrors.SelfReferral);
        }

        // Kullanıcı başına tek ödül: aynı kodun ikinci kullanımı da, başka bir kod da buradan döner.
        if (await _repository.HasRedeemedAsync(redeemerUserId, cancellationToken))
        {
            return Result<MembershipStatusResponse>.Failure(MembershipPlanErrors.ReferralAlreadyUsed);
        }

        var now = _clock.UtcNow;
        var months = MembershipCampaigns.ReferralRewardMonths;

        var redeemer = await GrantAsync(redeemerUserId, months, now, cancellationToken);
        if (!redeemer.IsSuccess)
        {
            return Result<MembershipStatusResponse>.Failure(redeemer.Error);
        }

        var referrer = await GrantAsync(referralCode.OwnerUserId, months, now, cancellationToken);
        if (!referrer.IsSuccess)
        {
            return Result<MembershipStatusResponse>.Failure(referrer.Error);
        }

        referralCode.RegisterRedemption();
        await _repository.AddRedemptionAsync(
            new ReferralRedemption(
                _idGenerator.New(), referralCode.Id, referralCode.OwnerUserId,
                redeemerUserId, months, now),
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<MembershipStatusResponse>.Success(
            MembershipStatusFactory.From(redeemer.Value!, planCode: null, now));
    }

    /// <summary>Ödülü tek bir tarafa yazar; aboneliği yoksa plansız bir kampanya aboneliği açar.</summary>
    private async Task<Result<UserSubscription>> GrantAsync(
        Guid userId, int months, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var subscription = await _repository.GetSubscriptionByUserAsync(userId, cancellationToken);
        if (subscription is null)
        {
            subscription = new UserSubscription(_idGenerator.New(), userId, planId: null, nowUtc);
            await _repository.AddSubscriptionAsync(subscription, cancellationToken);
        }

        var granted = subscription.GrantCampaignMonths(months, nowUtc);
        return granted.IsSuccess
            ? Result<UserSubscription>.Success(subscription)
            : Result<UserSubscription>.Failure(granted.Error);
    }
}

/// <summary>
/// Plan yazma uçları yalnız Admin'e açıktır: fiyat, kullanıcıya değil operasyona ait bir karardır.
/// </summary>
public sealed class AdminOnlyMembershipAuthorizer
    : ICommandAuthorizer<CreateSubscriptionPlanCommand>,
      ICommandAuthorizer<UpdateSubscriptionPlanCommand>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    public AdminOnlyMembershipAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Plan oluşturma yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(CreateSubscriptionPlanCommand command, CancellationToken cancellationToken)
        => Task.FromResult(AdminOnly());

    /// <summary>Plan güncelleme yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(UpdateSubscriptionPlanCommand command, CancellationToken cancellationToken)
        => Task.FromResult(AdminOnly());

    private Result AdminOnly()
        => _currentUser.IsAuthenticated && _currentUser.Roles.Contains("Admin")
            ? Result.Success()
            : Result.Failure(MembershipApplicationErrors.Forbidden);
}

/// <summary>
/// Deneme, iptal ve referans komutları yalnız <b>kendi</b> üyeliğini değiştirir (hedef kullanıcı
/// oturumdan gelir), bu yüzden kapı tek koşuldur: oturum açılmış olmalı.
/// </summary>
public sealed class AuthenticatedMembershipAuthorizer
    : ICommandAuthorizer<StartTrialCommand>,
      ICommandAuthorizer<CancelSubscriptionCommand>,
      ICommandAuthorizer<EnsureReferralCodeCommand>,
      ICommandAuthorizer<RedeemReferralCommand>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    public AuthenticatedMembershipAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Deneme başlatma yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(StartTrialCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>İptal yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(CancelSubscriptionCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Referans kodu yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(EnsureReferralCodeCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Referans kullanma yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(RedeemReferralCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    private Result Authenticated()
        => _currentUser.IsAuthenticated && Guid.TryParse(_currentUser.UserId, out _)
            ? Result.Success()
            : Result.Failure(MembershipApplicationErrors.Forbidden);
}
