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
        services.AddSingleton<IAssignmentFileStorage, LocalAssignmentFileStorage>();
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

        // Öğrenci ödev aksiyonları (tamamlama + teslim + dosya indirme)
        services.AddScoped<IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>>, GetAssignmentQueryHandler>();
        services.AddScoped<ICommandHandler<MarkAssignmentCompletedCommand, Result<AssignmentResponse>>, MarkAssignmentCompletedCommandHandler>();
        services.AddScoped<ICommandHandler<SubmitAssignmentWorkCommand, Result<AssignmentResponse>>, SubmitAssignmentWorkCommandHandler>();
        services.AddScoped<ICommandAuthorizer<MarkAssignmentCompletedCommand>, AssignmentStudentActionAuthorizer>();
        services.AddScoped<ICommandAuthorizer<SubmitAssignmentWorkCommand>, AssignmentStudentActionAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetAssignmentQuery>, AssignmentStudentActionAuthorizer>();

        // Öğretmen ödev aksiyonları (onay + geri gönder)
        services.AddScoped<ICommandHandler<ApproveAssignmentCommand, Result<AssignmentResponse>>, ApproveAssignmentCommandHandler>();
        services.AddScoped<ICommandHandler<ReturnAssignmentCommand, Result<AssignmentResponse>>, ReturnAssignmentCommandHandler>();
        services.AddScoped<ICommandAuthorizer<ApproveAssignmentCommand>, AssignmentTeacherAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ReturnAssignmentCommand>, AssignmentTeacherAuthorizer>();
        return services;
    }
}
