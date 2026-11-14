using System.Diagnostics;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        // C-04: Outbox turuyla aynı gerekçe — bu tur her modülün iki tablosunu tarar ve span'ı
        // olmadan her sorgu ayrı bir kök trace olur (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "retention.sweep",
            ActivityKind.Internal);

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
/// <para>
/// A-07 (P15 Task 8): Tur başına kira alır — düz bir <c>BackgroundService</c> iken her uygulama
/// örneği aynı silme turunu eşzamanlı koşuyordu. Kira süresi 48 saattir (iki tur); kirayı tutan örnek
/// her turda yeniler. Bedeli: yeniden başlayan bir süreç eski sürecin kirası dolana kadar (en çok
/// 48 saat) temizlik yapmaz — 90 günlük saklama süresi ve 6 aylık yasal üst sınır yanında önemsiz.
/// </para>
/// </summary>
internal sealed class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataRetentionService> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "data-retention";

    protected override TimeSpan PollInterval => TimeSpan.FromDays(1);

    /// <inheritdoc />
    /// <remarks>
    /// Tur tüm modül şemalarını tarar; tek küresel kira yeter. <c>OutgoingEmailDispatcher</c> ile
    /// aynı seçim: adı alfabetik ilk gelen modülün şeması — önemli olan her turda aynı olması.
    /// </remarks>
    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
    {
        var descriptor = services.GetServices<ModuleDbContextDescriptor>()
            .OrderBy(item => item.ModuleName, StringComparer.Ordinal)
            .First();

        return (ModuleDbContext)services.GetRequiredService(descriptor.DbContextType);
    }

    // Silinen satır sayısı işlemcinin kendisinde loglanır; turun düşmesi tabanda loglanır.
    protected override Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetRequiredService<IDataRetentionProcessor>().RunAsync(cancellationToken);
}
