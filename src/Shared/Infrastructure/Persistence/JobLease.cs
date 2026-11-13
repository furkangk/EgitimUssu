namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>
/// A-07: Bir arka plan işinin "şu an bu örnek koşuyor" kirası. <see cref="OutboxMessage"/> ve
/// <see cref="InboxMessage"/>'ın kardeşi; modül şemasında yaşar.
/// <para>
/// Neden veritabanı, Redis değil: Redis'e bağlı korumaların hepsi <b>fail-open</b>'dır (ADR-0004) —
/// Redis düştüğünde kilit "alınmış" sayılıp iş yine koşardı ve çift bildirim tam da o an üretilirdi.
/// Kiranın doğruluğu işin kendisi kadar kritik olduğundan, işin yazdığı veritabanının kendisine
/// dayanır: aynı transaction sınırında, aynı erişilebilirlik garantisiyle.
/// </para>
/// </summary>
public sealed class JobLease
{
    /// <summary>EF için parametresiz kurucu.</summary>
    private JobLease()
    {
    }

    /// <summary>Yeni bir kira kaydı oluşturur.</summary>
    /// <param name="jobName">İşin benzersiz adı.</param>
    /// <param name="ownerId">Kirayı alan örnek.</param>
    /// <param name="acquiredOnUtc">Alınma anı.</param>
    /// <param name="expiresOnUtc">Sona erme anı.</param>
    public JobLease(string jobName, Guid ownerId, DateTime acquiredOnUtc, DateTime expiresOnUtc)
    {
        JobName = jobName;
        OwnerId = ownerId;
        AcquiredOnUtc = acquiredOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    /// <summary>İşin benzersiz adı (birincil anahtar).</summary>
    public string JobName { get; private set; } = string.Empty;

    /// <summary>Kirayı elinde tutan uygulama örneğinin kimliği.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Kiranın alındığı an.</summary>
    public DateTime AcquiredOnUtc { get; private set; }

    /// <summary>
    /// Kiranın sona erdiği an. Bu süre geçtikten sonra başka bir örnek kirayı devralabilir —
    /// böylece çöken bir örnek işi kalıcı olarak kilitlemez.
    /// </summary>
    public DateTime ExpiresOnUtc { get; private set; }
}
