using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

internal sealed class TeacherStudentLinkRepository : ITeacherStudentLinkRepository
{
    private readonly StudentsDbContext _dbContext;

    public TeacherStudentLinkRepository(StudentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(TeacherStudentLink link, CancellationToken cancellationToken)
    {
        return _dbContext.TeacherStudentLinks.AddAsync(link, cancellationToken).AsTask();
    }

    public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken cancellationToken)
    {
        return _dbContext.TeacherStudentLinks.FirstOrDefaultAsync(link => link.Id == linkId, cancellationToken);
    }

    public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
    {
        return _dbContext.TeacherStudentLinks.FirstOrDefaultAsync(
            link => link.TeacherUserId == teacherUserId && link.StudentId == studentId,
            cancellationToken);
    }

    public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
    {
        return _dbContext.TeacherStudentLinks.CountAsync(
            link => link.TeacherUserId == teacherUserId && link.Status != TeacherStudentLinkStatus.Rejected,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken cancellationToken)
    {
        return await _dbContext.TeacherStudentLinks
            .Where(link => link.TeacherUserId == teacherUserId
                && link.Status != TeacherStudentLinkStatus.Rejected
                && (includeArchived || !link.IsArchived))
            .ToArrayAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
