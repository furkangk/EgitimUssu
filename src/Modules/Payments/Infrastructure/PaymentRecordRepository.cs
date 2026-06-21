using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

internal sealed class PaymentRecordRepository : IPaymentRecordRepository
{
    private readonly PaymentsDbContext _dbContext;

    public PaymentRecordRepository(PaymentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PaymentRecord?> GetByIdAsync(Guid paymentRecordId, CancellationToken cancellationToken)
    {
        return _dbContext.PaymentRecords.FirstOrDefaultAsync(record => record.Id == paymentRecordId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
    {
        return await _dbContext.PaymentRecords
            .Where(record => record.TeacherUserId == teacherUserId)
            .OrderBy(record => record.DueDateUtc)
            .ThenBy(record => record.Description)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(PaymentRecord paymentRecord, CancellationToken cancellationToken)
    {
        return _dbContext.PaymentRecords.AddAsync(paymentRecord, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
