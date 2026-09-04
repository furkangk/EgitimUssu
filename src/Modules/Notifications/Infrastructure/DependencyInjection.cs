using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        // Ç-06: Kendi ders hatırlatmaları da LessonScheduledDomainEvent (teacher null) üzerinden üretilir;
        // ayrı StudyScheduleEntry hatırlatma handler'ı kaldırıldı.
        services.AddScoped<IIntegrationEventHandler, LessonScheduleNotificationIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler, ParentEventNotificationHandler>();
        // M07-1: vadesi geçen ödeme → öğretmene (ve Premium veliye) in-app bildirim.
        services.AddScoped<IIntegrationEventHandler, PaymentOverdueNotificationHandler>();
        services.AddScoped<IParentWeeklySummaryProcessor, ParentWeeklySummaryProcessor>();
        services.AddHostedService<NotificationDispatcher>();
        services.AddHostedService<ParentWeeklySummaryService>();
        return services;
    }
}
