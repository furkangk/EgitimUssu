using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

internal sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly AssignmentsDbContext _dbContext;

    public AssignmentRepository(AssignmentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonNote?> GetLessonNoteByLessonSessionIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonNotes.FirstOrDefaultAsync(note => note.LessonSessionId == lessonSessionId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Assignment>> ListByLessonSessionIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
    {
        return await _dbContext.Assignments
            .Where(assignment => assignment.LessonSessionId == lessonSessionId)
            .OrderBy(assignment => assignment.DueDateUtc ?? DateTime.MaxValue)
            .ThenBy(assignment => assignment.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Assignment>> ListAsync(
        Guid? teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Assignments.AsQueryable();

        if (teacherUserId.HasValue)
        {
            query = query.Where(x => x.TeacherUserId == teacherUserId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(x => x.StudentId == studentId.Value);
        }

        if (lessonSessionId.HasValue)
        {
            query = query.Where(x => x.LessonSessionId == lessonSessionId.Value);
        }

        return await query
            .OrderBy(x => x.DueDateUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddLessonNoteAsync(LessonNote lessonNote, CancellationToken cancellationToken)
    {
        return _dbContext.LessonNotes.AddAsync(lessonNote, cancellationToken).AsTask();
    }

    public Task AddAssignmentsAsync(IEnumerable<Assignment> assignments, CancellationToken cancellationToken)
    {
        return _dbContext.Assignments.AddRangeAsync(assignments, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
