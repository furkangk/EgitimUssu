using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Parents.API;

public sealed class ParentsModule : ModuleDefinition
{
    public override string Name => "Parents";

    public override string RoutePrefix => "/api/parents";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddParentsModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);
        group.RequireAuthorization("AuthenticatedUser");

        group.MapPost("/profiles", CreateProfileAsync)
            .WithSummary("Veli profili oluşturur (gerçek Parent kullanıcısı için)");
        group.MapGet("/profiles/{userId:guid}", GetProfileAsync)
            .WithSummary("Veli profilini ve bildirim tercihlerini getirir");
        group.MapPut("/{parentUserId:guid}/notification-preferences", UpdateNotificationPreferencesAsync)
            .WithSummary("Veli bildirim tercihlerini günceller");

        group.MapPost("/children/link", RequestChildLinkAsync)
            .WithSummary("Çocuğa bağlanma talebi oluşturur (onay bekler)");
        group.MapPost("/children/{linkId:guid}/approve", ApproveChildLinkAsync)
            .WithSummary("Veli–çocuk bağını onaylar (öğrenci/öğretmen/Admin)");
        group.MapPost("/children/{linkId:guid}/reject", RejectChildLinkAsync)
            .WithSummary("Veli–çocuk bağını reddeder");
        group.MapPost("/children/{linkId:guid}/revoke", RevokeChildLinkAsync)
            .WithSummary("Veli–çocuk bağını iptal eder");
        group.MapGet("/{parentUserId:guid}/children", ListChildrenAsync)
            .WithSummary("Velinin bağlı çocuklarını (durum + özet) listeler");
        group.MapGet("/{parentUserId:guid}/children/{studentId:guid}/dashboard", GetChildDashboardAsync)
            .WithSummary("Onaylı bağlı çocuğun birleşik gelişim panelini getirir");
    }

    private static async Task<IResult> CreateProfileAsync(
        HttpContext context,
        CreateParentProfileRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var command = new CreateParentProfileCommand(request.UserId, request.FullName, request.ContactPhone, request.ContactEmail);
        var result = await dispatcher.Dispatch(command, cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> GetProfileAsync(
        HttpContext context,
        Guid userId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetParentProfileQuery(userId), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> UpdateNotificationPreferencesAsync(
        HttpContext context,
        Guid parentUserId,
        UpdateNotificationPreferencesRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var channel = ParseChannel(request.Channel);
        var command = new UpdateNotificationPreferencesCommand(
            parentUserId,
            request.MissedAssignment,
            request.WeeklyProgressSummary,
            request.LessonReminders,
            request.TestResults,
            request.Payments,
            channel);
        var result = await dispatcher.Dispatch(command, cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> RequestChildLinkAsync(
        HttpContext context,
        RequestChildLinkRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var command = new RequestChildLinkCommand(
            request.ParentUserId,
            request.StudentId,
            request.Relationship,
            request.ChildDisplayName,
            request.InviteCode,
            request.IsPrimaryContact);
        var result = await dispatcher.Dispatch(command, cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> ApproveChildLinkAsync(
        HttpContext context,
        Guid linkId,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var approverId))
        {
            return ApiErrorHttpResults.Unauthorized(context, "Onaylayan kullanıcı belirlenemedi.");
        }

        var result = await dispatcher.Dispatch(new ApproveChildLinkCommand(linkId, approverId), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> RejectChildLinkAsync(
        HttpContext context,
        Guid linkId,
        ICurrentUser currentUser,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var reviewerId))
        {
            return ApiErrorHttpResults.Unauthorized(context, "İşlemi yapan kullanıcı belirlenemedi.");
        }

        var result = await dispatcher.Dispatch(new RejectChildLinkCommand(linkId, reviewerId), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> RevokeChildLinkAsync(
        HttpContext context,
        Guid linkId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new RevokeChildLinkCommand(linkId), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> ListChildrenAsync(
        HttpContext context,
        Guid parentUserId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new ListChildrenQuery(parentUserId), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static async Task<IResult> GetChildDashboardAsync(
        HttpContext context,
        Guid parentUserId,
        Guid studentId,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Dispatch(new GetChildDashboardQuery(parentUserId, studentId), cancellationToken);
        return ToHttpResult(context, result);
    }

    private static ParentNotificationChannel ParseChannel(string? channel)
        => Enum.TryParse<ParentNotificationChannel>(channel, ignoreCase: true, out var parsed)
            ? parsed
            : ParentNotificationChannel.Push;

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error.Code switch
        {
            "shared.forbidden" or "parents.link_not_approved" => ApiErrorHttpResults.Forbidden(context, result.Error.Message),
            "parents.profile_not_found" or "parents.link_not_found" =>
                ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, result.Error),
            "parents.link_exists" or "parents.primary_exists" => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, result.Error),
            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, result.Error)
        };
    }
}

public sealed record CreateParentProfileRequest(Guid UserId, string FullName, string? ContactPhone, string? ContactEmail);

public sealed record UpdateNotificationPreferencesRequest(
    bool MissedAssignment,
    bool WeeklyProgressSummary,
    bool LessonReminders,
    bool TestResults,
    bool Payments,
    string? Channel);

public sealed record RequestChildLinkRequest(
    Guid ParentUserId,
    Guid StudentId,
    string? Relationship,
    string? ChildDisplayName,
    string? InviteCode,
    bool IsPrimaryContact);
