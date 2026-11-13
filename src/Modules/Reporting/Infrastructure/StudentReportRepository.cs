using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>M14-4: Üretilmiş PDF raporlarının kaydı (dosya <c>IFileStorage</c>'ta, burada yalnız anahtar).</summary>
internal sealed class StudentReportRepository : IStudentReportRepository
{
    private readonly ReportingDbContext _dbContext;

    public StudentReportRepository(ReportingDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(StudentReport report, CancellationToken cancellationToken)
        => await _dbContext.StudentReports.AddAsync(report, cancellationToken);

    public Task<StudentReport?> GetByIdAsync(Guid reportId, CancellationToken cancellationToken)
        => _dbContext.StudentReports
            .AsNoTracking()
            .FirstOrDefaultAsync(report => report.Id == reportId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
