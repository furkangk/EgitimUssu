using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Messaging;

/// <summary>
/// A-07: Zamanlayıcıyla koşan modül işleri için ortak taban — <b>tur başına kira</b> alır.
/// <para>
/// Modül arka plan servisleri (bildirim gönderimi, ödeme gecikme taraması, ödev son tarih taraması,
/// ilerleme anlık görüntüsü) düz bir <c>while + Task.Delay</c> döngüsüydü: kilit, leader election
/// ya da satır sahiplenme yoktu. Outbox <c>FOR UPDATE SKIP LOCKED</c> ile korunurken bu işlerin
/// karşılığı yoktu. İkinci bir uygulama örneği açıldığı anda aynı satırlar iki kez okunuyor ve
/// <b>çift push bildirimi</b>, <b>çift gecikme olayı</b> üretiliyordu — üstelik her örnek farklı bir
/// <c>EventId</c> ürettiği için tüketici tarafındaki <c>inbox_messages</c> dedup'ı da yakalayamıyordu.
/// </para>
/// <para>
/// Kira süresi bir turun makul üst sınırından uzun tutulur: tur uzarsa kira dolup başka bir örnek
/// aynı işe girmemeli. Çöken bir örnek işi kalıcı kilitlemez, kira süresi dolunca devralınır.
/// </para>
/// <para>
/// N-41: Kirayı elinde tutan örnek onu <b>her turda yeniler</b> (<see cref="IJobLeaseStore"/> sahibe
/// izin verir); iş, kendi <see cref="PollInterval"/>'ında koşar. Diğer örnekler, sahip yenilemeyi
/// bırakana — çökene ya da kapanana — kadar turu atlar; ardından en geç <see cref="LeaseDuration"/>
/// sonra devralır. Bedeli: yeniden başlayan bir süreç yeni bir <c>InstanceId</c> alır ve eski
/// sürecin kirası dolana kadar bekler (dağıtım sonrası bir kerelik gecikme).
/// </para>
/// </summary>
public abstract class LeasedBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    /// <summary>Kiranın benzersiz adı; modül şemasındaki <c>job_leases</c> tablosunun anahtarıdır.</summary>
    protected abstract string JobName { get; }

    /// <summary>Turlar arası bekleme.</summary>
    protected abstract TimeSpan PollInterval { get; }

    /// <summary>
    /// Kiranın geçerlilik süresi. Varsayılan: bir tur aralığının iki katı ve en az beş dakika —
    /// tur beklenenden uzun sürdüğünde kiranın altımızdan çekilmemesi için. Sahip her turda
    /// yenilediğinden bu süre işin sıklığını değil, yalnız <b>devralma gecikmesini</b> belirler.
    /// </summary>
    protected virtual TimeSpan LeaseDuration
    {
        get
        {
            var doubled = PollInterval + PollInterval;
            return doubled > TimeSpan.FromMinutes(5) ? doubled : TimeSpan.FromMinutes(5);
        }
    }

    /// <summary>Kiranın tutulacağı modül context'ini çözer.</summary>
    /// <param name="services">Tur için açılmış scope'un servis sağlayıcısı.</param>
    /// <returns>Modülün <see cref="ModuleDbContext"/>'i.</returns>
    protected abstract ModuleDbContext ResolveDbContext(IServiceProvider services);

    /// <summary>Kira alındıysa çalıştırılacak tur.</summary>
    /// <param name="services">Tur için açılmış scope'un servis sağlayıcısı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    protected abstract Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var leases = scope.ServiceProvider.GetRequiredService<IJobLeaseStore>();
                var dbContext = ResolveDbContext(scope.ServiceProvider);

                if (await leases.TryAcquireAsync(dbContext, JobName, LeaseDuration, stoppingToken))
                {
                    await ExecuteCycleAsync(scope.ServiceProvider, stoppingToken);
                }
                else
                {
                    // Kira süresi dolmamış başka bir örnekte (sahip kendi kirasını her turda yenilediği
                    // için buraya yalnız sahip olmayan örnek düşer); sessizce beklemek doğru davranıştır.
                    logger.LogDebug("{JobName} turu atlandı: kira başka bir örnekte.", JobName);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "{JobName} turu başarısız oldu.", JobName);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
