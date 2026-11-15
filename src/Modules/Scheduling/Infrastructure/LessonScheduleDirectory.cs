using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

/// <summary>
/// M05-2: LessonSessions'ın planlı dersten oturum türetebilmesi için salt-okunur erişim.
/// Öğretmensiz (self, Ç-06) dersler dönmez — onlardan oturum türetilemez.
/// </summary>
internal sealed class LessonScheduleDirectory : ILessonScheduleDirectory, ILessonScheduleReader, ITeacherDashboardLessonSource
{
    private readonly SchedulingDbContext _dbContext;
    private readonly ILessonScheduleRepository _lessons;
    private readonly ITimeOffBlockRepository _timeOff;
    private readonly ILocalCalendar _calendar;

    public LessonScheduleDirectory(
        SchedulingDbContext dbContext,
        ILessonScheduleRepository lessons,
        ITimeOffBlockRepository timeOff,
        ILocalCalendar calendar)
    {
        _dbContext = dbContext;
        _lessons = lessons;
        _timeOff = timeOff;
        _calendar = calendar;
    }

    public async Task<IReadOnlyCollection<DashboardLessonOccurrence>> ListDashboardOccurrencesAsync(
        DateTime fromUtc, DateTime toUtc, Guid? lessonScheduleId, CancellationToken cancellationToken)
    {
        var query = _dbContext.LessonSchedules.AsNoTracking().Where(l => l.TeacherUserId != null &&
            l.Status != LessonScheduleStatus.Draft);
        if (lessonScheduleId.HasValue) query = query.Where(l => l.Id == lessonScheduleId);
        var lessons = await query.Where(l => l.StartAtUtc < toUtc ||
            _dbContext.LessonOccurrenceExceptions.Any(e => e.SeriesLessonScheduleId == l.Id &&
                e.OverrideStartAtUtc < toUtc)).ToArrayAsync(cancellationToken);
        var ids = lessons.Select(l => l.Id).ToArray();
        var exceptions = await _dbContext.LessonOccurrenceExceptions.AsNoTracking()
            .Where(e => ids.Contains(e.SeriesLessonScheduleId)).ToArrayAsync(cancellationToken);
        var byLesson = exceptions.ToLookup(e => e.SeriesLessonScheduleId);
        var result = new List<DashboardLessonOccurrence>();
        foreach (var lesson in lessons)
        {
            var overrides = byLesson[lesson.Id].Select(e => new OccurrenceOverride(e.OriginalStartAtUtc,
                e.Action, e.OverrideStartAtUtc, e.OverrideEndAtUtc)).ToArray();
            foreach (var occurrence in RecurrenceExpander.Expand(lesson.StartAtUtc, lesson.EndAtUtc,
                lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone), fromUtc, toUtc, overrides))
            {
                if (occurrence.StartAtUtc < fromUtc || occurrence.StartAtUtc >= toUtc) continue;
                result.Add(new(lesson.TeacherUserId!.Value, lesson.Id, occurrence.RecurrenceIdUtc,
                    occurrence.StartAtUtc, lesson.Status == LessonScheduleStatus.Cancelled || occurrence.IsCancelled));
            }
        }
        return result;
    }

    public async Task<IReadOnlyCollection<ReminderOccurrence>> ListReminderOccurrencesAsync(
        DateTime fromUtc, DateTime toUtc, Guid? lessonScheduleId, CancellationToken cancellationToken)
    {
        var query = _dbContext.LessonSchedules.AsNoTracking().Where(l => l.Status == LessonScheduleStatus.Planned);
        if (lessonScheduleId.HasValue) query = query.Where(l => l.Id == lessonScheduleId.Value);
        var lessons = await query.Where(l => l.StartAtUtc <= toUtc ||
            _dbContext.LessonOccurrenceExceptions.Any(e => e.SeriesLessonScheduleId == l.Id &&
                e.Action == OccurrenceExceptionAction.Rescheduled && e.OverrideStartAtUtc <= toUtc))
            .ToArrayAsync(cancellationToken);
        var ids = lessons.Select(l => l.Id).ToArray();
        var exceptions = await _dbContext.LessonOccurrenceExceptions.AsNoTracking()
            .Where(e => ids.Contains(e.SeriesLessonScheduleId)).ToArrayAsync(cancellationToken);
        var byLesson = exceptions.ToLookup(e => e.SeriesLessonScheduleId);
        var result = new List<ReminderOccurrence>();
        foreach (var lesson in lessons)
        {
            var overrides = byLesson[lesson.Id].Select(e => new OccurrenceOverride(e.OriginalStartAtUtc,
                e.Action, e.OverrideStartAtUtc, e.OverrideEndAtUtc)).ToArray();
            foreach (var occurrence in RecurrenceExpander.Expand(lesson.StartAtUtc, lesson.EndAtUtc,
                lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone), fromUtc, toUtc, overrides))
            {
                if (occurrence.IsCancelled || occurrence.IsCompleted || occurrence.StartAtUtc <= fromUtc || occurrence.StartAtUtc > toUtc) continue;
                result.Add(new(lesson.Id, lesson.TeacherUserId, lesson.StudentId,
                    occurrence.RecurrenceIdUtc, occurrence.StartAtUtc, lesson.ReminderOffsetMinutes));
            }
        }
        return result;
    }

    public async Task<LessonScheduleOccurrence?> GetOccurrenceAsync(Guid lessonScheduleId, DateTime occurrenceStartUtc, CancellationToken cancellationToken)
    {
        var lesson = await _dbContext.LessonSchedules.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == lessonScheduleId && item.TeacherUserId != null, cancellationToken);
        if (lesson is null || !RecurrenceExpander.IsOccurrence(lesson.StartAtUtc, lesson.EndAtUtc,
            lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone), occurrenceStartUtc)) return null;

        var exception = await _dbContext.LessonOccurrenceExceptions.AsNoTracking().FirstOrDefaultAsync(
            item => item.SeriesLessonScheduleId == lessonScheduleId && item.OriginalStartAtUtc == occurrenceStartUtc, cancellationToken);
        var start = exception?.OverrideStartAtUtc ?? occurrenceStartUtc;
        var end = exception?.OverrideEndAtUtc ?? start.Add(lesson.EndAtUtc - lesson.StartAtUtc);
        return new(occurrenceStartUtc, start, end,
            exception?.Action is OccurrenceExceptionAction.Cancelled or OccurrenceExceptionAction.Skipped);
    }

    public async Task<LessonScheduleDetails?> GetAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
        => await _dbContext.LessonSchedules
            .Where(lesson => lesson.Id == lessonScheduleId && lesson.TeacherUserId != null)
            .Select(lesson => new LessonScheduleDetails(
                lesson.Id,
                lesson.TeacherUserId!.Value,
                lesson.StudentId,
                lesson.StartAtUtc,
                lesson.EndAtUtc,
                lesson.Subject,
                lesson.MeetingUrl,
                lesson.Status.ToString(), lesson.RecurrenceRule != null && lesson.RecurrenceRule != ""))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// M14-5: Öğretmenin dolu aralıkları. Tekrar eden seriler <see cref="RecurrenceExpander"/> ile açılır ve
    /// occurrence istisnaları (iptal/atlama/erteleme) uygulanır — öğretmen takvimi ucuyla <b>aynı</b> yol
    /// (Öğrenilenler #67: var olan tek-sorgulu yükleme deseni kopyalanmaz, yeniden kullanılır).
    /// İzin/tatil blokları da doluluk sayılır: öğretmen tatildeyken "boş" görünmemeli.
    /// </summary>
    public async Task<IReadOnlyCollection<TeacherBusyInterval>> ListTeacherBusyIntervalsAsync(
        Guid teacherUserId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var busy = new List<TeacherBusyInterval>();

        var lessons = await _lessons.ListActiveForTeacherUntilAsync(teacherUserId, toUtc, cancellationToken);
        var exceptions = await _lessons.ListExceptionsForTeacherAsync(teacherUserId, cancellationToken);
        var exceptionsByLesson = exceptions
            .GroupBy(item => item.SeriesLessonScheduleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<OccurrenceOverride>)group
                    .Select(item => new OccurrenceOverride(
                        item.OriginalStartAtUtc, item.Action, item.OverrideStartAtUtc, item.OverrideEndAtUtc))
                    .ToArray());

        foreach (var lesson in lessons)
        {
            var overrides = exceptionsByLesson.TryGetValue(lesson.Id, out var found)
                ? found
                : Array.Empty<OccurrenceOverride>();

            // P16 T2 / N-04: seri kendi diliminin yerel takviminde genişletilir.
            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone),
                fromUtc, toUtc, overrides))
            {
                if (occurrence.IsCancelled)
                {
                    continue;
                }

                busy.Add(new TeacherBusyInterval(occurrence.StartAtUtc, occurrence.EndAtUtc, "Lesson"));
            }
        }

        var blocks = await _timeOff.ListForTeacherAsync(teacherUserId, fromUtc, toUtc, cancellationToken);
        busy.AddRange(blocks.Select(block => new TeacherBusyInterval(block.StartAtUtc, block.EndAtUtc, "TimeOff")));

        return busy;
    }
}
