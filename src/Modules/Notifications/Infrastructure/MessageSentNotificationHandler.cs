using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// M16-1 → M09-2/M11: Yeni mesajda alıcıya in-app bildirim.
/// <para>
/// Olay alıcıyı <b>kullanıcı kimliği</b> olarak taşır (sohbetin diğer tarafı), bu yüzden burada
/// dizin sorgusu yoktur. Push bu handler'da gönderilmez: <c>NotificationDispatcher</c> turu
/// üstlenir ve tercih kapısını orada uygular — böylece push'u kapatan kullanıcı bildirimi yine de
/// bildirim merkezinde görür.
/// </para>
/// <para>
/// Alıcı sohbeti <b>açıkken</b> de kayıt yazılır: "okundu" ayrı bir akıştır
/// (<c>POST /conversations/{id}/read</c>) ve bildirim orada değil, merkezde okunmuş sayılır.
/// </para>
/// </summary>
public sealed class MessageSentNotificationHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly IUserNotificationRepository _userNotifications;
    private readonly IIdGenerator _idGenerator;

    public MessageSentNotificationHandler(
        NotificationsDbContext dbContext,
        IUserNotificationRepository userNotifications,
        IIdGenerator idGenerator,
        IClock clock)
        : base(dbContext, clock)
    {
        _userNotifications = userNotifications;
        _idGenerator = idGenerator;
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "Messaging"
        && integrationEvent.Name == "MessageSentDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<MessageSentPayload>(envelope.Payload, JsonOptions);
        if (payload is null || payload.RecipientUserId == Guid.Empty)
        {
            return false; // bozuk yük: inbox'a yazılmaz, yeniden denenebilir
        }

        await _userNotifications.AddAsync(
            new UserNotification(
                _idGenerator.New(),
                payload.RecipientUserId,
                UserNotificationKind.MessageReceived,
                "Yeni mesajın var",
                payload.Preview,
                $"/messages/{payload.ConversationId}",
                Clock.UtcNow),
            cancellationToken);

        return true;
    }

    private sealed record MessageSentPayload(
        Guid ConversationId,
        Guid MessageId,
        Guid SenderUserId,
        Guid RecipientUserId,
        string Preview,
        DateTime SentOnUtc);
}
