using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

/// <summary>
/// Integration event tüketicileri için ortak idempotency tabanı. Outbox en-az-bir-kez teslim
/// ettiğinden her (EventId, Handler) çifti en fazla bir kez işlenir. İş-yazımı + inbox-mark
/// tek transaction'da commit olur (sıkı atomik). <see cref="ApplyAsync"/> SaveChanges ÇAĞIRMAZ.
/// </summary>
public abstract class IdempotentIntegrationEventHandler : IIntegrationEventHandler
{
    protected IdempotentIntegrationEventHandler(ModuleDbContext dbContext, IClock clock)
    {
        DbContext = dbContext;
        Clock = clock;
    }

    protected ModuleDbContext DbContext { get; }

    protected IClock Clock { get; }

    /// <summary>Dedup anahtarının handler bileşeni. Varsayılan tip adı; override edilebilir.</summary>
    protected virtual string HandlerName => GetType().Name;

    public abstract bool CanHandle(IIntegrationEvent integrationEvent);

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        var relational = DbContext.Database.IsRelational();
        await using var transaction = relational
            ? await DbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var handlerName = HandlerName;
        // Bu guard "önce oku, sonra ekle" şeklindedir; bugün outbox dispatch tek-thread'li olduğu için güvenlidir.
        // İleride eşzamanlı bir dispatcher gelirse aynı (EventId, Handler) için iki dispatch bu guard'ı aynı anda
        // geçebilir — ikincisi commit'te composite-PK unique ihlaline çarpar; bu kendi kendini onarır (mesaj
        // tekrar dener, guard o zaman yakalar), o yüzden oradaki bir `DbUpdateException` FATAL sayılmamalı.
        var alreadyProcessed = await DbContext.Set<InboxMessage>()
            .AnyAsync(item => item.EventId == envelope.EventId && item.Handler == handlerName, cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var applied = await ApplyAsync(envelope, cancellationToken);
        if (!applied)
        {
            // Reddeden handler paylaşılan (scoped) DbContext'e iş-yazımı STAGE etmiş olabilir.
            // Transaction'ı açmamak/rollback ChangeTracker'ı sıfırlamaz — o yalnız DB etkisidir.
            // Temizlemezsek kardeş handler'ın SaveChanges'i bu terk edilmiş yazımları sessizce flush eder.
            DbContext.ChangeTracker.Clear();
            return;
        }

        DbContext.Set<InboxMessage>().Add(new InboxMessage(envelope.EventId, handlerName, envelope.Name, Clock.UtcNow));
        await DbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>İş etkisini <see cref="DbContext"/> üzerinde STAGE eder (SaveChanges YOK). İşlenecek
    /// bir şey yoksa false döner → inbox'a yazılmaz.</summary>
    protected abstract Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken);
}
