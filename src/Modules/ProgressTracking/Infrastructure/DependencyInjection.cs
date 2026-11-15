using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProgressTrackingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProgressTrackingDbContext>(configuration, "ProgressTracking", ProgressTrackingDbContext.SchemaName);
        services.AddScoped<IProgressRepository, ProgressRepository>();
        services.AddScoped<ProgressOwnershipGuard>();
        services.AddScoped<MasteryService>();
        services.AddScoped<ProgressOverviewReader>();

        // Öğrenci-kapsamlı istekleri koruyan kapalı-generik sahiplik yetkilendiricileri
        AddStudentScopedQueryAuthorizer<ListTopicMasteryQuery>(services);
        AddStudentScopedQueryAuthorizer<ListWeakSpotsQuery>(services);
        AddStudentScopedQueryAuthorizer<ListStrengthsQuery>(services);
        AddStudentScopedQueryAuthorizer<GetProgressOverviewQuery>(services);
        AddStudentScopedQueryAuthorizer<ListTopicGoalsQuery>(services);
        AddStudentScopedQueryAuthorizer<ListProgressSnapshotsQuery>(services);
        AddStudentScopedCommandAuthorizer<CreateTopicGoalCommand>(services);
        services.AddScoped<ICommandAuthorizer<CancelTopicGoalCommand>, CancelTopicGoalAuthorizer>();

        // M10-2: öğretmen/veli görünümü — bağ doğrulaması sahiplikten ayrıdır (bkz. ProgressTrackingPolicies)
        services.AddScoped<IQueryAuthorizer<GetTeacherStudentProgressOverviewQuery>, TeacherStudentProgressAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetParentChildProgressOverviewQuery>, ParentChildProgressAuthorizer>();

        // Sorgu / komut handler'ları
        services.AddScoped<IQueryHandler<ListTopicMasteryQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>, ListTopicMasteryQueryHandler>();
        services.AddScoped<IQueryHandler<ListWeakSpotsQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>, ListWeakSpotsQueryHandler>();
        services.AddScoped<IQueryHandler<ListStrengthsQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>, ListStrengthsQueryHandler>();
        services.AddScoped<IQueryHandler<GetProgressOverviewQuery, Result<ProgressOverviewResponse>>, GetProgressOverviewQueryHandler>();
        services.AddScoped<IQueryHandler<ListTopicGoalsQuery, Result<IReadOnlyCollection<TopicGoalResponse>>>, ListTopicGoalsQueryHandler>();
        services.AddScoped<IQueryHandler<ListProgressSnapshotsQuery, Result<IReadOnlyCollection<ProgressSnapshotResponse>>>, ListProgressSnapshotsQueryHandler>();
        services.AddScoped<IQueryHandler<GetTeacherStudentProgressOverviewQuery, Result<ProgressOverviewResponse>>, GetTeacherStudentProgressOverviewQueryHandler>();
        services.AddScoped<IQueryHandler<GetParentChildProgressOverviewQuery, Result<ProgressOverviewResponse>>, GetParentChildProgressOverviewQueryHandler>();
        services.AddScoped<ICommandHandler<CreateTopicGoalCommand, Result<TopicGoalResponse>>, CreateTopicGoalCommandHandler>();
        services.AddScoped<ICommandHandler<CancelTopicGoalCommand, Result<bool>>, CancelTopicGoalCommandHandler>();

        // M08 çalışma/test olaylarını tüketen (idempotent) gelişim besleme handler'ları
        services.AddScoped<IIntegrationEventHandler, StudyStatisticsProgressHandler>();

        // M10-4: tamamlanan ders → konuya katılım katkısı
        services.AddScoped<IIntegrationEventHandler, LessonSessionCompletedProgressHandler>();

        // P15 / N-28: profil birleşmesi → gelişim verisi kanonik profile (çakışmada kanonik kazanır)
        services.AddScoped<IIntegrationEventHandler, StudentProfilesMergedProgressHandler>();

        // M10-1: kapanmış hafta/ay için gelişim anlık görüntüsü üreten günlük tur
        services.AddScoped<IProgressSnapshotProcessor, ProgressSnapshotProcessor>();
        services.AddHostedService<ProgressSnapshotService>();

        return services;
    }

    private static void AddStudentScopedQueryAuthorizer<TQuery>(IServiceCollection services)
        where TQuery : IStudentScopedProgressRequest =>
        services.AddScoped<IQueryAuthorizer<TQuery>, ProgressOwnershipQueryAuthorizer<TQuery>>();

    private static void AddStudentScopedCommandAuthorizer<TCommand>(IServiceCollection services)
        where TCommand : IStudentScopedProgressRequest =>
        services.AddScoped<ICommandAuthorizer<TCommand>, ProgressOwnershipCommandAuthorizer<TCommand>>();
}
