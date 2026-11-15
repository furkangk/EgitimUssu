using Microsoft.EntityFrameworkCore;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

public sealed class LessonReminderPlanningProcessor(
    NotificationsDbContext db, ILessonScheduleReader reader, IStudentDirectory students,
    IParentNotificationDirectory parents, IIdGenerator ids, IClock clock, ILocalCalendar calendar)
{
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var count = 0;
        var attempt = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            await ReconcileAsync(null, ct);
            count = await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        });
        return count;
    }

    // Olay tüketicisi kendi inbox transaction'ında çağırır; burada SaveChanges yoktur.
    public async Task ReconcileAsync(Guid? lessonScheduleId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('notification-reminder-plan'))", ct);
        var now = clock.UtcNow;
        var occurrences = await reader.ListReminderOccurrencesAsync(now, now.AddDays(7), lessonScheduleId, ct);
        var valid = occurrences.Where(o => o.StartAtUtc > now && o.StartAtUtc <= now.AddDays(7) && o.ReminderOffsetMinutes > 0).ToArray();
        var scheduleIds = valid.Select(o => o.LessonScheduleId).Distinct().ToArray();
        var existing = await db.LessonReminders.Where(r => lessonScheduleId.HasValue
            ? r.LessonScheduleId == lessonScheduleId.Value
            : r.Status == ReminderStatus.Pending || scheduleIds.Contains(r.LessonScheduleId)).ToArrayAsync(ct);
        var byKey = existing.ToDictionary(r => (r.LessonScheduleId, r.OccurrenceStartUtc, r.RecipientUserId));
        var desired = new HashSet<(Guid, DateTime, Guid)>();
        foreach (var occurrence in valid)
        {
            var recipients = new HashSet<Guid>();
            if (occurrence.TeacherUserId is { } teacher && teacher != Guid.Empty) recipients.Add(teacher);
            if (await students.GetOwnerUserIdAsync(occurrence.StudentId, ct) is { } owner && owner != Guid.Empty) recipients.Add(owner);
            var targets = await parents.GetApprovedParentsForStudentAsync(occurrence.StudentId, ct);
            foreach (var target in targets.Where(t => t.Prefs.LessonReminders)) recipients.Add(target.ParentUserId);
            foreach (var recipient in recipients.Where(r => r != Guid.Empty))
            {
                var key = (occurrence.LessonScheduleId, occurrence.OccurrenceStartUtc, recipient);
                desired.Add(key);
                var remind = occurrence.StartAtUtc.AddMinutes(-Math.Max(occurrence.ReminderOffsetMinutes, 0));
                var message = NotificationTimeText.LessonReminderBody(calendar, occurrence.StartAtUtc);
                if (byKey.TryGetValue(key, out var reminder))
                {
                    if (reminder.ScheduledLessonStartAtUtc != occurrence.StartAtUtc || reminder.RemindAtUtc != remind || reminder.Status == ReminderStatus.Cancelled)
                        reminder.Replan(occurrence.StartAtUtc, remind, message, now);
                }
                else
                {
                    reminder = new LessonReminder(ids.New(), occurrence.LessonScheduleId,
                        occurrence.TeacherUserId ?? Guid.Empty, occurrence.StudentId, "Yaklaşan ders hatırlatması", message,
                        occurrence.StartAtUtc, remind, NotificationChannel.Push, ReminderStatus.Pending, now,
                        occurrence.OccurrenceStartUtc, recipient);
                    db.LessonReminders.Add(reminder);
                    byKey.Add(key, reminder);
                }
            }
        }
        foreach (var reminder in existing.Where(r => r.Status == ReminderStatus.Pending))
        {
            if (reminder.ScheduledLessonStartAtUtc <= now) reminder.Expire(now);
            else if (!desired.Contains((reminder.LessonScheduleId, reminder.OccurrenceStartUtc, reminder.RecipientUserId))) reminder.Cancel(now);
        }
    }
}

internal sealed class LessonReminderPlanner(IServiceScopeFactory scopeFactory, ILogger<LessonReminderPlanner> logger)
    : LeasedBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "lesson-reminder-planning";
    protected override TimeSpan PollInterval => TimeSpan.FromMinutes(1);
    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<NotificationsDbContext>();
    protected override Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetRequiredService<LessonReminderPlanningProcessor>().RunAsync(cancellationToken);
}
