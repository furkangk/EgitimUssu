using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

internal sealed class LessonScheduleNotificationIntegrationEventHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILessonReminderRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public LessonScheduleNotificationIntegrationEventHandler(
        ILessonReminderRepository repository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public bool CanHandle(IIntegrationEvent integrationEvent)
    {
        return integrationEvent.SourceModule == "Scheduling"
            && (integrationEvent.Name == "LessonScheduledDomainEvent"
                || integrationEvent.Name == "LessonScheduleCancelledDomainEvent");
    }

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent eventEnvelope)
        {
            return;
        }

        if (eventEnvelope.Name == "LessonScheduledDomainEvent")
        {
            await HandleScheduledAsync(eventEnvelope, cancellationToken);
            return;
        }

        if (eventEnvelope.Name == "LessonScheduleCancelledDomainEvent")
        {
            await HandleCancelledAsync(eventEnvelope, cancellationToken);
        }
    }

    private async Task HandleScheduledAsync(IntegrationEvent eventEnvelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonScheduledEventPayload>(eventEnvelope.Payload, JsonOptions);
        if (payload is null)
        {
            return;
        }

        var existingReminder = await _repository.GetByLessonScheduleIdAsync(payload.LessonScheduleId, cancellationToken);
        if (existingReminder is not null)
        {
            return;
        }

        var reminder = new LessonReminder(
            _idGenerator.New(),
            payload.LessonScheduleId,
            payload.TeacherUserId,
            payload.StudentId,
            "Yaklasan ders hatirlatmasi",
            $"Ders {payload.StartAtUtc:O} tarihinde baslayacak.",
            payload.StartAtUtc,
            payload.StartAtUtc.AddMinutes(-60),
            NotificationChannel.InApp,
            ReminderStatus.Pending,
            _clock.UtcNow);

        await _repository.AddAsync(reminder, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleCancelledAsync(IntegrationEvent eventEnvelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonScheduleCancelledEventPayload>(eventEnvelope.Payload, JsonOptions);
        if (payload is null)
        {
            return;
        }

        var reminder = await _repository.GetByLessonScheduleIdAsync(payload.LessonScheduleId, cancellationToken);
        if (reminder is null)
        {
            return;
        }

        reminder.Cancel(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private sealed record LessonScheduledEventPayload(
        Guid LessonScheduleId,
        Guid TeacherUserId,
        Guid StudentId,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        DateTime CreatedOnUtc);

    private sealed record LessonScheduleCancelledEventPayload(
        Guid LessonScheduleId,
        Guid TeacherUserId,
        Guid StudentId,
        DateTime CancelledOnUtc);
}
