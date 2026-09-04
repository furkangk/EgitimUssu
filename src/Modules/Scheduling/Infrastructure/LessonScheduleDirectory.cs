using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

/// <summary>
/// M05-2: LessonSessions'ın planlı dersten oturum türetebilmesi için salt-okunur erişim.
/// Öğretmensiz (self, Ç-06) dersler dönmez — onlardan oturum türetilemez.
/// </summary>
internal sealed class LessonScheduleDirectory : ILessonScheduleDirectory
{
    private readonly SchedulingDbContext _dbContext;

    public LessonScheduleDirectory(SchedulingDbContext dbContext) => _dbContext = dbContext;

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
                lesson.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);
}
