using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Infrastructure;

/// <summary>
/// Öğrencinin kendi program girdisi (`StudyScheduleEntry`) için hatırlatma planlar/iptal eder.
/// Scheduling modülünden gelen domain event'lerini (outbox → integration event) tüketir; Scheduling'e
/// proje referansı vermez — olay adı + JSON payload üzerinden çalışır (modül izolasyonu).
///
/// Hatırlatma kaydı, öğretmen dersleriyle aynı `LessonReminder` aggregate'ında tutulur: girdinin kimliği
/// `LessonScheduleId` alanına (tekil), öğrenci `StudentId`'ye yazılır; `TeacherUserId` boştur (öğretmen yok).
/// Tekrarlı girdilerde hatırlatma **ilk oluşuma** göre planlanır (öğretmen dersleriyle aynı MVP davranışı).
/// </summary>
internal sealed class StudyScheduleReminderIntegrationEventHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILessonReminderRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public StudyScheduleReminderIntegrationEventHandler(
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
            && (integrationEvent.Name == "StudyScheduleEntryScheduledDomainEvent"
                || integrationEvent.Name == "StudyScheduleEntryRescheduledDomainEvent"
                || integrationEvent.Name == "StudyScheduleEntryCancelledDomainEvent");
    }

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        switch (envelope.Name)
        {
            case "StudyScheduleEntryScheduledDomainEvent":
                await HandleScheduledAsync(envelope, cancellationToken);
                break;
            case "StudyScheduleEntryRescheduledDomainEvent":
                await HandleRescheduledAsync(envelope, cancellationToken);
                break;
            case "StudyScheduleEntryCancelledDomainEvent":
                await HandleCancelledAsync(envelope, cancellationToken);
                break;
        }
    }

    private async Task HandleScheduledAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ScheduledPayload>(envelope.Payload, JsonOptions);
        if (payload is null || payload.ReminderOffsetMinutes <= 0)
        {
            // Hatırlatma kapalı (0 dk) → kayıt oluşturulmaz.
            return;
        }

        var existing = await _repository.GetByLessonScheduleIdAsync(payload.StudyScheduleEntryId, cancellationToken);
        if (existing is not null)
        {
            return; // idempotent
        }

        var reminder = new LessonReminder(
            _idGenerator.New(),
            payload.StudyScheduleEntryId,
            Guid.Empty,
            payload.StudentId,
            "Calisma hatirlatmasi",
            $"{payload.Subject} dersin {payload.StartAtUtc:O} tarihinde basliyor.",
            payload.StartAtUtc,
            payload.StartAtUtc.AddMinutes(-payload.ReminderOffsetMinutes),
            NotificationChannel.InApp,
            ReminderStatus.Pending,
            _clock.UtcNow);

        await _repository.AddAsync(reminder, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleRescheduledAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<RescheduledPayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            return;
        }

        var existing = await _repository.GetByLessonScheduleIdAsync(payload.StudyScheduleEntryId, cancellationToken);

        if (payload.ReminderOffsetMinutes <= 0)
        {
            // Hatırlatma kapatıldıysa mevcut kaydı iptal et.
            if (existing is not null)
            {
                existing.Cancel(_clock.UtcNow);
                await _repository.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var remindAtUtc = payload.StartAtUtc.AddMinutes(-payload.ReminderOffsetMinutes);
        if (existing is null)
        {
            var reminder = new LessonReminder(
                _idGenerator.New(),
                payload.StudyScheduleEntryId,
                Guid.Empty,
                payload.StudentId,
                "Calisma hatirlatmasi",
                $"{payload.Subject} dersin {payload.StartAtUtc:O} tarihinde basliyor.",
                payload.StartAtUtc,
                remindAtUtc,
                NotificationChannel.InApp,
                ReminderStatus.Pending,
                _clock.UtcNow);
            await _repository.AddAsync(reminder, cancellationToken);
        }
        else
        {
            existing.Reschedule(payload.StartAtUtc, remindAtUtc, _clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleCancelledAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<CancelledPayload>(envelope.Payload, JsonOptions);
        if (payload is null)
        {
            return;
        }

        var existing = await _repository.GetByLessonScheduleIdAsync(payload.StudyScheduleEntryId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Cancel(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private sealed record ScheduledPayload(
        Guid StudyScheduleEntryId,
        Guid StudentId,
        string Subject,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        int ReminderOffsetMinutes,
        DateTime CreatedOnUtc);

    private sealed record RescheduledPayload(
        Guid StudyScheduleEntryId,
        Guid StudentId,
        string Subject,
        DateTime StartAtUtc,
        DateTime EndAtUtc,
        int ReminderOffsetMinutes,
        DateTime UpdatedOnUtc);

    private sealed record CancelledPayload(
        Guid StudyScheduleEntryId,
        Guid StudentId,
        DateTime CancelledOnUtc);
}
