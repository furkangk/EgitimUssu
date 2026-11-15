using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

internal sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly AssignmentsDbContext _dbContext;

    public AssignmentRepository(AssignmentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Assignment?> GetAssignmentByIdAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        return _dbContext.Assignments.FindAsync(new object?[] { assignmentId }, cancellationToken).AsTask();
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
        CancellationToken cancellationToken, EgitimUssu.Shared.Kernel.KeysetPage? page = null)
    {
        var query = _dbContext.Assignments.AsNoTracking();

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

        if (page is not null)
            return await query.Seek(page, x => x.CreatedOnUtc, x => x.Id, _dbContext.Database.IsNpgsql()).ToArrayAsync(cancellationToken);

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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_assignment_submission_versions" })
        {
            // EF çocuğu parent xmin UPDATE'inden önce ekleyebilir: aynı sürüm yarışı da 409'dur.
            throw new DbUpdateConcurrencyException("Ödev teslimi başka bir istek tarafından değiştirildi.", ex);
        }
    }
}
