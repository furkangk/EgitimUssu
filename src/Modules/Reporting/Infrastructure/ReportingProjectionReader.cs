using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>
/// M14 projeksiyon satırlarının salt-okunur erişimi. Dönem sınırları <c>(yıl, dönem)</c> çiftidir; EF'in
/// demet karşılaştırmasını çeviremediği yerlerde açık <c>&gt;</c> / <c>==</c> ayrımı yazılır (Öğrenilenler #83:
/// filtre projeksiyondan önce, çevrilebilir ifadelerle).
/// </summary>
internal sealed class ReportingProjectionReader : IReportingProjectionReader
{
    private readonly ReportingDbContext _dbContext;

    public ReportingProjectionReader(ReportingDbContext dbContext) => _dbContext = dbContext;

    public Task<TeacherMonthlyProjection?> GetTeacherMonthlyAsync(
        Guid teacherUserId, int year, int month, CancellationToken cancellationToken)
        => _dbContext.TeacherMonthlyProjections
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.TeacherUserId == teacherUserId && item.Year == year && item.Month == month,
                cancellationToken);

    public async Task<IReadOnlyList<StudentStudyProjection>> ListStudentStudyAsync(
        Guid studentId, (int Year, int Week)? from, (int Year, int Week)? to, CancellationToken cancellationToken)
    {
        var query = _dbContext.StudentStudyProjections
            .AsNoTracking()
            .Include(item => item.Items)
            .Where(item => item.StudentId == studentId);

        if (from is { } lower)
        {
            query = query.Where(item =>
                item.Year > lower.Year || (item.Year == lower.Year && item.WeekOfYear >= lower.Week));
        }

        if (to is { } upper)
        {
            query = query.Where(item =>
                item.Year < upper.Year || (item.Year == upper.Year && item.WeekOfYear <= upper.Week));
        }

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentPerformanceProjection>> ListStudentPerformanceAsync(
        Guid studentId,
        string? subject,
        (int Year, int Month)? from,
        (int Year, int Month)? to,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.StudentPerformanceProjections
            .AsNoTracking()
            .Include(item => item.Items)
            .Where(item => item.StudentId == studentId);

        if (subject is not null)
        {
            query = query.Where(item => item.Subject == subject);
        }

        if (from is { } lower)
        {
            query = query.Where(item =>
                item.Year > lower.Year || (item.Year == lower.Year && item.Month >= lower.Month));
        }

        if (to is { } upper)
        {
            query = query.Where(item =>
                item.Year < upper.Year || (item.Year == upper.Year && item.Month <= upper.Month));
        }

        return await query.ToArrayAsync(cancellationToken);
    }
}
