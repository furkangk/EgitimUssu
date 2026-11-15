using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

internal sealed class PaymentTransactionDirectory(PaymentsDbContext db) : IPaymentTransactionDirectory
{
    public async Task<IReadOnlyCollection<PaymentTransactionLine>> ListAsync(CancellationToken cancellationToken)
        => await (from movement in db.PaymentTransactions.AsNoTracking()
                  join record in db.PaymentRecords.IgnoreAutoIncludes().AsNoTracking() on movement.PaymentRecordId equals record.Id
                  select new PaymentTransactionLine(movement.Id, record.TeacherUserId,
                      movement.Kind == PaymentTransactionKind.Refund, movement.Amount, record.Currency, movement.OccurredOnUtc))
            .ToArrayAsync(cancellationToken);
}
