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

        group.MapPost("/lessons", async (
            HttpContext context,
            CreateLessonScheduleRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToCommand(), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPost("/lessons/{lessonId:guid}/cancel", async (
            HttpContext context,
            Guid lessonId,
            CancelLessonScheduleRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new CancelLessonScheduleCommand(lessonId, request.CancellationNote), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/lessons/{lessonId:guid}", async (
            HttpContext context,
            Guid lessonId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetLessonScheduleByIdQuery(lessonId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/teachers/{teacherUserId:guid}/lessons", async (
            HttpContext context,
            Guid teacherUserId,
            DateTime startAtUtc,
            DateTime endAtUtc,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(
                new ListLessonSchedulesForTeacherQuery(teacherUserId, startAtUtc, endAtUtc),
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
            "scheduling.teacher_conflict" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "scheduling.lesson_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

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

public sealed record CancelLessonScheduleRequest(string? CancellationNote);
