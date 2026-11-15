using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Projections;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>Yalnız tahsilat boyutunu kanonik hareketlerden kurar; ders ve beklenen tutar boyutlarına dokunmaz.</summary>
public sealed class PaymentCollectionProjectionRebuilder(ReportingDbContext db, IPaymentTransactionDirectory source, ILocalCalendar calendar, IClock clock)
    : IProjectionRebuilder
{
    public string Name => "reporting.payment-collections";
    private static readonly Guid UpgradeMarker = Guid.Parse("9a073b5c-e180-4e02-a000-000000000001");
    public Task<int> RebuildAsync(CancellationToken cancellationToken) => RebuildCoreAsync(false, cancellationToken);
    public Task<int> EnsureInitializedAsync(CancellationToken cancellationToken) => RebuildCoreAsync(true, cancellationToken);
    private async Task<int> RebuildCoreAsync(bool initializeOnly, CancellationToken cancellationToken)
    {
        var written = 0;
        var attempt = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            await TeacherDashboardOccurrenceProcessor.AcquireLockAsync(db, cancellationToken);
            var initialized = await db.ModuleStates.AnyAsync(x => x.Id == UpgradeMarker, cancellationToken);
            if (initializeOnly && initialized) return;
            var projections = await db.TeacherMonthlyProjections.Include(x => x.Items).ToListAsync(cancellationToken);
            var lines = await source.ListAsync(cancellationToken);
            foreach (var projection in projections)
            {
                db.RemoveRange(projection.Items.Where(x => x.Kind is TeacherMonthlyItemKind.Collection or TeacherMonthlyItemKind.Refund));
                projection.ClearPaymentTransactions();
            }
            // Silinen ve yeniden eklenecek aynı PK'ları ChangeTracker'da bir arada tutma.
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            projections = await db.TeacherMonthlyProjections.Include(x => x.Items).ToListAsync(cancellationToken);
            foreach (var line in lines)
            {
                var (year, month) = ReportingPeriods.MonthOf(line.OccurredOnUtc, calendar);
                var projection = projections.SingleOrDefault(x => x.TeacherUserId == line.TeacherUserId && x.Year == year && x.Month == month);
                if (projection is null)
                {
                    projection = new TeacherMonthlyProjection(line.TeacherUserId, year, month);
                    db.Add(projection);
                    projections.Add(projection);
                }
                projection.TrackPaymentTransaction(line.TransactionId, line.IsRefund, line.Amount);
            }
            if (!initialized) db.ModuleStates.Add(new ModuleState { Id = UpgradeMarker, CreatedOnUtc = clock.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            written = lines.Count;
        });
        return written;
    }
}

/// <summary>Şema migration'larından sonra, HTTP sunucusu başlamadan eski delta raporlarını kaynak hareketlere geçirir.</summary>
internal sealed class PaymentCollectionProjectionInitializer(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PaymentCollectionProjectionRebuilder>().EnsureInitializedAsync(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
