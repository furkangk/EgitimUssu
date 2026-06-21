using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

internal sealed class LessonReminderRepository : ILessonReminderRepository
{
    private readonly NotificationsDbContext _dbContext;

    public LessonReminderRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonReminder?> GetByLessonScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonReminders.FirstOrDefaultAsync(reminder => reminder.LessonScheduleId == lessonScheduleId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonReminder>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonReminders
            .Where(reminder => reminder.TeacherUserId == teacherUserId)
            .OrderBy(reminder => reminder.RemindAtUtc)
            .ThenBy(reminder => reminder.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonReminder>> ListDuePendingAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonReminders
            .Where(reminder => reminder.Status == ReminderStatus.Pending && reminder.RemindAtUtc <= utcNow)
            .OrderBy(reminder => reminder.RemindAtUtc)
            .ThenBy(reminder => reminder.CreatedOnUtc)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(LessonReminder reminder, CancellationToken cancellationToken)
    {
        return _dbContext.LessonReminders.AddAsync(reminder, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
