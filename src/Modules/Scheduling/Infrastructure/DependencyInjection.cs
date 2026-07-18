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
        services.AddScoped<ICommandHandler<CreateLessonScheduleCommand, Result<LessonScheduleResponse>>, CreateLessonScheduleCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateLessonScheduleCommand, Result<LessonScheduleResponse>>, UpdateLessonScheduleCommandHandler>();
        services.AddScoped<ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>, CancelLessonScheduleCommandHandler>();
        services.AddScoped<ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>, RescheduleLessonScheduleCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteLessonScheduleCommand, Result>, DeleteLessonScheduleCommandHandler>();
        services.AddScoped<ICommandHandler<CompleteLessonScheduleCommand, Result<LessonScheduleResponse>>, CompleteLessonScheduleCommandHandler>();
        services.AddScoped<IQueryHandler<GetLessonScheduleByIdQuery, Result<LessonScheduleResponse>>, GetLessonScheduleByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListLessonSchedulesForTeacherQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>, ListLessonSchedulesForTeacherQueryHandler>();
        services.AddScoped<IQueryHandler<ListLessonSchedulesForStudentQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>, ListLessonSchedulesForStudentQueryHandler>();
        services.AddScoped<ICommandValidator<CreateLessonScheduleCommand>, CreateLessonScheduleCommandValidator>();
        services.AddScoped<ICommandValidator<UpdateLessonScheduleCommand>, UpdateLessonScheduleCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdateLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CancelLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RescheduleLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CompleteLessonScheduleCommand>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetLessonScheduleByIdQuery>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListLessonSchedulesForTeacherQuery>, LessonScheduleCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListLessonSchedulesForStudentQuery>, StudentLessonQueryAuthorizer>();

        // B-01: Tatil / müsait değil blokları.
        services.AddScoped<ITimeOffBlockRepository, TimeOffBlockRepository>();
        services.AddScoped<ICommandHandler<CreateTimeOffBlockCommand, Result<CreateTimeOffResponse>>, CreateTimeOffBlockCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTimeOffBlockCommand, Result>, DeleteTimeOffBlockCommandHandler>();
        services.AddScoped<IQueryHandler<ListTimeOffBlocksForTeacherQuery, Result<IReadOnlyCollection<TimeOffBlockResponse>>>, ListTimeOffBlocksForTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateTimeOffBlockCommand>, CreateTimeOffBlockCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateTimeOffBlockCommand>, TimeOffBlockAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteTimeOffBlockCommand>, TimeOffBlockAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListTimeOffBlocksForTeacherQuery>, TimeOffBlockAuthorizer>();

        // Öğrenci-sahipli kişisel program (StudyScheduleEntry) + birleşik takvim.
        services.AddScoped<IStudyScheduleEntryRepository, StudyScheduleEntryRepository>();
        services.AddScoped<ICommandHandler<CreateStudyScheduleEntryCommand, Result<StudyScheduleEntryResponse>>, CreateStudyScheduleEntryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateStudyScheduleEntryCommand, Result<StudyScheduleEntryResponse>>, UpdateStudyScheduleEntryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteStudyScheduleEntryCommand, Result<StudyScheduleEntryResponse>>, DeleteStudyScheduleEntryCommandHandler>();
        services.AddScoped<IQueryHandler<GetStudentCalendarQuery, Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>, GetStudentCalendarQueryHandler>();
        services.AddScoped<ICommandValidator<CreateStudyScheduleEntryCommand>, CreateStudyScheduleEntryCommandValidator>();
        services.AddScoped<ICommandValidator<UpdateStudyScheduleEntryCommand>, UpdateStudyScheduleEntryCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateStudyScheduleEntryCommand>, StudyScheduleEntryAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdateStudyScheduleEntryCommand>, StudyScheduleEntryAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteStudyScheduleEntryCommand>, StudyScheduleEntryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentCalendarQuery>, StudyScheduleEntryAuthorizer>();
        return services;
    }
}
