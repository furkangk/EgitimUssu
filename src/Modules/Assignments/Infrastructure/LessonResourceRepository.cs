using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

internal sealed class LessonResourceRepository : ILessonResourceRepository
{
    private readonly AssignmentsDbContext _dbContext;

    public LessonResourceRepository(AssignmentsDbContext dbContext) => _dbContext = dbContext;

    public Task<LessonResource?> GetByIdAsync(Guid resourceId, CancellationToken cancellationToken)
        => _dbContext.LessonResources.FirstOrDefaultAsync(entity => entity.Id == resourceId, cancellationToken);

    /// <summary>
    /// Yalniz aktif (arsivlenmemis) kaynaklari doner. Ogrenci filtresi verildiginde, o ogrenciye ozel
    /// kaynaklar **ve** tum ogrencilere acik olanlar (StudentId = null) birlikte gelir.
    /// </summary>
    public async Task<IReadOnlyCollection<LessonResource>> ListAsync(
        Guid? teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.LessonResources.Where(entity => entity.IsActive);

        if (teacherUserId is not null)
        {
            query = query.Where(entity => entity.TeacherUserId == teacherUserId);
        }

        if (studentId is not null)
        {
            query = query.Where(entity => entity.StudentId == studentId || entity.StudentId == null);
        }

        if (lessonSessionId is not null)
        {
            query = query.Where(entity => entity.LessonSessionId == lessonSessionId);
        }

        return await query.OrderByDescending(entity => entity.CreatedOnUtc).ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(LessonResource resource, CancellationToken cancellationToken)
        => _dbContext.LessonResources.AddAsync(resource, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
