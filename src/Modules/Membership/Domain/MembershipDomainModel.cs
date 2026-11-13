using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Membership.Domain;

// ⚠️ `MembershipTier` bilinçli olarak burada **yeniden tanımlanmaz**: kanonik tanım
// `Shared.Contracts`'tadır (Students/Parents/Study/Reporting hepsi onu kullanıyor).
// P09'un amacı üyeliği tek kaynağa bağlamak; ikinci bir enum tam tersini yapardı.

/// <summary>Aboneliğin yaşam döngüsü durumu.</summary>
public enum SubscriptionStatus
{
    /// <summary>Ücretsiz deneme sürüyor.</summary>
    Trialing = 1,

    /// <summary>Ödeme alındı, dönem açık.</summary>
    Active = 2,

    /// <summary>Yenileme tahsilatı başarısız; dönem sonuna kadar erişim sürer.</summary>
    PastDue = 3,

    /// <summary>Kullanıcı iptal etti; <b>dönem sonuna kadar erişim sürer</b>.</summary>
    Cancelled = 4,

    /// <summary>Dönem bitti, erişim kapandı.</summary>
    Expired = 5
}

/// <summary>Faturalama periyodu.</summary>
public enum BillingPeriod
{
    Monthly = 1,
    Yearly = 2
}

/// <summary>Membership modülünün hata kodları (`&lt;modul&gt;.&lt;hata&gt;`).</summary>
public static class MembershipErrors
{
    public static readonly Error InvalidTrialDays =
        new("membership.invalid_trial_days", "Deneme süresi gün sayısı sıfırdan büyük olmalıdır.");

    public static readonly Error TrialAlreadyUsed =
        new("membership.trial_already_used", "Bu abonelik için ücretsiz deneme hakkı zaten kullanıldı.");

    public static readonly Error InvalidPeriodEnd =
        new("membership.invalid_period_end", "Dönem bitişi şu andan ileri bir tarih olmalıdır.");

    public static readonly Error InvalidCampaignMonths =
        new("membership.invalid_campaign_months", "Kampanya ödülü en az bir ay olmalıdır.");
}

/// <summary>
/// M17-1: Satılabilir bir üyelik planı — rol × tier × periyot × fiyat.
/// <para><see cref="Code"/> sağlayıcıya ve istemciye giden kararlı anahtardır (ör. <c>teacher-premium-monthly</c>).</para>
/// </summary>
public sealed class SubscriptionPlan : AggregateRoot<Guid>
{
    private SubscriptionPlan()
    {
    }

    public SubscriptionPlan(
        Guid id,
        string code,
        string role,
        MembershipTier tier,
        BillingPeriod period,
        decimal price,
        string currency,
        int trialDays,
        bool isActive)
    {
        Id = id;
        Code = code;
        Role = role;
        Tier = tier;
        Period = period;
        Price = price;
        Currency = currency;
        TrialDays = trialDays;
        IsActive = isActive;
    }

    /// <summary>Kararlı plan anahtarı (ör. <c>teacher-premium-monthly</c>).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Planın hitap ettiği rol: <c>Teacher</c> | <c>Student</c> | <c>Parent</c>.</summary>
    public string Role { get; private set; } = string.Empty;

    public MembershipTier Tier { get; private set; }

    public BillingPeriod Period { get; private set; }

    public decimal Price { get; private set; }

    /// <summary>ISO para birimi; varsayılan <c>TRY</c> (master tasarım §4).</summary>
    public string Currency { get; private set; } = "TRY";

