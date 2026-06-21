using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Assignments.API;

public sealed class AssignmentsModule : ModuleDefinition
{
    public override string Name => "Assignments";

    public override string RoutePrefix => "/api/assignments";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAssignmentsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/lesson-sessions/{lessonSessionId:guid}/follow-up", async (
            HttpContext context,
            Guid lessonSessionId,
            CreateLessonSessionFollowUpRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToCommand(lessonSessionId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/lesson-sessions/{lessonSessionId:guid}/follow-up", async (
            HttpContext context,
            Guid lessonSessionId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetLessonSessionFollowUpQuery(lessonSessionId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet(string.Empty, async (
            HttpContext context,
            Guid? teacherUserId,
            Guid? studentId,
            Guid? lessonSessionId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(
                new ListAssignmentsQuery(teacherUserId, studentId, lessonSessionId),
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
            "assignments.follow_up_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "assignments.lesson_session_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "assignments.follow_up_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

public sealed record CreateLessonSessionFollowUpAssignmentRequest(
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    string? AttachmentUrl);

public sealed record CreateLessonSessionFollowUpRequest(
    string Summary,
    string? CoveredTopics,
    string? Recommendations,
    IReadOnlyCollection<CreateLessonSessionFollowUpAssignmentRequest>? Assignments)
{
    public CreateLessonSessionFollowUpCommand ToCommand(Guid lessonSessionId)
    {
        return new CreateLessonSessionFollowUpCommand(
            lessonSessionId,
            Summary,
            CoveredTopics,
            Recommendations,
            (Assignments ?? [])
                .Select(assignment => new AssignmentDraft(
                    assignment.Title,
                    assignment.Description,
                    assignment.DueDateUtc,
                    assignment.AttachmentUrl))
                .ToArray());
    }
}
