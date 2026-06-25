using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Scheduling.API;

public sealed class SchedulingModule : ModuleDefinition
{
    public override string Name => "Scheduling";

    public override string RoutePrefix => "/api/scheduling";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSchedulingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/lessons", CreateLessonScheduleAsync)
        .WithSummary("Ders planı oluşturur");

        group.MapPost("/lessons/{lessonId:guid}/cancel", CancelLessonScheduleAsync)
        .WithSummary("Ders planını iptal eder");

        group.MapPost("/lessons/{lessonId:guid}/complete", CompleteLessonScheduleAsync)
        .WithSummary("Ders planını tamamlandı olarak işaretler");

        group.MapGet("/lessons/{lessonId:guid}", GetLessonScheduleByIdAsync)
        .WithSummary("Ders planı detayını getirir");

        group.MapGet("/teachers/{teacherUserId:guid}/lessons", ListLessonSchedulesForTeacherAsync)
        .WithSummary("Öğretmenin ders planlarını listeler");
    }

    /// <summary>
    /// Öğretmen ve öğrenci için tarih, format, tekrar ve hatırlatma bilgileriyle ders planı oluşturur.
    /// </summary>
    private static async Task<IResult> CreateLessonScheduleAsync(
        HttpContext context,
        CreateLessonScheduleRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı dersi isteğe bağlı iptal notuyla iptal eder.
    /// </summary>
    private static async Task<IResult> CancelLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        CancelLessonScheduleRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new CancelLessonScheduleCommand(lessonId, request.CancellationNote), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı dersi tamamlandı olarak işaretler.
    /// </summary>
    private static async Task<IResult> CompleteLessonScheduleAsync(
        HttpContext context,
        Guid lessonId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new CompleteLessonScheduleCommand(lessonId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Planlı ders detayını ders planı kimliğiyle getirir.
    /// </summary>
    private static async Task<IResult> GetLessonScheduleByIdAsync(
        HttpContext context,
        Guid lessonId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetLessonScheduleByIdQuery(lessonId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmenin belirli UTC tarih aralığındaki ders planlarını listeler.
    /// </summary>
    private static async Task<IResult> ListLessonSchedulesForTeacherAsync(
        HttpContext context,
        Guid teacherUserId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListLessonSchedulesForTeacherQuery(teacherUserId, startAtUtc, endAtUtc),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "scheduling.teacher_conflict" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "scheduling.lesson_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "scheduling.already_completed" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// Ders planı oluşturmak için öğretmen, öğrenci, zaman, format, tekrar ve hatırlatma verilerini taşır.
/// </summary>
public sealed record CreateLessonScheduleRequest(
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? Notes)
{
    public CreateLessonScheduleCommand ToCommand()
    {
        return new CreateLessonScheduleCommand(
            TeacherUserId,
            StudentId,
            Subject,
            LessonFormat,
            StartAtUtc,
            EndAtUtc,
            TimeZone,
            RecurrenceRule,
            ReminderOffsetMinutes,
            LocationLabel,
            Notes);
    }
}

/// <summary>
/// Planlı ders iptal edilirken tutulacak isteğe bağlı açıklamayı taşır.
/// </summary>
public sealed record CancelLessonScheduleRequest(string? CancellationNote);
