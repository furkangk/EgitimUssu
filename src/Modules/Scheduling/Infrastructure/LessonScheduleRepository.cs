using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class LessonScheduleRepository : ILessonScheduleRepository
{
    private readonly SchedulingDbContext _dbContext;

    public LessonScheduleRepository(SchedulingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);
    }

    public Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.AnyAsync(
            lesson => lesson.TeacherUserId == teacherUserId
                && lesson.Status != LessonScheduleStatus.Cancelled
                && (excludeLessonId == null || lesson.Id != excludeLessonId)
                && lesson.StartAtUtc < endAtUtc
                && lesson.EndAtUtc > startAtUtc,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.TeacherUserId == teacherUserId
                && lesson.StartAtUtc >= startAtUtc
                && lesson.StartAtUtc <= endAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
    {
        // Ç-06: bu liste öğretmen derslerini döndürür (öğrencinin kendi dersleri /calendar'da gelir).
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.StudentId == studentId
                && lesson.TeacherUserId != null
                && lesson.StartAtUtc >= startAtUtc
                && lesson.StartAtUtc <= endAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.StudentId == studentId
                && lesson.Status != LessonScheduleStatus.Cancelled
                && lesson.StartAtUtc <= untilUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.AddAsync(lessonSchedule, cancellationToken).AsTask();
    }

    public void Remove(LessonSchedule lessonSchedule)
    {
        _dbContext.LessonSchedules.Remove(lessonSchedule);
    }

    public Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
        => _dbContext.LessonOccurrenceExceptions.AddAsync(occurrenceException, cancellationToken).AsTask();

    public async Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
        => await _dbContext.LessonOccurrenceExceptions
            .Where(x => x.SeriesLessonScheduleId == seriesLessonScheduleId)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
        => await (from x in _dbContext.LessonOccurrenceExceptions
                  join l in _dbContext.LessonSchedules on x.SeriesLessonScheduleId equals l.Id
                  where l.TeacherUserId == teacherUserId
                  select x).ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
