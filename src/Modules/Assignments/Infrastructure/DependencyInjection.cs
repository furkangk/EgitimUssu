using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
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
        services.AddScoped<EgitimUssu.Shared.Contracts.IStudentNotesDirectory, StudentNotesDirectory>();
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
        services.AddScoped<IIntegrationEventHandler, AssignmentsStudentMergedHandler>();

        // Öğrenci ödev aksiyonları (tamamlama + teslim + dosya indirme)
        services.AddScoped<IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>>, GetAssignmentQueryHandler>();
        services.AddScoped<ICommandHandler<MarkAssignmentCompletedCommand, Result<AssignmentResponse>>, MarkAssignmentCompletedCommandHandler>();
        services.AddScoped<ICommandHandler<SubmitAssignmentWorkCommand, Result<AssignmentResponse>>, SubmitAssignmentWorkCommandHandler>();
        services.AddScoped<IQueryHandler<GetAssignmentAttachmentQuery, Result<AssignmentAttachmentResponse>>, GetAssignmentAttachmentQueryHandler>();
        // M06-1: ders kaynagi paylasimi.
        services.AddScoped<ILessonResourceRepository, LessonResourceRepository>();
        services.AddScoped<ICommandHandler<CreateLessonResourceCommand, Result<LessonResourceResponse>>, CreateLessonResourceCommandHandler>();
        services.AddScoped<ICommandAuthorizer<CreateLessonResourceCommand>, LessonResourceAuthorizer>();
        services.AddScoped<ICommandHandler<ArchiveLessonResourceCommand, Result>, ArchiveLessonResourceCommandHandler>();
        services.AddScoped<ICommandAuthorizer<ArchiveLessonResourceCommand>, LessonResourceAuthorizer>();
        services.AddScoped<IQueryHandler<ListLessonResourcesQuery, Result<IReadOnlyCollection<LessonResourceResponse>>>, ListLessonResourcesQueryHandler>();
        services.AddScoped<IQueryAuthorizer<ListLessonResourcesQuery>, LessonResourceAuthorizer>();
        services.AddScoped<IQueryHandler<GetLessonResourceFileQuery, Result<LessonResourceFileResponse>>, GetLessonResourceFileQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetLessonResourceFileQuery>, LessonResourceAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetAssignmentAttachmentQuery>, AssignmentStudentActionAuthorizer>();
        services.AddScoped<ICommandAuthorizer<MarkAssignmentCompletedCommand>, AssignmentStudentActionAuthorizer>();
        services.AddScoped<ICommandAuthorizer<SubmitAssignmentWorkCommand>, AssignmentStudentActionAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetAssignmentQuery>, AssignmentStudentActionAuthorizer>();

        // Öğretmen ödev aksiyonları (onay + geri gönder)
        services.AddScoped<ICommandHandler<ApproveAssignmentCommand, Result<AssignmentResponse>>, ApproveAssignmentCommandHandler>();
        services.AddScoped<ICommandHandler<ReturnAssignmentCommand, Result<AssignmentResponse>>, ReturnAssignmentCommandHandler>();
        services.AddScoped<ICommandAuthorizer<ApproveAssignmentCommand>, AssignmentTeacherAuthorizer>();
        services.AddScoped<ICommandAuthorizer<ReturnAssignmentCommand>, AssignmentTeacherAuthorizer>();
        // M06-4: odev puanlama.
        services.AddScoped<ICommandHandler<GradeAssignmentCommand, Result<AssignmentResponse>>, GradeAssignmentCommandHandler>();
        services.AddScoped<ICommandAuthorizer<GradeAssignmentCommand>, AssignmentTeacherAuthorizer>();

        // M14-4: rapor ozeti icin odev tamamlama sayaci (Shared/Contracts okuma sozlesmesi).
        services.AddScoped<IStudentAssignmentDigestDirectory, StudentAssignmentDigestDirectory>();

        // M06-3: son teslim uyarisi taramasi.
        services.AddScoped<IAssignmentDueScanProcessor, AssignmentDueScanProcessor>();
        services.AddHostedService<AssignmentDueScanner>();
        // E-07: KVKK hesap silme — bu modüldeki kişisel veriyi anonimleştirir/siler.
        services.AddScoped<IPersonalDataAnonymizer, AssignmentsPersonalDataAnonymizer>();

        return services;
    }
}
