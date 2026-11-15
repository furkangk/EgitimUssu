using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// Scheduling → ders hatırlatması üretici/iptal edici. Replay koruması artık ortak inbox üzerinden
/// (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler); LessonScheduleId başına TEK
/// hatırlatma kuralı (unique index, <c>lesson_reminders</c>) gerçek bir iş kuralı olduğundan burada
/// korunur — mevcut davranış: reschedule (aynı LessonScheduleId için ikinci LessonScheduledDomainEvent)
/// güncelleme YAPMAZ, yalnız atlanır (önceki davranışla birebir).
/// </summary>
internal sealed class LessonScheduleNotificationIntegrationEventHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IIdGenerator _idGenerator;
    private readonly ILocalCalendar _calendar;

    public LessonScheduleNotificationIntegrationEventHandler(
        NotificationsDbContext dbContext,
        IIdGenerator idGenerator,
        IClock clock,
        ILocalCalendar calendar)
        : base(dbContext, clock)
    {
        _idGenerator = idGenerator;
        _calendar = calendar;
    }

    private NotificationsDbContext NotificationsDb => (NotificationsDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
    {
        return integrationEvent.SourceModule == "Scheduling"
            && (integrationEvent.Name == "LessonScheduledDomainEvent"
                || integrationEvent.Name == "LessonScheduleCancelledDomainEvent"
                || integrationEvent.Name == "LessonCancellationAppliedDomainEvent"
                || integrationEvent.Name == "LessonScheduleDeletedDomainEvent");
    }

    protected override Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (envelope.Name is "LessonCancellationAppliedDomainEvent" or "LessonScheduleDeletedDomainEvent")
            return ApplyScopeCancelledAsync(envelope, cancellationToken);
        return envelope.Name == "LessonScheduledDomainEvent"
            ? ApplyScheduledAsync(envelope, cancellationToken)
            : ApplyCancelledAsync(envelope, cancellationToken);
    }

    private async Task<bool> ApplyScheduledAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonScheduledEventPayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            return false;
        }

        // İş kuralı (unique index): LessonScheduleId başına tek hatırlatma. Zaten varsa yeniden üretilmez.
        var exists = await NotificationsDb.LessonReminders
            .AnyAsync(reminder => reminder.LessonScheduleId == payload.LessonScheduleId, cancellationToken);
        if (exists)
        {
            return false;
        }

        var reminder = new LessonReminder(
            _idGenerator.New(),
            payload.LessonScheduleId,
            payload.TeacherUserId ?? Guid.Empty,
            payload.StudentId,
            "Yaklasan ders hatirlatmasi",
            NotificationTimeText.LessonReminderBody(_calendar, payload.StartAtUtc),
            payload.StartAtUtc,
            payload.StartAtUtc.AddMinutes(-Math.Max(payload.ReminderOffsetMinutes, 0)),
            NotificationChannel.InApp,
            ReminderStatus.Pending,
            Clock.UtcNow);

        NotificationsDb.LessonReminders.Add(reminder);
        return true;
    }

    private async Task<bool> ApplyCancelledAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonScheduleCancelledEventPayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            return false;
        }

        var reminder = await NotificationsDb.LessonReminders
            .FirstOrDefaultAsync(r => r.LessonScheduleId == payload.LessonScheduleId, cancellationToken);
        if (reminder is null)
        {
            return false;
        }

        reminder.Cancel(Clock.UtcNow);
        return true;
    }

    private async Task<bool> ApplyScopeCancelledAsync(IntegrationEvent envelope, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<LessonCancellationApplied>(envelope.Payload, JsonOptions);
        if (payload is null) return false;
        var reminders = await NotificationsDb.LessonReminders.Where(r => r.LessonScheduleId == payload.LessonScheduleId && r.Status == ReminderStatus.Pending).ToArrayAsync(ct);
        var changed = false;
        foreach (var reminder in reminders)
        {
            var matches = envelope.Name == "LessonScheduleDeletedDomainEvent" || payload.Scope == "All" ||
                (payload.Scope == "Single" ? reminder.ScheduledLessonStartAtUtc == payload.EffectiveStartUtc : reminder.ScheduledLessonStartAtUtc >= payload.EffectiveStartUtc);
            if (!matches) continue;
            reminder.Cancel(Clock.UtcNow);
            changed = true;
        }
        return changed;
    }

    private sealed record LessonScheduledEventPayload(
        Guid LessonScheduleId,
        Guid? TeacherUserId,
        Guid StudentId,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        int ReminderOffsetMinutes,
        DateTime CreatedOnUtc);

    private sealed record LessonScheduleCancelledEventPayload(
        Guid LessonScheduleId,
        Guid? TeacherUserId,
        Guid StudentId,
        DateTime CancelledOnUtc);
}
