using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.LessonSessions.API;

public sealed class LessonSessionsModule : ModuleDefinition
{
    public override string Name => "LessonSessions";

    public override string RoutePrefix => "/api/lesson-sessions";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddLessonSessionsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost(string.Empty, CreateLessonSessionAsync)
        .WithSummary("Ders oturumu oluşturur");

        group.MapPost("/{lessonSessionId:guid}/complete", CompleteLessonSessionAsync)
        .WithSummary("Ders oturumunu tamamlar");

        group.MapPost("/{lessonSessionId:guid}/start", StartLessonSessionAsync)
        .WithSummary("Planlı dersi başlatır — Planned → InProgress (M05-1)");

        group.MapPost("/{lessonSessionId:guid}/cancel", CancelLessonSessionAsync)
        .WithSummary("Planlı veya devam eden dersi iptal eder (M05-1)");

        group.MapPost("/from-schedule/{lessonScheduleId:guid}", CreateSessionFromScheduleAsync)
        .WithSummary("Planlı dersten oturum türetir — aynı plan için idempotent (M05-2)");

        group.MapGet("/{lessonSessionId:guid}", GetLessonSessionByIdAsync)
        .WithSummary("Ders oturumu detayını getirir");

        group.MapGet(string.Empty, ListLessonSessionsAsync)
        .WithSummary("Ders oturumlarını listeler");
    }

    /// <summary>
    /// Planlı ya da bağımsız bir ders için oturum kaydı oluşturur.
    /// </summary>
    private static async Task<IResult> CreateLessonSessionAsync(
        HttpContext context,
        CreateLessonSessionRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Ders oturumunu gerçek saat, yoklama, işlenen konu ve öğretmen notlarıyla tamamlanmış duruma getirir.
    /// </summary>
    private static async Task<IResult> CompleteLessonSessionAsync(
        HttpContext context,
        Guid lessonSessionId,
        CompleteLessonSessionRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new CompleteLessonSessionCommand(
                lessonSessionId,
                request.ActualStartAtUtc,
                request.ActualEndAtUtc,
                request.AttendanceStatus,
                request.TopicTitle,
                request.CoveredContent,
                request.TeacherNotes,
                request.IsChargeable),
            cancellationToken);

        return ToHttpResult(context, result);
    }

    /// <summary>
    /// M05-1: Dersi başlatır. Yalnız <c>Planned</c> durumundaki oturum başlatılabilir; aksi halde 409.
    /// </summary>
    private static async Task<IResult> StartLessonSessionAsync(
        HttpContext context,
        Guid lessonSessionId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new StartLessonSessionCommand(lessonSessionId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// M05-1: Dersi iptal eder. Yalnız <c>Planned</c>/<c>InProgress</c> iptal edilebilir; aksi halde 409.
    /// </summary>
    private static async Task<IResult> CancelLessonSessionAsync(
        HttpContext context,
        Guid lessonSessionId,
        CancelLessonSessionRequest? request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new CancelLessonSessionCommand(lessonSessionId, request?.Reason), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// M05-2: Planlı dersten (M04) oturum türetir. Aynı plan için ikinci çağrı mevcut oturumu döner.
    /// </summary>
    private static async Task<IResult> CreateSessionFromScheduleAsync(
        HttpContext context,
        Guid lessonScheduleId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new CreateSessionFromScheduleCommand(lessonScheduleId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Ders oturumu detayını oturum kimliğiyle getirir.
    /// </summary>
    private static async Task<IResult> GetLessonSessionByIdAsync(
        HttpContext context,
        Guid lessonSessionId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetLessonSessionByIdQuery(lessonSessionId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Öğretmen, öğrenci ve tarih aralığı filtreleriyle ders oturumlarını listeler.
    /// </summary>
    private static async Task<IResult> ListLessonSessionsAsync(
        HttpContext context,
        Guid? teacherUserId,
        Guid? studentId,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(
            new ListLessonSessionsQuery(teacherUserId, studentId, dateFromUtc, dateToUtc),
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
            "lesson_sessions.not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "lesson_sessions.schedule_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            // M05-2: plan iptal/tamamlanmis -> istek dogru, kaynagin durumu uygun degil.
            "lesson_sessions.schedule_not_derivable" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            // M05-1: gecersiz durum gecisi bir cakismadir (istek dogru, kaynagin durumu uygun degil).
            "lesson_sessions.invalid_transition" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            // P15 T2 / N-08: başka öğretmenin (ya da başka öğrencinin) planlı dersine oturum bağlanamaz.
            // Kod korunur (Forbidden() kodu shared.forbidden'a çevirirdi) — istemci nedeni ayırt edebilsin.
            "lesson_sessions.schedule_not_owned" => ApiErrorHttpResults.FromError(context, StatusCodes.Status403Forbidden, result.Error),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// Ders oturumu oluşturmak için plan, öğretmen, öğrenci, ders ve başlangıç bilgilerini taşır.
/// </summary>
public sealed record CreateLessonSessionRequest(
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    DateTime PlannedStartAtUtc,
    string TopicTitle)
{
    public CreateLessonSessionCommand ToCommand()
    {
        return new CreateLessonSessionCommand(
            LessonScheduleId,
            TeacherUserId,
            StudentId,
            Subject,
            PlannedStartAtUtc,
            TopicTitle);
    }
}

/// <summary>
/// M05-1: Ders iptal isteği — gövde tümüyle opsiyoneldir.
/// </summary>
/// <param name="Reason">İptal nedeni (serbest metin, opsiyonel).</param>
public sealed record CancelLessonSessionRequest(string? Reason);

/// <summary>
/// Ders oturumunu tamamlamak için gerçek süre, yoklama durumu ve ders içeriği bilgilerini taşır.
/// </summary>
public sealed record CompleteLessonSessionRequest(
    DateTime ActualStartAtUtc,
    DateTime ActualEndAtUtc,
    StudentAttendanceStatus AttendanceStatus,
    string TopicTitle,
    string? CoveredContent,
    string? TeacherNotes,
    bool IsChargeable);