    /// <summary>Plan ücretsiz deneme veriyorsa gün sayısı; 0 ise deneme yok.</summary>
    public int TrialDays { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Verilen andan itibaren bir faturalama döneminin bitişi. Takvim ayı/yılı kullanılır
    /// (30/365 gün <b>değil</b>): 31 Ocak'ta başlayan aylık abonelik 28 Şubat'ta yenilenmelidir.
    /// </summary>
    public DateTime NextPeriodEnd(DateTime fromUtc) => Period switch
    {
        BillingPeriod.Yearly => fromUtc.AddYears(1),
        _ => fromUtc.AddMonths(1)
    };

    /// <summary>
    /// M17-1: Admin plan güncellemesi. <see cref="Code"/>, <see cref="Role"/> ve <see cref="Period"/>
    /// bilinçli olarak <b>değiştirilemez</b>: bunlar ödeme sağlayıcısına ve istemciye giden kararlı
    /// anahtarlardır, sonradan değişmeleri geçmiş abonelikleri anlamsızlaştırırdı.
    /// </summary>
    public void UpdatePricing(decimal price, string currency, int trialDays, bool isActive)
    {
        Price = price;
        Currency = currency;
        TrialDays = trialDays;
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

/// <summary>
/// M17-1: Bir kullanıcının abonelik kaydı.
/// <para>
/// <b>Erişim ile durum aynı şey değildir.</b> <see cref="SubscriptionStatus.Cancelled"/> ve
/// <see cref="SubscriptionStatus.PastDue"/> durumlarında da kullanıcı <see cref="CurrentPeriodEndUtc"/>
/// gelene kadar Premium'dur — ödediği süreyi kaybetmez. Tier'ı Free'ye düşüren tek geçiş
/// <see cref="ExpireIfDue"/>'dur ve olayı orada yayılır.
/// </para>
/// </summary>
public sealed class UserSubscription : AggregateRoot<Guid>
{
    private UserSubscription()
    {
    }

    public UserSubscription(Guid id, Guid userId, Guid? planId, DateTime createdOnUtc)
    {
        Id = id;
        UserId = userId;
        PlanId = planId;
        Status = SubscriptionStatus.Expired;   // henüz hiçbir dönem açılmadı
        StartedOnUtc = createdOnUtc;
        CurrentPeriodEndUtc = createdOnUtc;
        AutoRenew = true;
    }

    public Guid UserId { get; private set; }

    /// <summary>
    /// Abone olunan plan. <b>Kampanya (referans) ödülüyle</b> açılan aboneliklerde plan yoktur:
    /// ödül satın alınmış bir plana bağlı değildir, bu yüzden alan <c>null</c> olabilir.
    /// </summary>
    public Guid? PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartedOnUtc { get; private set; }

    /// <summary>Erişimin bittiği an (hariç). Bu ana kadar kullanıcı Premium'dur.</summary>
    public DateTime CurrentPeriodEndUtc { get; private set; }

    public bool AutoRenew { get; private set; }

    /// <summary>Ödeme sağlayıcısındaki abonelik kimliği (webhook eşlemesi için, P09 Task 4).</summary>
    public string? ProviderSubscriptionId { get; private set; }

    public DateTime? CancelledOnUtc { get; private set; }

    /// <summary>
    /// Deneme hakkı bu abonelikte kullanıldı mı. Kullanıcı başına <b>tek</b> abonelik kaydı tutulduğu
    /// için (benzersiz <c>UserId</c> indeksi) bu bayrak "kullanıcı başına bir deneme" kuralını taşır;
    /// kullanıcı-düzeyi kontrol Application katmanında (Task 3) bu bayrağı okur.
    /// </summary>
    public bool HasUsedTrial { get; private set; }

    /// <summary>Verilen anda kullanıcının premium erişimi var mı.</summary>
    public bool HasAccess(DateTime nowUtc)
        => Status != SubscriptionStatus.Expired && nowUtc < CurrentPeriodEndUtc;

    /// <summary>Verilen andaki etkin üyelik seviyesi.</summary>
    public MembershipTier EffectiveTier(DateTime nowUtc)
        => HasAccess(nowUtc) ? MembershipTier.Premium : MembershipTier.Free;

    /// <summary>M17-3: Ücretsiz denemeyi başlatır. Bir abonelikte yalnız bir kez çalışır.</summary>
    public Result StartTrial(int days, DateTime nowUtc)
    {
        if (days <= 0)
        {
            return Result.Failure(MembershipErrors.InvalidTrialDays);
        }

        if (HasUsedTrial)
        {
            return Result.Failure(MembershipErrors.TrialAlreadyUsed);
        }

        HasUsedTrial = true;
        Status = SubscriptionStatus.Trialing;
        StartedOnUtc = nowUtc;
        CurrentPeriodEndUtc = nowUtc.AddDays(days);
        RaiseChanged(MembershipTier.Premium, nowUtc);
        return Result.Success();
    }

    /// <summary>
    /// Ödeme alındı: dönemi <paramref name="periodEndUtc"/>'ye taşır ve aboneliği aktifleştirir.
    /// Süresi dolmuş bir abonelik de bu yolla yeniden canlanır.
    /// </summary>
    public Result Activate(DateTime periodEndUtc, DateTime nowUtc)
    {
        if (periodEndUtc <= nowUtc)
        {
            return Result.Failure(MembershipErrors.InvalidPeriodEnd);
        }

        Status = SubscriptionStatus.Active;
        CurrentPeriodEndUtc = periodEndUtc;
        CancelledOnUtc = null;
        RaiseChanged(MembershipTier.Premium, nowUtc);
        return Result.Success();
    }

    /// <summary>Yenileme tahsilatı başarılı: dönemi ileri taşır.</summary>
    public Result Renew(DateTime newPeriodEndUtc, DateTime nowUtc) => Activate(newPeriodEndUtc, nowUtc);

    /// <summary>
    /// Yenileme tahsilatı başarısız. Erişim <b>kesilmez</b>; ödenen dönem sonuna kadar sürer,
    /// sonrasını <see cref="ExpireIfDue"/> kapatır.
    /// </summary>
    public Result MarkPastDue(DateTime nowUtc)
    {
        Status = SubscriptionStatus.PastDue;
        return Result.Success();
    }

    /// <summary>
    /// Kullanıcı iptal etti: otomatik yenileme kapanır, erişim dönem sonuna kadar sürer.
    /// Tier değişmediği için <b>olay yayılmaz</b> — Free'ye düşüş <see cref="ExpireIfDue"/>'dadır.
    /// </summary>
    public Result Cancel(DateTime nowUtc)
    {
        Status = SubscriptionStatus.Cancelled;
        CancelledOnUtc = nowUtc;
        AutoRenew = false;
        return Result.Success();
    }

    /// <summary>
    /// Dönem bittiyse aboneliği kapatır ve Free'ye düşüş olayını yayar.
    /// Idempotent: zaten <see cref="SubscriptionStatus.Expired"/> ise hiçbir şey yapmaz.
    /// </summary>
    /// <returns>Durum bu çağrıda değiştiyse <c>true</c>.</returns>
    public bool ExpireIfDue(DateTime nowUtc)
    {
        if (Status == SubscriptionStatus.Expired || nowUtc < CurrentPeriodEndUtc)
        {
            return false;
        }

        Status = SubscriptionStatus.Expired;
        RaiseChanged(MembershipTier.Free, nowUtc);
        return true;
    }

    /// <summary>
    /// M17-3: Kampanya ödülü (referans, beta vb.) olarak <paramref name="months"/> ay premium ekler.
    /// <para>
    /// Ödül mevcut dönemi <b>ezmez, uzatır</b>: erişimi süren kullanıcıda başlangıç noktası
    /// <see cref="CurrentPeriodEndUtc"/>, süresi dolmuş kullanıcıda <paramref name="nowUtc"/>'dir.
    /// Aksi hâlde ödül, ödenmiş bir dönemi kısaltabilirdi.
    /// </para>
    /// </summary>
    public Result GrantCampaignMonths(int months, DateTime nowUtc)
    {
        if (months <= 0)
        {
            return Result.Failure(MembershipErrors.InvalidCampaignMonths);
        }

        var baseline = HasAccess(nowUtc) ? CurrentPeriodEndUtc : nowUtc;
        return Activate(baseline.AddMonths(months), nowUtc);
    }

    /// <summary>Aboneliği bir plana bağlar (plansız kampanya aboneliği sonradan plan seçtiğinde).</summary>
    public void SwitchPlan(Guid planId) => PlanId = planId;

    /// <summary>Sağlayıcı abonelik kimliğini bağlar (webhook eşlemesi, P09 Task 4).</summary>
    public void LinkProviderSubscription(string providerSubscriptionId)
        => ProviderSubscriptionId = providerSubscriptionId;

    private void RaiseChanged(MembershipTier tier, DateTime nowUtc)
        => Raise(new MembershipChangedDomainEvent(UserId, tier, CurrentPeriodEndUtc, nowUtc));
}

/// <summary>
/// K-09: Üyelik seviyesi değişti. Diğer modüller entitlement'ı bu olayla tazeler
/// (`MembershipChangedIntegrationEvent` — P09 Task 2).
/// </summary>
/// <param name="UserId">Üyeliği değişen kullanıcı.</param>
/// <param name="Tier">Yeni etkin seviye.</param>
/// <param name="EffectiveUntilUtc">Bu seviyenin geçerli olduğu son an.</param>
/// <param name="ChangedOnUtc">Değişimin gerçekleştiği an (<c>IClock.UtcNow</c>).</param>
public sealed record MembershipChangedDomainEvent(
    Guid UserId,
    MembershipTier Tier,
    DateTime EffectiveUntilUtc,
    DateTime ChangedOnUtc) : DomainEvent;

/// <summary>
/// M17-3: "Arkadaşını getir" kampanyasının kullanıcı başına tek referans kodu.
/// <para>
/// Kod <b>kullanıcı kimliğinden türetilir</b> (bkz. <see cref="ReferralCodeFactory"/>): rastgele üretim
/// çakışma hâlinde yeniden deneme döngüsü gerektirirdi ve testte sabitlenemezdi.
/// </para>
/// </summary>
public sealed class ReferralCode : AggregateRoot<Guid>
{
    private ReferralCode()
    {
    }

    public ReferralCode(Guid id, Guid ownerUserId, string code, DateTime createdOnUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Code = code;
        CreatedOnUtc = createdOnUtc;
        IsActive = true;
    }

    /// <summary>Kodun sahibi (davet eden kullanıcı).</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Elle girilebilir, büyük harfli kod (ör. <c>K7QMR2XA</c>).</summary>
    public string Code { get; private set; } = string.Empty;

    public DateTime CreatedOnUtc { get; private set; }

    /// <summary>Kampanya kapatıldığında kod kullanılamaz olur; geçmiş ödüller geri alınmaz.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Bu kodun kaç kez kullanıldığı (kampanya raporlaması için).</summary>
    public int RedemptionCount { get; private set; }

    public void RegisterRedemption() => RedemptionCount++;

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// M17-3: Bir referans kodunun tek kullanımı. <see cref="RedeemerUserId"/> benzersizdir:
/// bir kullanıcı ömrü boyunca <b>yalnız bir</b> referans ödülü alabilir.
/// </summary>
public sealed class ReferralRedemption : AggregateRoot<Guid>
{
    private ReferralRedemption()
    {
    }

    public ReferralRedemption(
        Guid id,
        Guid referralCodeId,
        Guid referrerUserId,
        Guid redeemerUserId,
        int rewardMonths,
        DateTime redeemedOnUtc)
    {
        Id = id;
        ReferralCodeId = referralCodeId;
        ReferrerUserId = referrerUserId;
        RedeemerUserId = redeemerUserId;
        RewardMonths = rewardMonths;
        RedeemedOnUtc = redeemedOnUtc;
    }

    public Guid ReferralCodeId { get; private set; }

    /// <summary>Davet eden (kodun sahibi).</summary>
    public Guid ReferrerUserId { get; private set; }

    /// <summary>Kodu giren (davet edilen) kullanıcı.</summary>
    public Guid RedeemerUserId { get; private set; }

    /// <summary>Her iki tarafa verilen ay sayısı (kampanya kuralı değişirse geçmiş kayıt bozulmasın).</summary>
    public int RewardMonths { get; private set; }

    public DateTime RedeemedOnUtc { get; private set; }
}

/// <summary>
/// M17-3: Referans kodunun kullanıcı kimliğinden türetilmesi — saf ve deterministik.
/// <para>
/// Alfabede karıştırılabilir karakterler (<c>0/O</c>, <c>1/I/L</c>) yoktur: kod kullanıcıdan
/// <b>elle</b> girilir ve bu karakterler yanlış girişin en sık nedenidir.
/// </para>
/// </summary>
public static class ReferralCodeFactory
{
    /// <summary>Kodun karakter sayısı.</summary>
    public const int Length = 8;

    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Verilen kullanıcı için kararlı referans kodunu üretir.</summary>
    /// <param name="userId">Kod sahibinin kimliği.</param>
    /// <returns><see cref="Length"/> karakterlik büyük harfli kod.</returns>
    public static string For(Guid userId)
    {
        var bytes = userId.ToByteArray();
        var value = BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8);

        var characters = new char[Length];
        for (var index = Length - 1; index >= 0; index--)
        {
            characters[index] = Alphabet[(int)(value % (ulong)Alphabet.Length)];
            value /= (ulong)Alphabet.Length;
        }

        return new string(characters);
    }

    /// <summary>Kullanıcının girdiği kodu karşılaştırılabilir biçime getirir (boşluk/küçük harf toleransı).</summary>
    /// <param name="code">Ham girdi.</param>
    /// <returns>Normalleştirilmiş kod.</returns>
    public static string Normalize(string code)
        => code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
