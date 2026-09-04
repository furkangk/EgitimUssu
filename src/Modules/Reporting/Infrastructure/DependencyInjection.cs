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

        // M14-1/2/3/5: analitik uçlar
        services.AddScoped<IReportingProjectionReader, ReportingProjectionReader>();
        services.AddScoped<StudentReportPrivacyGate>();

        services.AddScoped<IQueryHandler<GetTeacherMonthlyReportQuery, Result<TeacherMonthlyReportResponse>>, GetTeacherMonthlyReportQueryHandler>();
        services.AddScoped<IQueryHandler<GetTeacherFreeSlotsQuery, Result<IReadOnlyCollection<FreeSlotResponse>>>, GetTeacherFreeSlotsQueryHandler>();
        services.AddScoped<IQueryHandler<GetStudentStudyAnalysisQuery, Result<IReadOnlyCollection<StudentStudyWeekResponse>>>, GetStudentStudyAnalysisQueryHandler>();
        services.AddScoped<IQueryHandler<GetStudentPerformanceQuery, Result<IReadOnlyCollection<StudentPerformanceMonthResponse>>>, GetStudentPerformanceQueryHandler>();

        services.AddScoped<IQueryAuthorizer<GetTeacherMonthlyReportQuery>, TeacherScopedReportAuthorizer<GetTeacherMonthlyReportQuery>>();
        services.AddScoped<IQueryAuthorizer<GetTeacherFreeSlotsQuery>, TeacherScopedReportAuthorizer<GetTeacherFreeSlotsQuery>>();
        services.AddScoped<IQueryAuthorizer<GetStudentStudyAnalysisQuery>, StudentScopedReportAuthorizer<GetStudentStudyAnalysisQuery>>();
        services.AddScoped<IQueryAuthorizer<GetStudentPerformanceQuery>, StudentScopedReportAuthorizer<GetStudentPerformanceQuery>>();

        // Read-model projeksiyon handler'ları (diğer modüllerin integration event'lerinden beslenir)
        services.AddScoped<IIntegrationEventHandler, LessonScheduleUpsertProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, LessonScheduleCancelledProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, AssignmentOpenedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, AssignmentClosedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, PaymentOverdueProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, PaymentSettledProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, TeacherStudentLinkProjectionHandler>();

        // M14-1/2/3 analitik projeksiyon handler'ları
        services.AddScoped<IIntegrationEventHandler, TeacherMonthlyLessonCompletedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, TeacherMonthlyLessonCancelledProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, TeacherMonthlyPaymentExpectedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, TeacherMonthlyPaymentCollectedProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, StudentStudySessionProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, StudentStudyTestProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, StudentPerformanceTestProjectionHandler>();
        services.AddScoped<IIntegrationEventHandler, StudentPerformanceMasteryProjectionHandler>();

        return services;
    }
}
