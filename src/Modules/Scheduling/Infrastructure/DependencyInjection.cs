using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SchedulingDbContext>(configuration, "Scheduling", SchedulingDbContext.SchemaName);
        services.AddScoped<ILessonScheduleRepository, LessonScheduleRepository>();
        services.AddScoped<EgitimUssu.Shared.Contracts.IStudentUpcomingLessonsDirectory, StudentUpcomingLessonsDirectory>();
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

        // Ç-06: Öğrencinin kendi dersi (self LessonSchedule, TeacherUserId null) + birleşik takvim.
        services.AddScoped<ICommandHandler<CreateSelfLessonCommand, Result<SelfLessonResponse>>, CreateSelfLessonCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateSelfLessonCommand, Result<SelfLessonResponse>>, UpdateSelfLessonCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteSelfLessonCommand, Result<SelfLessonResponse>>, DeleteSelfLessonCommandHandler>();
        services.AddScoped<IQueryHandler<GetStudentCalendarQuery, Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>, GetStudentCalendarQueryHandler>();
        services.AddScoped<ICommandValidator<CreateSelfLessonCommand>, CreateSelfLessonCommandValidator>();
        services.AddScoped<ICommandValidator<UpdateSelfLessonCommand>, UpdateSelfLessonCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateSelfLessonCommand>, SelfLessonAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdateSelfLessonCommand>, SelfLessonAuthorizer>();
        services.AddScoped<ICommandAuthorizer<DeleteSelfLessonCommand>, SelfLessonAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentCalendarQuery>, SelfLessonAuthorizer>();

        // Ö-C: profil birleştirmede kaynak öğrenciye ait kayıtları kanonik öğrenciye taşır.
        services.AddScoped<IIntegrationEventHandler, SchedulingStudentMergedHandler>();

        // Ö-F: Öğrenci ders erteleme talebi (öğrenci talep eder, öğretmen kabul/red eder).
        services.AddScoped<ILessonChangeRequestRepository, LessonChangeRequestRepository>();
        services.AddScoped<ICommandHandler<CreateLessonChangeRequestCommand, Result<LessonChangeRequestResponse>>, CreateLessonChangeRequestCommandHandler>();
        services.AddScoped<ICommandHandler<AcceptLessonChangeRequestCommand, Result<LessonChangeRequestResponse>>, AcceptLessonChangeRequestCommandHandler>();
        services.AddScoped<ICommandHandler<RejectLessonChangeRequestCommand, Result<LessonChangeRequestResponse>>, RejectLessonChangeRequestCommandHandler>();
        services.AddScoped<IQueryHandler<ListLessonChangeRequestsForTeacherQuery, Result<IReadOnlyCollection<LessonChangeRequestResponse>>>, ListLessonChangeRequestsForTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateLessonChangeRequestCommand>, CreateLessonChangeRequestCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateLessonChangeRequestCommand>, LessonChangeRequestStudentAuthorizer>();
        services.AddScoped<ICommandAuthorizer<AcceptLessonChangeRequestCommand>, LessonChangeRequestTeacherAuthorizer>();
        services.AddScoped<ICommandAuthorizer<RejectLessonChangeRequestCommand>, LessonChangeRequestTeacherAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListLessonChangeRequestsForTeacherQuery>, LessonChangeRequestTeacherAuthorizer>();
        return services;
    }
}
