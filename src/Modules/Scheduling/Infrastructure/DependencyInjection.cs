using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SchedulingDbContext>(configuration, "Scheduling", SchedulingDbContext.SchemaName);
        services.AddScoped<ILessonScheduleRepository, LessonScheduleRepository>();
        services.AddScoped<ILessonScheduleNotificationService, LessonScheduleNotificationService>();
        services.AddScoped<ICommandHandler<CreateLessonScheduleCommand, Result<LessonScheduleResponse>>, CreateLessonScheduleCommandHandler>();
        services.AddScoped<ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>, CancelLessonScheduleCommandHandler>();
        services.AddScoped<IQueryHandler<GetLessonScheduleByIdQuery, Result<LessonScheduleResponse>>, GetLessonScheduleByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListLessonSchedulesForTeacherQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>, ListLessonSchedulesForTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateLessonScheduleCommand>, CreateLessonScheduleCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CancelLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetLessonScheduleByIdQuery>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListLessonSchedulesForTeacherQuery>, LessonScheduleCommandAuthorizer>();
        return services;
    }
}
