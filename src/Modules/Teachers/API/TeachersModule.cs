using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Modules.Teachers.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Teachers.API;

public sealed class TeachersModule : ModuleDefinition
{
    public override string Name => "Teachers";

    public override string RoutePrefix => "/api/teachers";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTeachersModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/profiles", async (
            HttpContext context,
            UpsertTeacherProfileRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToCreateCommand(), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapPut("/profiles/{userId:guid}", async (
            HttpContext context,
            Guid userId,
            UpsertTeacherProfileRequest request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(request.ToUpdateCommand(userId), cancellationToken);
            return ToHttpResult(context, result);
        });

        group.MapGet("/profiles/{userId:guid}", async (
            HttpContext context,
            Guid userId,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Dispatch(new GetTeacherProfileByUserIdQuery(userId), cancellationToken);
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
            "teachers.profile_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "teachers.profile_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

public sealed record TeacherAvailabilityItem(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

public sealed record UpsertTeacherProfileRequest(
    Guid UserId,
    string FullName,
    string Subject,
    string City,
    string District,
    string? Biography,
    string? Headline,
    TeacherLessonFormat LessonFormat,
    int ExperienceYears,
    string EducationLevel,
    decimal HourlyRateAmount,
    string Currency,
    bool IsVerified,
    string? ProfilePhotoUrl,
    IReadOnlyCollection<TeacherAvailabilityItem> AvailabilitySlots)
{
    public CreateTeacherProfileCommand ToCreateCommand()
    {
        return new CreateTeacherProfileCommand(
            UserId,
            FullName,
            Subject,
            City,
            District,
            Biography,
            Headline,
            LessonFormat,
            ExperienceYears,
            EducationLevel,
            HourlyRateAmount,
            Currency,
            ProfilePhotoUrl,
            AvailabilitySlots
                .Select(slot => new TeacherAvailabilityRequest(
                    slot.DayOfWeek,
                    slot.StartTime,
                    slot.EndTime,
                    slot.IsOnlineAvailable,
                    slot.IsInPersonAvailable))
                .ToArray());
    }

    public UpdateTeacherProfileCommand ToUpdateCommand(Guid userId)
    {
        return new UpdateTeacherProfileCommand(
            userId,
            FullName,
            Subject,
            City,
            District,
            Biography,
            Headline,
            LessonFormat,
            ExperienceYears,
            EducationLevel,
            HourlyRateAmount,
            Currency,
            IsVerified,
            ProfilePhotoUrl,
            AvailabilitySlots
                .Select(slot => new TeacherAvailabilityRequest(
                    slot.DayOfWeek,
                    slot.StartTime,
                    slot.EndTime,
                    slot.IsOnlineAvailable,
                    slot.IsInPersonAvailable))
                .ToArray());
    }
}
