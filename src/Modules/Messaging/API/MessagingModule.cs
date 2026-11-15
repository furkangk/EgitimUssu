using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Http;
using EgitimUssu.Shared.Infrastructure.Modules;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Messaging.API;

public sealed class MessagingModule : ModuleDefinition
{
    public override string Name => "Messaging";

    public override string RoutePrefix => "/api/messaging";

    public override void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMessagingModule(configuration);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = CreateModuleGroup(endpoints);

        group.MapGet("/status", GetStatus)
            .WithSummary("Mesajlaşma modül durumunu getirir");

        var secure = CreateModuleGroup(endpoints);
        secure.RequireAuthorization("AuthenticatedUser");

        secure.MapPost("/conversations", OpenConversationAsync)
            .WithSummary("Sohbeti açar; aynı çift + öğrenci bağlamı için varsa mevcudu döner (M16-1)");
        secure.MapGet("/conversations", ListConversationsAsync)
            .WithSummary("Gelen kutusu: son mesaj önizlemesi + okunmamış sayısı (M16-1)");
        secure.MapGet("/conversations/{conversationId:guid}/messages", ListMessagesAsync)
            .WithSummary("Sohbetin mesajlarını keyset sayfalamayla listeler (yeniden eskiye)");
        secure.MapPost("/conversations/{conversationId:guid}/messages", SendMessageAsync)
            .WithSummary("Sohbete mesaj gönderir (dakikada 30 mesaj sınırı)");
        secure.MapPost("/conversations/{conversationId:guid}/read", MarkReadAsync)
            .WithSummary("Sohbetteki gelen mesajları okundu işaretler");
        secure.MapPost("/conversations/{conversationId:guid}/block", BlockAsync)
            .WithSummary("Sohbeti engeller; gönderim iki taraf için de kapanır");
        secure.MapPost("/conversations/{conversationId:guid}/unblock", UnblockAsync)
            .WithSummary("Kendi koyduğun engeli kaldırır");
        secure.MapDelete("/messages/{messageId:guid}", DeleteMessageAsync)
            .WithSummary("Mesajı yumuşak siler (yalnız gönderen veya Admin)");
    }

    private static async Task<IResult> OpenConversationAsync(
        HttpContext context,
        OpenConversationRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new OpenConversationCommand(request.CounterpartUserId, request.CounterpartRole, request.StudentId),
            cancellationToken));

    private static async Task<IResult> ListConversationsAsync(
        HttpContext context,
        int? skip,
        int? take,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new ListConversationsQuery(skip ?? 0, take ?? MessagingPaging.DefaultTake), cancellationToken));

    private static async Task<IResult> ListMessagesAsync(
        HttpContext context,
        Guid conversationId,
        DateTime? beforeUtc,
        int? take,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new ListMessagesQuery(conversationId, beforeUtc, take ?? MessagingPaging.DefaultTake),
            cancellationToken));

    private static async Task<IResult> SendMessageAsync(
        HttpContext context,
        Guid conversationId,
        SendMessageRequest request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new SendMessageCommand(conversationId, request.Body ?? string.Empty), cancellationToken));

    private static async Task<IResult> MarkReadAsync(
        HttpContext context,
        Guid conversationId,
        MarkConversationReadRequest? request,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new MarkConversationReadCommand(conversationId, request?.UpToMessageId), cancellationToken));

    private static async Task<IResult> BlockAsync(
        HttpContext context,
        Guid conversationId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new SetConversationBlockCommand(conversationId, Blocked: true), cancellationToken));

    private static async Task<IResult> UnblockAsync(
        HttpContext context,
        Guid conversationId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new SetConversationBlockCommand(conversationId, Blocked: false), cancellationToken));

    private static async Task<IResult> DeleteMessageAsync(
        HttpContext context,
        Guid messageId,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => ToHttpResult(context, await dispatcher.Dispatch(
            new DeleteMessageCommand(messageId), cancellationToken));

    private static IResult ToHttpResult<T>(HttpContext context, Result<T> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : MapError(context, result.Error);

    private static IResult ToHttpResult(HttpContext context, Result result)
        => result.IsSuccess
            ? Results.NoContent()
            : MapError(context, result.Error);

    private static IResult MapError(HttpContext context, Error error)
        => error.Code switch
        {
            "shared.forbidden" => ApiErrorHttpResults.Forbidden(context, error.Message),

            // Katılımcı olmayan kullanıcı için 403: kaynağın varlığını doğrulamak sorun değil,
            // içeriği görmemesi yeterli. Kayıt gerçekten yoksa 404.
            "messaging.not_participant" => ApiErrorHttpResults.Forbidden(context, error.Message),

            // P15 T5 / K-28: ilişki bitti — gönderim yasak (403), geçmiş okunur. `.Forbidden(...)` kodu
            // `shared.forbidden`'a çevirirdi; istemci "bağ bitti"yi engelden ayırabilsin diye kod korunur (#182).
            "messaging.relationship_ended"
                => ApiErrorHttpResults.FromError(context, StatusCodes.Status403Forbidden, error),
            "messaging.conversation_not_found" or "messaging.message_not_found"
                => ApiErrorHttpResults.FromError(context, StatusCodes.Status404NotFound, error),

            // Kural çakışması: yasak çift, kendinle sohbet, bağ yok, engelli sohbet → 409.
            "messaging.pair_not_allowed" or "messaging.self_conversation"
                or "messaging.relationship_required" or "messaging.blocked"
                => ApiErrorHttpResults.FromError(context, StatusCodes.Status409Conflict, error),

            _ => ApiErrorHttpResults.FromError(context, StatusCodes.Status400BadRequest, error)
        };

    private IResult GetStatus()
    {
        return TypedResults.Ok(new
        {
            module = Name,
            route = RoutePrefix,
            state = "active"
        });
    }
}

/// <summary>Sohbet açma isteği.</summary>
/// <param name="CounterpartUserId">Karşı tarafın kullanıcı kimliği.</param>
/// <param name="CounterpartRole">Karşı tarafın rolü (<c>Teacher</c> | <c>Student</c> | <c>Parent</c>).</param>
/// <param name="StudentId">Sohbetin bağlamı olan öğrenci profili.</param>
public sealed record OpenConversationRequest(Guid CounterpartUserId, string CounterpartRole, Guid StudentId);

/// <summary>Mesaj gönderme isteği.</summary>
/// <param name="Body">Mesaj metni.</param>
public sealed record SendMessageRequest(string? Body);

public sealed record MarkConversationReadRequest(Guid? UpToMessageId);
