using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using EgitimUssu.Shared.Kernel;
using Npgsql;

namespace EgitimUssu.Modules.Payments.Infrastructure;

internal sealed class ParentPaymentDeclarationRepository : IParentPaymentDeclarationRepository
{
    private readonly PaymentsDbContext _dbContext;

    public ParentPaymentDeclarationRepository(PaymentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result> TryAddPendingAsync(ParentPaymentDeclaration declaration, CancellationToken cancellationToken)
    {
        _dbContext.ParentPaymentDeclarations.Add(declaration);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_payment_declaration_pending" })
        {
            // SaveChanges atomiktir: kaybeden beyan ve outbox olayı kalıcılaşmaz.
            // İstek bu başarısız sonuçla biter; bu context ile başka yazım yapılmaz.
            return Result.Failure(ParentPaymentDeclarationErrors.DeclarationPending);
        }
    }

    public async Task<IReadOnlyCollection<ParentPaymentDeclaration>> ListPendingForPaymentAsync(Guid paymentRecordId, CancellationToken cancellationToken)
        => await _dbContext.ParentPaymentDeclarations
            .Where(d => d.PaymentRecordId == paymentRecordId && d.Status == ParentPaymentDeclarationStatus.Declared)
            .ToArrayAsync(cancellationToken);

    public Task<ParentPaymentDeclaration?> GetByIdAsync(Guid declarationId, CancellationToken cancellationToken)
        => _dbContext.ParentPaymentDeclarations.FindAsync(new object?[] { declarationId }, cancellationToken).AsTask();

    public async Task<IReadOnlyCollection<ParentPaymentDeclaration>> ListForTeacherAsync(Guid teacherUserId, bool onlyPending, CancellationToken cancellationToken)
    {
        var query = _dbContext.ParentPaymentDeclarations.Where(d => d.TeacherUserId == teacherUserId);
        if (onlyPending)
        {
            query = query.Where(d => d.Status == ParentPaymentDeclarationStatus.Declared);
        }

        return await query
            .OrderByDescending(d => d.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
