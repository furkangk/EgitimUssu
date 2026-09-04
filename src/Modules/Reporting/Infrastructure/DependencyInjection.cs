using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // P07: Modül artık gerçek bir modele (öğretmen dashboard projeksiyonu) sahip; DbContext kaydı güvenli.
        services.AddModuleDbContext<ReportingDbContext>(configuration, "Reporting", ReportingDbContext.SchemaName);
        services.AddScoped<ITeacherDashboardProjectionReader, TeacherDashboardProjectionReader>();

        services.AddScoped<IQueryHandler<GetTeacherDashboardQuery, Result<TeacherDashboardResponse>>, GetTeacherDashboardQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetTeacherDashboardQuery>, ReportingAuthorizer>();

        // Read-model projeksiyon handler'ları (diğer modüllerin integration event'lerinden beslenir)
        services.AddScoped<IIntegrationEventHandler, LessonScheduleUpsertProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, LessonScheduleCancelledProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, AssignmentOpenedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, AssignmentClosedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, PaymentOverdueProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, PaymentSettledProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, TeacherStudentLinkProjectionHandler>();

        return services;
    }
}
