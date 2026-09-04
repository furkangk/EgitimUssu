using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    public PaymentOverdueScanProcessor(PaymentsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var candidates = await _dbContext.PaymentRecords
            .Where(record => record.DueDateUtc < now
                && (record.Status == PaymentStatus.Pending || record.Status == PaymentStatus.PartiallyPaid))
            .OrderBy(record => record.DueDateUtc)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        var markedCount = candidates.Count(record => record.MarkOverdue(now));
        if (markedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return markedCount;
    }
}

/// <summary>M07-1: Tarama turunu saatte bir koşturan arka plan servisi.</summary>
internal sealed class PaymentOverdueScanner : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentOverdueScanner> _logger;

    public PaymentOverdueScanner(IServiceScopeFactory scopeFactory, ILogger<PaymentOverdueScanner> logger)
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
                var processor = scope.ServiceProvider.GetRequiredService<IPaymentOverdueScanProcessor>();
                var markedCount = await processor.ScanAsync(stoppingToken);
                if (markedCount > 0)
                {
                    _logger.LogInformation("Payment overdue scan marked {MarkedCount} record(s) as overdue.", markedCount);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Payment overdue scan cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
