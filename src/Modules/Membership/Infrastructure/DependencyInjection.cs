using EgitimUssu.Modules.Membership.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Membership.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMembershipModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<MembershipDbContext>(configuration, "Membership", MembershipDbContext.SchemaName);

        // Tek örnek iki sözleşmeyi karşılar: modüller-arası IEntitlementDirectory ve modül-içi IMembershipReader.
        services.AddScoped<EntitlementDirectory>();
        services.AddScoped<IEntitlementDirectory>(provider => provider.GetRequiredService<EntitlementDirectory>());
        services.AddScoped<IMembershipReader>(provider => provider.GetRequiredService<EntitlementDirectory>());

        services.AddScoped<IQueryHandler<GetMyMembershipQuery, Result<MembershipStatusResponse>>, GetMyMembershipQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetMyMembershipQuery>, GetMyMembershipQueryAuthorizer>();

        // M17-1/M17-3: plan kataloğu, deneme, iptal ve referans kampanyası (P09 Task 3).
        services.AddScoped<IMembershipRepository, MembershipRepository>();

        services.AddScoped<
            IQueryHandler<ListPlansQuery, Result<IReadOnlyList<SubscriptionPlanResponse>>>,
            ListPlansQueryHandler>();

        services.AddScoped<
            ICommandHandler<CreateSubscriptionPlanCommand, Result<SubscriptionPlanResponse>>,
            CreateSubscriptionPlanCommandHandler>();
        services.AddScoped<
            ICommandHandler<UpdateSubscriptionPlanCommand, Result<SubscriptionPlanResponse>>,
            UpdateSubscriptionPlanCommandHandler>();
        services.AddScoped<
            ICommandHandler<StartTrialCommand, Result<MembershipStatusResponse>>,
            StartTrialCommandHandler>();
        services.AddScoped<
            ICommandHandler<CancelSubscriptionCommand, Result<MembershipStatusResponse>>,
            CancelSubscriptionCommandHandler>();
        services.AddScoped<
            ICommandHandler<EnsureReferralCodeCommand, Result<ReferralCodeResponse>>,
            EnsureReferralCodeCommandHandler>();
        services.AddScoped<
            ICommandHandler<RedeemReferralCommand, Result<MembershipStatusResponse>>,
            RedeemReferralCommandHandler>();

        // Tek örnek birden çok komutun kapısını taşır (Admin uçları / oturum uçları).
        services.AddScoped<AdminOnlyMembershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CreateSubscriptionPlanCommand>>(
            provider => provider.GetRequiredService<AdminOnlyMembershipAuthorizer>());
        services.AddScoped<ICommandAuthorizer<UpdateSubscriptionPlanCommand>>(
            provider => provider.GetRequiredService<AdminOnlyMembershipAuthorizer>());

        services.AddScoped<AuthenticatedMembershipAuthorizer>();
        services.AddScoped<ICommandAuthorizer<StartTrialCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMembershipAuthorizer>());
        services.AddScoped<ICommandAuthorizer<CancelSubscriptionCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMembershipAuthorizer>());
        services.AddScoped<ICommandAuthorizer<EnsureReferralCodeCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMembershipAuthorizer>());
        services.AddScoped<ICommandAuthorizer<RedeemReferralCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMembershipAuthorizer>());

        return services;
    }
}
