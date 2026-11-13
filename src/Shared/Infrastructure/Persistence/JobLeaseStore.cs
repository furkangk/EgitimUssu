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
/// </summary>
public sealed class JobLeaseStore(IClock clock) : IJobLeaseStore
{
    /// <summary>Bu uygulama örneğinin kimliği; kira sahipliğini günlüklerde ayırt etmeye yarar.</summary>
    public static readonly Guid InstanceId = Guid.NewGuid();

    /// <inheritdoc />
    public async Task<bool> TryAcquireAsync(
        ModuleDbContext dbContext,
        string jobName,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var expiresOn = now.Add(leaseDuration);

        if (!dbContext.Database.IsNpgsql())
        {
            // InMemory (test/dev): tek süreç, yarış yok — okuma/yazma yeterli.
            var existing = await dbContext.JobLeases
                .FirstOrDefaultAsync(lease => lease.JobName == jobName, cancellationToken);

            if (existing is null)
            {
                dbContext.JobLeases.Add(new JobLease(jobName, InstanceId, now, expiresOn));
                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            if (existing.ExpiresOnUtc > now)
            {
                return false;
            }

            dbContext.JobLeases.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.JobLeases.Add(new JobLease(jobName, InstanceId, now, expiresOn));
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var table = $"\"{dbContext.Model.GetDefaultSchema()}\".\"job_leases\"";
        var sql =
            $"INSERT INTO {table} (\"JobName\", \"OwnerId\", \"AcquiredOnUtc\", \"ExpiresOnUtc\") " +
            "VALUES ({0}, {1}, {2}, {3}) " +
            "ON CONFLICT (\"JobName\") DO UPDATE SET " +
            "\"OwnerId\" = EXCLUDED.\"OwnerId\", " +
            "\"AcquiredOnUtc\" = EXCLUDED.\"AcquiredOnUtc\", " +
            $"\"ExpiresOnUtc\" = EXCLUDED.\"ExpiresOnUtc\" WHERE {table}.\"ExpiresOnUtc\" <= {{2}}";

        var affected = await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            [jobName, InstanceId, now, expiresOn],
            cancellationToken);

        return affected == 1;
    }
}
