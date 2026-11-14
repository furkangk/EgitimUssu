using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>
/// A-07: Bir arka plan işinin kirasını atomik olarak almaya çalışır.
/// <para>
/// Kira, işin kendi modül şemasında tutulur; alma işlemi tek bir koşullu <c>INSERT ... ON CONFLICT
/// DO UPDATE</c> ifadesidir. İki uygulama örneği aynı anda denerse yalnız biri satır güncelleyip
/// <c>true</c> alır. Süresi dolmuş kira devralınabilir — böylece çöken bir örnek işi kalıcı
/// olarak kilitlemez.
/// </para>
/// <para>
/// N-41: Kiranın <b>sahibi</b> onu süresi dolmadan da yeniler. Önceki koşul yalnız "süresi dolmuşsa"
/// idi; kira bir tur aralığından uzun olduğu için (en az 5 dk) sahip bir sonraki turda kendi kirasına
/// takılıyor ve 15–30 sn'lik işler ~5 dk'da bir koşuyordu. Kural: kira <b>boşsa, süresi dolmuşsa ya
/// da zaten bende ise</b> alınır; başka bir örnekteyse ve süresi dolmadıysa alınamaz.
/// </para>
/// </summary>
public sealed class JobLeaseStore : IJobLeaseStore
{
    /// <summary>Bu uygulama örneğinin kimliği; kira sahipliğini günlüklerde ayırt etmeye yarar.</summary>
    public static readonly Guid InstanceId = Guid.NewGuid();

    private readonly IClock _clock;
    private readonly Guid _ownerId;

    /// <summary>DI kurucusu: kirayı bu sürecin <see cref="InstanceId"/>'si adına alır.</summary>
    /// <param name="clock">Kira anlarının kaynağı.</param>
    public JobLeaseStore(IClock clock)
        : this(clock, InstanceId)
    {
    }

    /// <summary>
    /// Kirayı verilen sahip adına alan kurucu. Aynı süreçteki her host aynı <see cref="InstanceId"/>'yi
    /// paylaştığı için "iki ayrı örnek" senaryosu ancak bu kurucuyla kurulabilir (testler).
    /// </summary>
    /// <param name="clock">Kira anlarının kaynağı.</param>
    /// <param name="ownerId">Kirayı alan örneğin kimliği.</param>
    public JobLeaseStore(IClock clock, Guid ownerId)
    {
        _clock = clock;
        _ownerId = ownerId;
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireAsync(
        ModuleDbContext dbContext,
        string jobName,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var expiresOn = now.Add(leaseDuration);

        if (!dbContext.Database.IsNpgsql())
        {
            // InMemory (test/dev): tek süreç, yarış yok — okuma/yazma yeterli.
            var existing = await dbContext.JobLeases
                .FirstOrDefaultAsync(lease => lease.JobName == jobName, cancellationToken);

            if (existing is null)
            {
                dbContext.JobLeases.Add(new JobLease(jobName, _ownerId, now, expiresOn));
                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            // N-41: Postgres dalıyla aynı kural — sahibi yenileyebilir, başkası süre dolana kadar bekler.
            if (existing.ExpiresOnUtc > now && existing.OwnerId != _ownerId)
            {
                return false;
            }

            dbContext.JobLeases.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.JobLeases.Add(new JobLease(jobName, _ownerId, now, expiresOn));
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        // N-41: Güncelleme yalnız kira süresi dolmuşsa YA DA satır zaten bu sahibe aitse olur. Satır
        // başka bir örnekteyse ve süresi dolmadıysa WHERE eşleşmez, etkilenen satır 0 döner.
        var table = $"\"{dbContext.Model.GetDefaultSchema()}\".\"job_leases\"";
        var sql =
            $"INSERT INTO {table} (\"JobName\", \"OwnerId\", \"AcquiredOnUtc\", \"ExpiresOnUtc\") " +
            "VALUES ({0}, {1}, {2}, {3}) " +
            "ON CONFLICT (\"JobName\") DO UPDATE SET " +
            "\"OwnerId\" = EXCLUDED.\"OwnerId\", " +
            "\"AcquiredOnUtc\" = EXCLUDED.\"AcquiredOnUtc\", " +
            $"\"ExpiresOnUtc\" = EXCLUDED.\"ExpiresOnUtc\" " +
            $"WHERE {table}.\"ExpiresOnUtc\" <= {{2}} OR {table}.\"OwnerId\" = EXCLUDED.\"OwnerId\"";

        var affected = await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            [jobName, _ownerId, now, expiresOn],
            cancellationToken);

        return affected == 1;
    }
}
