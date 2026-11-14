using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

internal sealed class LessonSessionRepository : ILessonSessionRepository
{
    private readonly LessonSessionsDbContext _dbContext;

    public LessonSessionRepository(LessonSessionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSessions.FindAsync(new object?[] { lessonSessionId }, cancellationToken).AsTask();
    }

    // K-20: Bir planlı dersin her orijinal oluşumundan en fazla bir oturum türetilir.
    public Task<LessonSession?> GetByOccurrenceAsync(Guid lessonScheduleId, DateTime occurrenceStartUtc, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSessions
            .Where(session => session.LessonScheduleId == lessonScheduleId && session.OccurrenceStartUtc == occurrenceStartUtc)
            .OrderBy(session => session.CreatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSession>> ListAsync(
        Guid? teacherUserId,
        Guid? studentId,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.LessonSessions.AsQueryable();

        if (teacherUserId.HasValue)
        {
            query = query.Where(x => x.TeacherUserId == teacherUserId.Value);
        }

        if (studentId.HasValue)
        {
            query = query.Where(x => x.StudentId == studentId.Value);
        }

        if (dateFromUtc.HasValue)
        {
            query = query.Where(x => x.PlannedStartAtUtc >= dateFromUtc.Value);
        }

        if (dateToUtc.HasValue)
        {
            query = query.Where(x => x.PlannedStartAtUtc <= dateToUtc.Value);
        }

        return await query
            .OrderBy(x => x.PlannedStartAtUtc)
            .ThenBy(x => x.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<LessonSession> GetOrCreateAsync(LessonSession lessonSession, CancellationToken cancellationToken)
    {
        if (lessonSession.LessonScheduleId is { } schedule && lessonSession.OccurrenceStartUtc is { } key)
        {
            var existing = await GetByOccurrenceAsync(schedule, key, cancellationToken);
            if (existing is not null) return existing;
        }

        await AddAsync(lessonSession, cancellationToken);
        try
        {
            await SaveChangesAsync(cancellationToken);
            return lessonSession;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_lesson_sessions_LessonScheduleId_OccurrenceStartUtc" }
            && lessonSession.LessonScheduleId.HasValue && lessonSession.OccurrenceStartUtc.HasValue)
        {
            // SaveChanges transaction'ı oturum ve outbox'ı birlikte geri aldı. Yalnız kazanan kaydı döndür.
            _dbContext.ChangeTracker.Clear();
            return await GetByOccurrenceAsync(lessonSession.LessonScheduleId.Value,
                lessonSession.OccurrenceStartUtc.Value, cancellationToken) ?? throw new InvalidOperationException("Oluşum oturumu bulunamadı.");
        }
    }

    public Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSessions.AddAsync(lessonSession, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
