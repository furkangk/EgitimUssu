using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Projections;

/// <summary>
/// Tek bir payload tipini tüketen projeksiyon handler'larının tabanı (K-04). Zarf içindeki JSON
/// payload'ı <typeparamref name="TEvent"/> tipine çözer ve <see cref="ProjectAsync"/>'e verir.
/// Idempotency + atomiklik tabandan (<see cref="IdempotentIntegrationEventHandler"/>) gelir:
/// aynı event iki kez gelirse projeksiyon bir kez uygulanır, uygulama hata verirse inbox satırı yazılmaz.
/// <see cref="ProjectAsync"/> SaveChanges ÇAĞIRMAZ; commit tabana aittir.
/// </summary>
/// <typeparam name="TEvent">Integration event'in JSON payload tipi (domain event sözleşmesi).</typeparam>
public abstract class ProjectionHandler<TEvent> : IdempotentIntegrationEventHandler
    where TEvent : class
{
    protected ProjectionHandler(ModuleDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    protected sealed override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            throw new PoisonMessageException("Projeksiyon olay yükü null olamaz.");
        }

        return await ProjectAsync(payload, envelope, cancellationToken);
    }

    /// <summary>Projeksiyon satırını <see cref="IdempotentIntegrationEventHandler.DbContext"/> üzerinde
    /// STAGE eder (SaveChanges YOK). Geçerli etkisiz sonuç true döner ve inbox'a yazılır; hata istisna ile bildirilir.</summary>
    protected abstract Task<bool> ProjectAsync(TEvent payload, IntegrationEvent envelope, CancellationToken cancellationToken);
}
