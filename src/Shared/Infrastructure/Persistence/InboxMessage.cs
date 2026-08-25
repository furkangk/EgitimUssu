namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>
/// "Bu handler bu event'i işledi" kaydı (tüketici idempotency). <see cref="OutboxMessage"/>'ın kardeşi.
/// Bileşik anahtar (EventId, Handler): tek event birden çok handler tarafından tüketilebilir.
/// </summary>
public sealed class InboxMessage
{
    public InboxMessage(Guid eventId, string handler, string eventName, DateTime processedOnUtc)
    {
        EventId = eventId;
        Handler = handler;
        EventName = eventName;
        ProcessedOnUtc = processedOnUtc;
    }

    private InboxMessage()
    {
    }

    public Guid EventId { get; private set; }

    public string Handler { get; private set; } = string.Empty;

    public string EventName { get; private set; } = string.Empty;

    public DateTime ProcessedOnUtc { get; private set; }
}
