using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

/// <summary>Veli paneli için öğrencinin ödeme kalemlerini (en yeni vade önce) canlı döner (Veli V-F).</summary>
internal sealed class StudentPaymentDigestDirectory : IStudentPaymentDigestDirectory
{
    private readonly PaymentsDbContext _dbContext;

    public StudentPaymentDigestDirectory(PaymentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<ParentPaymentLine>> GetLinesAsync(Guid studentId, int take, CancellationToken cancellationToken)
        => await _dbContext.PaymentRecords
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.DueDateUtc)
            .Take(take)
            .Select(p => new ParentPaymentLine(
                p.Id,
                p.Description,
                p.Currency,
                p.ExpectedAmount,
                p.CollectedAmount,
                p.DueDateUtc,
                p.Status.ToString()))
            .ToArrayAsync(cancellationToken);
}
