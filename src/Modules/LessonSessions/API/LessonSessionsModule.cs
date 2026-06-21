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

        group.MapPost(string.Empty, async (
            HttpContext context,
            CreateLessonSessionRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToCommand(), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPost("/{lessonSessionId:guid}/complete", async (
            HttpContext context,
            Guid lessonSessionId,
            CompleteLessonSessionRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(
                new CompleteLessonSessionCommand(
                    lessonSessionId,
                    request.ActualStartAtUtc,
                    request.ActualEndAtUtc,
                    request.AttendanceStatus,
                    request.TopicTitle,
                    request.CoveredContent,
                    request.TeacherNotes),
                cancellationToken);

            return ToHttpResult(context, result);
        });

        group.MapGet("/{lessonSessionId:guid}", async (
            HttpContext context,
            Guid lessonSessionId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetLessonSessionByIdQuery(lessonSessionId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet(string.Empty, async (
            HttpContext context,
            Guid? teacherUserId,
            Guid? studentId,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(
                new ListLessonSessionsQuery(teacherUserId, studentId, dateFromUtc, dateToUtc),
                cancellationToken);
            return ToHttpResult(context, result);
        });
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
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

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

public sealed record CompleteLessonSessionRequest(
    DateTime ActualStartAtUtc,
    DateTime ActualEndAtUtc,
    StudentAttendanceStatus AttendanceStatus,
    string TopicTitle,
    string? CoveredContent,
    string? TeacherNotes);
