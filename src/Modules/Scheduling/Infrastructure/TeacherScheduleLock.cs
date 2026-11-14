using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

/// <summary>
/// P16 T4 / N-07: Öğretmen başına <c>pg_advisory_xact_lock(hashtext('sched:' || teacherUserId))</c>. Kilit transaction'a
/// bağlıdır, commit/rollback ile kendiliğinden bırakılır; böylece "çakışma kontrolü → yaz → kaydet" eşzamanlı iki istekte
/// tek kazanan verir (ikincisi kilidi bekler, sonra ilkinin yazdığını görür → 409).
/// <para>
/// <c>EnableRetryOnFailure</c> açıkken kullanıcı transaction'ı yalnız execution strategy içinde açılabilir (Öğrenilenler
/// #195 c, <c>IdentityRepositoryAndSecurity</c> deseni); iş bütün olarak yeniden denenir. Başarısız sonuçta commit
/// edilmez (dispose geri alır). Dış bir transaction zaten açıksa kilit ona bağlanır ve commit dışarıya kalır.
/// İlişkisel olmayan sağlayıcıda (InMemory, test/dev) kilit yoktur — iş doğrudan çalışır.
/// </para>
/// <para>
/// Anahtar <c>hashtext</c> ile 32 bit'e indirilir; iki öğretmenin çakışan özeti yalnız onları gereksiz yere serileştirir,
/// doğruluğu bozmaz. Başka bir advisory lock kullanıcısı aynı tek-argümanlı uzayı paylaşırsa önekiyle ayrışmalıdır.
/// </para>
/// </summary>
internal sealed class TeacherScheduleLock : ITeacherScheduleLock
{
    private readonly SchedulingDbContext _dbContext;

    public TeacherScheduleLock(SchedulingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Kilit anahtarı; operasyonda elle tutmak/izlemek için de aynı metin kullanılır.</summary>
    public static string KeyFor(Guid teacherUserId) => $"sched:{teacherUserId:D}";

    public async Task<TResult> RunExclusiveAsync<TResult>(
        Guid teacherUserId,
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(work);
        var database = _dbContext.Database;
        if (!database.IsNpgsql())
        {
            return await work(cancellationToken);
        }

        if (database.CurrentTransaction is not null)
        {
            await AcquireAsync(teacherUserId, cancellationToken);
            return await work(cancellationToken);
        }

        var strategy = database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token =>
            {
                await using var transaction = await database.BeginTransactionAsync(token);
                await AcquireAsync(teacherUserId, token);

                var result = await work(token);
                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(token);
                }

                return result;
            },
            cancellationToken);
    }

    private Task<int> AcquireAsync(Guid teacherUserId, CancellationToken cancellationToken)
        => _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({KeyFor(teacherUserId)}))",
            cancellationToken);
}
