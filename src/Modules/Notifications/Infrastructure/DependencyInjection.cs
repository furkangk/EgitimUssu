using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<NotificationsDbContext>(configuration, "Notifications", NotificationsDbContext.SchemaName);
        services.AddScoped<ILessonReminderRepository, LessonReminderRepository>();
        services.AddScoped<IParentNotificationRepository, ParentNotificationRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped<INotificationDispatchProcessor, NotificationDispatchProcessor>();
        services.AddScoped<ICommandHandler<RegisterDeviceTokenCommand, Result>, RegisterDeviceTokenCommandHandler>();
        services.AddScoped<ICommandHandler<UnregisterDeviceTokenCommand, Result>, UnregisterDeviceTokenCommandHandler>();
        services.AddScoped<ICommandAuthorizer<RegisterDeviceTokenCommand>, DeviceTokenCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UnregisterDeviceTokenCommand>, DeviceTokenCommandAuthorizer>();
        services.AddScoped<IQueryHandler<ListTeacherLessonRemindersQuery, Result<IReadOnlyCollection<LessonReminderResponse>>>, ListTeacherLessonRemindersQueryHandler>();
        services.AddScoped<IQueryValidator<ListTeacherLessonRemindersQuery>, ListTeacherLessonRemindersQueryValidator>();
        services.AddScoped<IQueryAuthorizer<ListTeacherLessonRemindersQuery>, LessonReminderQueryAuthorizer>();
        services.AddScoped<IQueryHandler<ListUserNotificationsQuery, Result<PagedResult<UserNotificationResponse>>>, ListUserNotificationsQueryHandler>();
        services.AddScoped<IQueryAuthorizer<ListUserNotificationsQuery>, UserNotificationAuthorizer>();
        services.AddScoped<IQueryHandler<GetUnreadNotificationCountQuery, Result<int>>, GetUnreadNotificationCountQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetUnreadNotificationCountQuery>, UserNotificationAuthorizer>();
        services.AddScoped<ICommandHandler<MarkNotificationReadCommand, Result>, MarkNotificationReadCommandHandler>();
        services.AddScoped<ICommandAuthorizer<MarkNotificationReadCommand>, UserNotificationAuthorizer>();
        services.AddScoped<IQueryHandler<ListParentNotificationsQuery, Result<IReadOnlyCollection<ParentNotificationResponse>>>, ListParentNotificationsQueryHandler>();
        services.AddScoped<IQueryAuthorizer<ListParentNotificationsQuery>, LessonReminderQueryAuthorizer>();
        // Ç-06 self dersler de pencere okuyucusundan öğrenci alıcısına planlanır.
        services.AddScoped<IIntegrationEventHandler, LessonScheduleNotificationIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler, ParentEventNotificationHandler>();
        // M07-1: vadesi geçen ödeme → öğretmene (ve Premium veliye) in-app bildirim.
        services.AddScoped<IIntegrationEventHandler, PaymentOverdueNotificationHandler>();
        services.AddScoped<IIntegrationEventHandler, PaymentDeclarationResolvedNotificationHandler>();
        // M06-3: son teslim uyarilari → ogrenciye (kacirilan odev ayrica Premium veliye).
        services.AddScoped<IIntegrationEventHandler, AssignmentDueSoonNotificationHandler>();
        services.AddScoped<IIntegrationEventHandler, AssignmentMissedNotificationHandler>();
        services.AddScoped<IIntegrationEventHandler, TopicGoalAchievedNotificationHandler>();

        // M16-1: yeni mesajda alıcıya in-app bildirim (push turunu dispatcher üstlenir).
        services.AddScoped<IIntegrationEventHandler, MessageSentNotificationHandler>();
        services.AddScoped<IParentWeeklySummaryProcessor, ParentWeeklySummaryProcessor>();
        services.AddScoped<LessonReminderPlanningProcessor>();
        services.AddHostedService<LessonReminderPlanner>();
        services.AddHostedService<NotificationDispatcher>();
        services.AddHostedService<ParentWeeklySummaryService>();
        // E-07: KVKK hesap silme — bu modüldeki kişisel veriyi anonimleştirir/siler.
        services.AddScoped<IPersonalDataAnonymizer, NotificationsPersonalDataAnonymizer>();

        return services;
    }
}
