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
        services.AddScoped<INotificationDispatchProcessor, NotificationDispatchProcessor>();
        services.AddScoped<IQueryHandler<ListTeacherLessonRemindersQuery, Result<IReadOnlyCollection<LessonReminderResponse>>>, ListTeacherLessonRemindersQueryHandler>();
        services.AddScoped<IQueryValidator<ListTeacherLessonRemindersQuery>, ListTeacherLessonRemindersQueryValidator>();
        services.AddScoped<IQueryAuthorizer<ListTeacherLessonRemindersQuery>, LessonReminderQueryAuthorizer>();
        services.AddScoped<IIntegrationEventHandler, LessonScheduleNotificationIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler, StudyScheduleReminderIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler, ParentEventNotificationHandler>();
        services.AddHostedService<NotificationDispatcher>();
        return services;
    }
}
