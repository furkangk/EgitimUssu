using System.Diagnostics;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Infrastructure.Observability;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;

namespace EgitimUssu.Modules.Payments.Infrastructure;

/// <summary>M07-1: Vadesi geçen ödemeleri işaretleyen tarama turu (arka plan servisinden ayrık, test edilebilir).</summary>
public interface IPaymentOverdueScanProcessor
{
    /// <summary>Bir tarama turu koşturur.</summary>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Bu turda <see cref="PaymentStatus.Overdue"/> işaretlenen kayıt sayısı.</returns>
    Task<int> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// M07-1: Vadesi geçmiş ve hâlâ açık ödeme kayıtlarını <see cref="PaymentStatus.Overdue"/> işaretler.
/// İşaretleme <see cref="PaymentRecord.MarkOverdue"/> içinde idempotenttir; zaten Overdue olan kayıt
/// sonraki turda sorguya bile girmez, dolayısıyla aynı kayıt için ikinci bir olay üretilmez.
/// Bildirim burada gönderilmez — <c>PaymentBecameOverdueDomainEvent</c> outbox'a düşer, Notifications
/// tüketir (Öğrenilenler #40: olay tüketicisinde/tarama turunda senkron push yok).
/// </summary>
public sealed class PaymentOverdueScanProcessor : IPaymentOverdueScanProcessor
{
    /// <summary>Tek turda işaretlenecek kayıt üst sınırı (tur süresini sınırlar).</summary>
    private const int BatchSize = 200;

    private readonly PaymentsDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;

    public PaymentOverdueScanProcessor(PaymentsDbContext dbContext, IClock clock, ILocalCalendar calendar)
    {
        _dbContext = dbContext;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        // C-04: Periyodik işin kendi span'ı. Olmazsa Npgsql bu turun her sorgusunu ayrı bir
        // **kök trace** olarak yayar (bkz. EgitimUssuActivitySource).
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            "scan.payments.overdue",
            ActivityKind.Internal);

        var now = _clock.UtcNow;

        var dayStart = _calendar.DayRangeUtc(_calendar.Today()).StartUtc;
        var candidates = await _dbContext.PaymentRecords
            .Where(record => record.DueDateUtc < dayStart
                && (record.Status == PaymentStatus.Pending || record.Status == PaymentStatus.PartiallyPaid))
            .OrderBy(record => record.DueDateUtc)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        var markedCount = candidates.Count(record => record.MarkOverdue(now, _calendar));
        if (markedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return markedCount;
    }
}

/// <summary>M07-1: Tarama turunu saatte bir koşturan arka plan servisi.</summary>
internal sealed class PaymentOverdueScanner(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentOverdueScanner> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "payment-overdue-scan";

    protected override TimeSpan PollInterval => TimeSpan.FromHours(1);

    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<PaymentsDbContext>();

    protected override async Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<IPaymentOverdueScanProcessor>();
        var markedCount = await processor.ScanAsync(cancellationToken);
        if (markedCount > 0)
        {
            logger.LogInformation("Payment overdue scan marked {MarkedCount} record(s) as overdue.", markedCount);
        }
    }
}
