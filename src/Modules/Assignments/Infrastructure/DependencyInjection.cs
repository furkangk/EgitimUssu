using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Shared.Infrastructure.Messaging;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAssignmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AssignmentsDbContext>(configuration, "Assignments", AssignmentsDbContext.SchemaName);
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<ICommandHandler<CreateLessonSessionFollowUpCommand, Result<LessonSessionFollowUpResponse>>, CreateLessonSessionFollowUpCommandHandler>();
        services.AddScoped<IQueryHandler<GetLessonSessionFollowUpQuery, Result<LessonSessionFollowUpResponse>>, GetLessonSessionFollowUpQueryHandler>();
        services.AddScoped<IQueryHandler<ListAssignmentsQuery, Result<IReadOnlyCollection<AssignmentResponse>>>, ListAssignmentsQueryHandler>();
        services.AddScoped<ICommandValidator<CreateLessonSessionFollowUpCommand>, CreateLessonSessionFollowUpCommandValidator>();
        services.AddScoped<IQueryValidator<GetLessonSessionFollowUpQuery>, GetLessonSessionFollowUpQueryValidator>();
        services.AddScoped<IQueryValidator<ListAssignmentsQuery>, ListAssignmentsQueryValidator>();
        services.AddScoped<ICommandAuthorizer<CreateLessonSessionFollowUpCommand>, AssignmentFollowUpAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetLessonSessionFollowUpQuery>, AssignmentFollowUpAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListAssignmentsQuery>, AssignmentFollowUpAuthorizer>();
        services.AddScoped<IIntegrationEventHandler, LessonSessionCompletedIntegrationEventHandler>();
        return services;
    }
}
