using System.Text.Json;
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

        // A-18: Bağlantı dayanıklılığı (`EnableRetryOnFailure`) açıkken EF, kullanıcı tarafından
        // başlatılan transaction'ı reddeder — çünkü geçici bir hatada neyin yeniden çalıştırılacağını
        // bilemez. Sınırı biz çiziyoruz: aşağıdaki iş birimi **bir bütün olarak** yeniden denenebilir.
        var strategy = DbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        try
        {
            await strategy.ExecuteAsync(
                async token =>
                {
                    // Yeniden denemede önceki denemenin STAGE ettiği yazımlar ChangeTracker'da durur
                    // (rollback yalnız veritabanını geri alır, tracker'ı değil). Temizlenmezse
                    // `ApplyAsync` ikinci kez çalışır ve aynı satırlar iki kez eklenir.
                    if (attempt++ > 0)
                    {
                        DbContext.ChangeTracker.Clear();
                    }

                    await ExecuteUnitOfWorkAsync(envelope, token);
                },
                cancellationToken);
        }
        catch (JsonException)
        {
            DbContext.ChangeTracker.Clear();
            throw new PoisonMessageException("Olay yükü geçerli JSON sözleşmesine uymuyor.");
        }
        catch
        {
            // Rollback veritabanını geri alır; aynı scope'taki kardeş tüketicinin SaveChanges'i
            // başarısız işin izlenen değişikliklerini kaydetmemelidir.
            DbContext.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>Guard → uygula → inbox'ı işaretle → tek transaction'da commit. Bir bütün olarak yeniden denenebilir.</summary>
    private async Task ExecuteUnitOfWorkAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
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
            throw new InvalidOperationException("Handler işlemi tamamlamadı; açık istisna veya başarılı etkisiz sonuç kullanın.");
        }

        DbContext.Set<InboxMessage>().Add(new InboxMessage(envelope.EventId, handlerName, envelope.Name, Clock.UtcNow));
        await DbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>İş etkisini <see cref="DbContext"/> üzerinde STAGE eder (SaveChanges YOK). İşlenecek
    /// bir şey yoksa true döner → başarılı etkisiz sonuç da inbox'a yazılır. Hata istisna ile bildirilir.</summary>
    protected abstract Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken);
}
