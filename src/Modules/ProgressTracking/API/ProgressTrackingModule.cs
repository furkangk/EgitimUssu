using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.ProgressTracking.API;

public sealed class ProgressTrackingModule : ModuleDefinition
{
    public override string Name => "ProgressTracking";

    public override string RoutePrefix => "/api/progress-tracking";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddProgressTrackingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
        .WithSummary("İlerleme takibi modül durumunu getirir");

        var secure = CreateModuleGroup(endpoints);
        secure.RequireAuthorization("AuthenticatedUser");

        secure.MapGet("/students/{studentId:guid}/mastery", ListMasteryAsync).WithSummary("Ders/konu hâkimiyetini listeler");
        secure.MapGet("/students/{studentId:guid}/weak-spots", ListWeakSpotsAsync).WithSummary("Eksik konuları listeler");
        secure.MapGet("/students/{studentId:guid}/strengths", ListStrengthsAsync).WithSummary("Güçlü konuları listeler");
        secure.MapGet("/students/{studentId:guid}/overview", GetOverviewAsync).WithSummary("Gelişim genel bakışını getirir");
        secure.MapGet("/students/{studentId:guid}/topic-goals", ListGoalsAsync).WithSummary("Konu hedeflerini listeler");
        secure.MapPost("/students/{studentId:guid}/topic-goals", CreateGoalAsync).WithSummary("Konu hedefi oluşturur");
        secure.MapPost("/topic-goals/{goalId:guid}/cancel", CancelGoalAsync).WithSummary("Konu hedefini iptal eder");
    }

    private IResult GetStatus() => TypedResults.Ok(new
    {
        module = Name,
        route = RoutePrefix,
        state = "active"
    });

    private static async Task<IResult> ListMasteryAsync(HttpContext ctx, Guid studentId, string? subject, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListTopicMasteryQuery(studentId, subject), ct));

    private static async Task<IResult> ListWeakSpotsAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListWeakSpotsQuery(studentId), ct));

    private static async Task<IResult> ListStrengthsAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListStrengthsQuery(studentId), ct));

    private static async Task<IResult> GetOverviewAsync(HttpContext ctx, Guid studentId, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new GetProgressOverviewQuery(studentId), ct));

    private static async Task<IResult> ListGoalsAsync(HttpContext ctx, Guid studentId, string? status, IQueryDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new ListTopicGoalsQuery(studentId, status), ct));

    private static async Task<IResult> CreateGoalAsync(HttpContext ctx, Guid studentId, CreateTopicGoalRequest req, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(
            new CreateTopicGoalCommand(studentId, req.Subject, req.Topic, req.TargetMasteryLevel, req.TargetNetRatio, req.TargetDate), ct));

    private static async Task<IResult> CancelGoalAsync(HttpContext ctx, Guid goalId, ICommandDispatcher dispatcher, CancellationToken ct)
        => ToHttpResult(ctx, await dispatcher.Dispatch(new CancelTopicGoalCommand(goalId), ct));

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "progress.goal_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

public sealed record CreateTopicGoalRequest(
    string Subject,
    string Topic,
    string TargetMasteryLevel,
    decimal? TargetNetRatio,
    DateOnly? TargetDate);
