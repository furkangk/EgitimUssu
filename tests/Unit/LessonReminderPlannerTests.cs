using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonReminderPlannerTests
{
    public static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Planner_creates_one_reminder_per_occurrence_and_distinct_recipient()
    {
        await using var db = Db();
        var reader = new Reader();
        var occurrence = Occurrence(Now.AddHours(2));
        reader.Items.Add(occurrence);
        var students = new Students();
        var parents = new Parents();
        var approved = Guid.NewGuid();
        parents.Items.Add(Target(approved, true));
        parents.Items.Add(Target(Guid.NewGuid(), false));
        parents.Items.Add(Target(approved, true));
        var clock = new Clock();
        var processor = Processor(db, reader, students, parents, clock);
        await processor.RunAsync();
        await processor.RunAsync();
        var reminders = await db.LessonReminders.ToArrayAsync();
        Assert.Equal(3, reminders.Length);
        Assert.Equal(new[] { occurrence.TeacherUserId!.Value, students.Owner, approved }.Order(), reminders.Select(r => r.RecipientUserId).Order());
        Assert.All(reminders, r => Assert.Equal(occurrence.OccurrenceStartUtc, r.OccurrenceStartUtc));
        Assert.All(reminders, r => Assert.Contains("15:00", r.Message));
    }

    [Fact]
    public async Task Rescheduling_replaces_old_trigger_and_text_but_preserves_original_key()
    {
        await using var db = Db();
        var reader = new Reader();
        var occurrence = Occurrence(Now.AddHours(2));
        reader.Items.Add(occurrence);
        var processor = Processor(db, reader);
        await processor.RunAsync();
        var reminder = await db.LessonReminders.SingleAsync();
        reminder.MarkFailed("503", Now);
        reader.Items[0] = occurrence with { StartAtUtc = occurrence.StartAtUtc.AddHours(3) };
        await processor.RunAsync();
        Assert.Single(await db.LessonReminders.ToArrayAsync());
        Assert.Equal(occurrence.OccurrenceStartUtc, reminder.OccurrenceStartUtc);
        Assert.Equal(Now.AddHours(5).AddMinutes(-15), reminder.RemindAtUtc);
        Assert.Contains("18:00", reminder.Message);
        Assert.Equal(0, reminder.AttemptCount);
        Assert.Null(reminder.LastError);
    }

    [Fact]
    public async Task Removed_occurrence_cancels_all_pending_recipients_and_replay_cannot_revive_it()
    {
        await using var db = Db();
        var reader = new Reader();
        reader.Items.Add(Occurrence(Now.AddHours(2)));
        var processor = Processor(db, reader, new Students());
        await processor.RunAsync();
        reader.Items.Clear();
        await processor.RunAsync();
        await processor.RunAsync();
        Assert.Equal(2, await db.LessonReminders.CountAsync());
        Assert.All(await db.LessonReminders.ToArrayAsync(), r => Assert.Equal(ReminderStatus.Cancelled, r.Status));
    }

    [Fact]
    public async Task Rolling_window_produces_three_weekly_occurrences_without_resending_sent_ones()
    {
        await using var db = Db();
        var reader = new Reader();
        var first = Occurrence(Now.AddHours(2));
        for (var week = 0; week < 3; week++) reader.Items.Add(first with
        {
            OccurrenceStartUtc = first.OccurrenceStartUtc.AddDays(7 * week), StartAtUtc = first.StartAtUtc.AddDays(7 * week)
        });
        var clock = new Clock();
        var processor = Processor(db, reader, clock: clock);
        for (var week = 0; week < 3; week++)
        {
            clock.UtcNow = Now.AddDays(7 * week);
            await processor.RunAsync();
            var reminder = await db.LessonReminders.SingleAsync(r => r.OccurrenceStartUtc == first.StartAtUtc.AddDays(7 * week));
            reminder.MarkSent(clock.UtcNow);
            await db.SaveChangesAsync();
            await processor.RunAsync();
            Assert.Equal(ReminderStatus.Sent, reminder.Status);
        }
        Assert.Equal(3, await db.LessonReminders.CountAsync());
    }

    [Fact]
    public async Task Past_and_outside_window_occurrences_do_not_create_reminders()
    {
        await using var db = Db();
        var reader = new Reader();
        reader.Items.Add(Occurrence(Now.AddHours(-1)));
        reader.Items.Add(Occurrence(Now.AddDays(8)));
        await Processor(db, reader).RunAsync();
        Assert.Empty(await db.LessonReminders.ToArrayAsync());
    }

    [Fact]
    public async Task Disabled_offset_does_not_plan_and_self_lesson_uses_student_user()
    {
        await using var db = Db();
        var reader = new Reader();
        var self = Occurrence(Now.AddHours(2)) with { TeacherUserId = null };
        reader.Items.Add(self);
        reader.Items.Add(Occurrence(Now.AddHours(3)) with { ReminderOffsetMinutes = 0 });
        var students = new Students();
        await Processor(db, reader, students).RunAsync();
        var reminder = Assert.Single(await db.LessonReminders.ToArrayAsync());
        Assert.Equal(students.Owner, reminder.RecipientUserId);
        Assert.Equal(Guid.Empty, reminder.TeacherUserId);
    }

    [Fact]
    public void Expired_reminder_and_replan_reset_delivery_state()
    {
        var r = new LessonReminder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ders", "Eski", Now.AddHours(1), Now.AddMinutes(-31), NotificationChannel.Push, ReminderStatus.Pending, Now);
        r.Expire(Now);
        Assert.Equal(ReminderStatus.Expired, r.Status);
        r.Replan(Now.AddHours(3), Now.AddHours(2), "Yeni", Now);
        Assert.Equal(ReminderStatus.Pending, r.Status);
        Assert.Equal("Yeni", r.Message);
    }

    public static LessonReminderPlanningProcessor Processor(NotificationsDbContext db, Reader reader, Students? students = null, Parents? parents = null, Clock? clock = null)
        => new(db, reader, students ?? new Students { Owner = Guid.Empty }, parents ?? new Parents(), new Ids(), clock ?? new Clock(), new LocalCalendar("Europe/Istanbul", clock ?? new Clock()));
    public static NotificationsDbContext Db() => new(new DbContextOptionsBuilder<NotificationsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new NoOpDomainEventMapper());
    public static ReminderOccurrence Occurrence(DateTime start) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, start, 15);
    public static ParentNotificationTarget Target(Guid id, bool enabled) => new(id, MembershipTier.Free, new(true, true, enabled, true, true));
    public sealed class Clock : IClock { public DateTime UtcNow { get; set; } = Now; }
    public sealed class Ids : IIdGenerator { public Guid New() => Guid.NewGuid(); }
    public sealed class Reader : ILessonScheduleReader
    {
        public List<ReminderOccurrence> Items { get; } = [];
        public Task<IReadOnlyCollection<ReminderOccurrence>> ListReminderOccurrencesAsync(DateTime fromUtc, DateTime toUtc, Guid? lessonScheduleId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ReminderOccurrence>>(Items.Where(i => lessonScheduleId == null || i.LessonScheduleId == lessonScheduleId).ToArray());
    }
    public sealed class Parents : IParentNotificationDirectory
    {
        public List<ParentNotificationTarget> Items { get; } = [];
        public Task<ParentNotificationTargetPage> ListApprovedTargetPageAsync(Guid? afterLinkId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new ParentNotificationTargetPage(Array.Empty<ParentStudentNotificationTarget>(), null));

        public Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentNotificationTarget>>(Items);
        public Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentStudentNotificationTarget>>(Array.Empty<ParentStudentNotificationTarget>());
    }
    public sealed class Students : IStudentDirectory
    {
        public Guid Owner { get; set; } = Guid.NewGuid();
        public Task<Guid?> GetOwnerUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Guid?>(Owner == Guid.Empty ? null : Owner);
        public Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
        public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacher, Guid id, CancellationToken ct) => Task.FromResult<AgreedLessonRate?>(null);
        public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacher, Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task<string?> GetDisplayNameAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
