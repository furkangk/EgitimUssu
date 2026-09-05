using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>E-07: Bir saklama süresi temizliği turunun sonucu.</summary>
/// <param name="OutboxDeleted">Silinen işlenmiş outbox satırı sayısı.</param>
/// <param name="InboxDeleted">Silinen inbox (idempotency) satırı sayısı.</param>
public sealed record DataRetentionRunSummary(int OutboxDeleted, int InboxDeleted)
{
    /// <summary>Toplam silinen satır.</summary>
    public int Total => OutboxDeleted + InboxDeleted;
}

/// <summary>E-07: Saklama süresi dolan teknik kayıtları temizleyen tur (arka plandan ayrık, test edilebilir).</summary>
public interface IDataRetentionProcessor
{
    /// <summary>Bir temizlik turu koşturur.</summary>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task<DataRetentionRunSummary> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// E-07: Saklama süresi dolan <b>teknik</b> kayıtları siler — işlenmiş outbox mesajları ve inbox
/// (idempotency) satırları, tüm modül şemalarında.
/// <para>
/// Bu satırlar kişisel veri taşımaz ama olay gövdelerinde kişisel veri <b>bulunabilir</b>
/// (ör. bir bildirim olayının başlığı). Süresiz saklanmaları için bir sebep yoktur: amaç hata
/// ayıklamadır ve 90 gün sonra o amaç da biter.
/// (Süreler: <c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §2.4.)
/// </para>
/// <para>
/// <b>İşlenmemiş</b> outbox satırlarına dokunulmaz — onlar henüz yayınlanmamış iştir; süresi geçmiş
/// olması onları çöp yapmaz, tam tersine bir arıza işaretidir.
/// </para>
/// </summary>
public sealed class DataRetentionProcessor : IDataRetentionProcessor
{
    /// <summary>İşlenmiş outbox ve inbox satırlarının saklama süresi.</summary>
    public static readonly TimeSpan MessageLogRetention = TimeSpan.FromDays(90);

    /// <summary>Tek turda tek tablodan silinecek satır üst sınırı (tur süresini sınırlar).</summary>
    private const int BatchSize = 1000;

    private readonly IEnumerable<ModuleDbContextDescriptor> _descriptors;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger<DataRetentionProcessor> _logger;

    public DataRetentionProcessor(
        IEnumerable<ModuleDbContextDescriptor> descriptors,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<DataRetentionProcessor> logger)
    {
        _descriptors = descriptors;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DataRetentionRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _clock.UtcNow - MessageLogRetention;
        var outboxDeleted = 0;
        var inboxDeleted = 0;

        foreach (var descriptor in _descriptors.OrderBy(item => item.ModuleName))
        {
            var context = (ModuleDbContext)_serviceProvider.GetRequiredService(descriptor.DbContextType);

            var outbox = await context.OutboxMessages
                .Where(message => message.ProcessedOnUtc != null && message.ProcessedOnUtc < cutoff)
                .Take(BatchSize)
                .ToArrayAsync(cancellationToken);

            var inbox = await context.InboxMessages
                .Where(message => message.ProcessedOnUtc < cutoff)
                .Take(BatchSize)
                .ToArrayAsync(cancellationToken);

            if (outbox.Length == 0 && inbox.Length == 0)
            {
                continue;
            }

            context.OutboxMessages.RemoveRange(outbox);
            context.InboxMessages.RemoveRange(inbox);
            await context.SaveChangesAsync(cancellationToken);

            outboxDeleted += outbox.Length;
            inboxDeleted += inbox.Length;
        }

        if (outboxDeleted > 0 || inboxDeleted > 0)
        {
            _logger.LogInformation(
                "Saklama süresi temizliği: {Outbox} outbox, {Inbox} inbox satırı silindi ({Cutoff} öncesi).",
                outboxDeleted, inboxDeleted, cutoff);
        }

        return new DataRetentionRunSummary(outboxDeleted, inboxDeleted);
    }
}

/// <summary>
/// E-07: Saklama süresi temizliğini <b>günde bir</b> koşturan arka plan servisi.
/// <para>
/// Mevzuat periyodik imha için 6 aylık bir üst sınır koyar; günlük koşum o sınırın çok altındadır
/// ve süresi dolan bir kaydı en fazla bir gün fazladan tutar.
/// </para>
/// </summary>
internal sealed class DataRetentionService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(IServiceScopeFactory scopeFactory, ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDataRetentionProcessor>();
                await processor.RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Saklama süresi temizliği başarısız oldu.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
