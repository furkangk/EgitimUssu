using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

public sealed class TeacherDashboardProjectionReader : ITeacherDashboardProjectionReader
{
    private readonly ReportingDbContext _dbContext;

    public TeacherDashboardProjectionReader(ReportingDbContext dbContext) => _dbContext = dbContext;

    public Task<TeacherDashboardProjection?> GetAsync(Guid teacherUserId, CancellationToken cancellationToken)
        => _dbContext.TeacherDashboardProjections
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.TeacherUserId == teacherUserId, cancellationToken);
}
