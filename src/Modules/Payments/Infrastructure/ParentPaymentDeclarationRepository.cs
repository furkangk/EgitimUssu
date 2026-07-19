using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

internal sealed class ParentPaymentDeclarationRepository : IParentPaymentDeclarationRepository
{
    private readonly PaymentsDbContext _dbContext;

    public ParentPaymentDeclarationRepository(PaymentsDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(ParentPaymentDeclaration declaration, CancellationToken cancellationToken)
        => _dbContext.ParentPaymentDeclarations.AddAsync(declaration, cancellationToken).AsTask();

    public Task<ParentPaymentDeclaration?> GetByIdAsync(Guid declarationId, CancellationToken cancellationToken)
        => _dbContext.ParentPaymentDeclarations.FirstOrDefaultAsync(d => d.Id == declarationId, cancellationToken);

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
