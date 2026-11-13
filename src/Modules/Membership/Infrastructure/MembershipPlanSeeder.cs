using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Membership.Infrastructure;

/// <summary>Tohumlanacak plan tanımı (kimlik çalışma zamanında üretilir).</summary>
/// <param name="Code">Kararlı plan kodu.</param>
/// <param name="Role">Hitap ettiği rol.</param>
/// <param name="Tier">Üyelik seviyesi.</param>
/// <param name="Period">Faturalama periyodu.</param>
/// <param name="Price">Fiyat.</param>
/// <param name="Currency">Para birimi.</param>
/// <param name="TrialDays">Ücretsiz deneme günü.</param>
public sealed record MembershipPlanSeed(
    string Code,
    string Role,
    MembershipTier Tier,
    BillingPeriod Period,
    decimal Price,
    string Currency,
    int TrialDays);

/// <summary>
/// M17-1: Lansman plan kataloğu — 3 rol × (aylık | yıllık).
/// <para>
/// ⚠️ <b>Fiyatlar hipotezdir, karar değildir.</b> Kaynak: <c>codex/03_paket_fiyatlandirma.md</c> §4
/// lansman aralığının <b>alt sınırı</b> (aynı dokümanın 5. ilkesi: "ilk fiyatlar lansman döneminde
/// düşük tutulup doğrulama sonrası revize edilmelidir"). Nihai liste <b>Q2</b> ile onaylanana kadar
/// bu tohumlama yalnız <c>Development</c> ortamında koşar; üretimde planlar Admin ucundan girilir.
/// </para>
/// <para>
/// "İlk ay ücretsiz" kampanyası aylık planlara 30 günlük deneme olarak gömülüdür; yıllık planlarda
/// deneme yoktur (yıllık taahhüt zaten indirimli fiyatı taşıyor).
/// </para>
/// </summary>
public static class MembershipPlanSeedData
{
    /// <summary>Tohumlanacak planlar.</summary>
    public static IReadOnlyList<MembershipPlanSeed> Plans { get; } =
    [
        new("teacher-premium-monthly", "Teacher", MembershipTier.Premium, BillingPeriod.Monthly, 249m, "TRY", 30),
        new("teacher-premium-yearly", "Teacher", MembershipTier.Premium, BillingPeriod.Yearly, 2490m, "TRY", 0),
        new("student-premium-monthly", "Student", MembershipTier.Premium, BillingPeriod.Monthly, 99m, "TRY", 30),
        new("student-premium-yearly", "Student", MembershipTier.Premium, BillingPeriod.Yearly, 990m, "TRY", 0),
        new("parent-premium-monthly", "Parent", MembershipTier.Premium, BillingPeriod.Monthly, 99m, "TRY", 30),
        new("parent-premium-yearly", "Parent", MembershipTier.Premium, BillingPeriod.Yearly, 990m, "TRY", 0)
    ];
}

/// <summary>
/// M17-1: Plan kataloğunu idempotent olarak tohumlar — yalnız <b>eksik kodlar</b> eklenir, var olan
/// planın fiyatına dokunulmaz (Admin'in elle yaptığı değişikliği her açılışta geri almamak için).
/// </summary>
public static class MembershipPlanSeeder
{
    /// <summary>Eksik planları ekler.</summary>
    /// <param name="serviceProvider">Kök servis sağlayıcı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Eklenen plan sayısı.</returns>
    public static async Task<int> SeedMembershipPlansAsync(
        this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MembershipPlanSeeder");

        var existing = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Select(plan => plan.Code)
            .ToArrayAsync(cancellationToken);

        var missing = MembershipPlanSeedData.Plans
            .Where(seed => !existing.Contains(seed.Code))
            .ToArray();

        if (missing.Length == 0)
        {
            return 0;
        }

        foreach (var seed in missing)
        {
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan(
                idGenerator.New(), seed.Code, seed.Role, seed.Tier, seed.Period,
                seed.Price, seed.Currency, seed.TrialDays, isActive: true));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} membership plans (Development only).", missing.Length);
        return missing.Length;
    }
}
