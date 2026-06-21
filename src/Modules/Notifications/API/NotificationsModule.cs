using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Notifications.API;

public sealed class NotificationsModule : ModuleDefinition
{
    public override string Name => "Notifications";

    public override string RoutePrefix => "/api/notifications";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotificationsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapGet("/teachers/{teacherUserId:guid}/lesson-reminders", async (
            HttpContext context,
            Guid teacherUserId,
            bool activeOnly,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new ListTeacherLessonRemindersQuery(teacherUserId, activeOnly), cancellationToken);
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
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}
