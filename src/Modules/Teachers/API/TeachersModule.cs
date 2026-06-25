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

        group.MapPost("/profiles", CreateTeacherProfileAsync)
        .WithSummary("Öğretmen profili oluşturur");

        group.MapPut("/profiles/{userId:guid}", UpdateTeacherProfileAsync)
        .WithSummary("Öğretmen profilini günceller");

        group.MapGet("/profiles/{userId:guid}", GetTeacherProfileAsync)
        .WithSummary("Öğretmen profilini getirir");
    }

    /// <summary>
    /// Öğretmenin uzmanlık, ücret, konum ve müsaitlik bilgilerini içeren yeni profil kaydını oluşturur.
    /// </summary>
    private static async Task<IResult> CreateTeacherProfileAsync(
        HttpContext context,
        UpsertTeacherProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToCreateCommand(), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Var olan öğretmen profilinin ders, ücret, doğrulama ve müsaitlik bilgilerini günceller.
    /// </summary>
    private static async Task<IResult> UpdateTeacherProfileAsync(
        HttpContext context,
        Guid userId,
        UpsertTeacherProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(request.ToUpdateCommand(userId), cancellationToken);
        return ToHttpResult(context, result);
    }

    /// <summary>
    /// Kullanıcı kimliğine göre öğretmen profil detayını getirir.
    /// </summary>
    private static async Task<IResult> GetTeacherProfileAsync(
        HttpContext context,
        Guid userId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetTeacherProfileByUserIdQuery(userId), cancellationToken);
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
            "teachers.profile_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            "teachers.profile_not_found" => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

/// <summary>
/// Öğretmenin haftalık tekrar eden müsaitlik aralığını ve ders formatı tercihlerini taşır.
/// </summary>
public sealed record TeacherAvailabilityItem(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOnlineAvailable,
    bool IsInPersonAvailable);

/// <summary>
/// Öğretmen profili oluşturma ve güncelleme işlemlerinde kullanılan profil, ücret ve müsaitlik verilerini taşır.
/// </summary>
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
