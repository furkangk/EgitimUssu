using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Parents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddParentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ParentsDbContext>(configuration, "Parents", ParentsDbContext.SchemaName);
        services.AddScoped<IParentRepository, ParentRepository>();

        // Command handler'ları
        services.AddScoped<ICommandHandler<CreateParentProfileCommand, Result<ParentProfileResponse>>, CreateParentProfileCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateNotificationPreferencesCommand, Result<ParentProfileResponse>>, UpdateNotificationPreferencesCommandHandler>();
        services.AddScoped<ICommandHandler<RequestChildLinkCommand, Result<ChildLinkResponse>>, RequestChildLinkCommandHandler>();
        services.AddScoped<ICommandHandler<ApproveChildLinkCommand, Result<ChildLinkResponse>>, ApproveChildLinkCommandHandler>();
        services.AddScoped<ICommandHandler<RejectChildLinkCommand, Result<ChildLinkResponse>>, RejectChildLinkCommandHandler>();
        services.AddScoped<ICommandHandler<RevokeChildLinkCommand, Result<ChildLinkResponse>>, RevokeChildLinkCommandHandler>();
        services.AddScoped<ICommandHandler<ClaimParentInviteCommand, Result<ChildLinkResponse>>, ClaimParentInviteCommandHandler>();

        // Query handler'ları
        services.AddScoped<IQueryHandler<GetParentProfileQuery, Result<ParentProfileResponse>>, GetParentProfileQueryHandler>();
        services.AddScoped<IQueryHandler<ListChildrenQuery, Result<IReadOnlyCollection<ChildLinkResponse>>>, ListChildrenQueryHandler>();
        services.AddScoped<IQueryHandler<GetChildDashboardQuery, Result<ChildDashboardResponse>>, GetChildDashboardQueryHandler>();

        // Validator'lar
        services.AddScoped<ICommandValidator<CreateParentProfileCommand>, CreateParentProfileCommandValidator>();
        services.AddScoped<ICommandValidator<RequestChildLinkCommand>, RequestChildLinkCommandValidator>();

        // Authorizer (tek sınıf tüm command/query'leri korur — ValidateAuthorizationCoverage şartı)
        services.AddScoped<ICommandAuthorizer<CreateParentProfileCommand>, ParentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdateNotificationPreferencesCommand>, ParentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RequestChildLinkCommand>, ParentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ApproveChildLinkCommand>, ParentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RejectChildLinkCommand>, ParentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RevokeChildLinkCommand>, ParentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ClaimParentInviteCommand>, ParentAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetParentProfileQuery>, ParentAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListChildrenQuery>, ParentAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetChildDashboardQuery>, ParentAuthorizer>();

        // Read-model projeksiyon handler'ları (diğer modüllerin integration event'lerinden beslenir)
        services.AddScoped<IIntegrationEventHandler, ParentLessonProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, ParentAssignmentProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, ParentPaymentProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, ParentStudentDirectoryProjectionHandler>();

        return services;
    }
}
