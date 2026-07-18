using System.Text.Json;
using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

/// <summary>
/// M08 <c>StudySessionCompletedDomainEvent</c> → ilgili ders+konu hâkimiyetine çalışma süresi ekler.
/// Idempotent: aynı olay iki kez işlenmez (<see cref="ProcessedEvent"/>).
/// </summary>
internal sealed class StudySessionCompletedProgressHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly IProgressRepository _repository;
    private readonly MasteryService _masteryService;
    private readonly IClock _clock;

    public StudySessionCompletedProgressHandler(IProgressRepository repository, MasteryService masteryService, IClock clock)
    {
        _repository = repository;
        _masteryService = masteryService;
        _clock = clock;
    }

    public bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "Study" && integrationEvent.Name == "StudySessionCompletedDomainEvent";

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        if (await _repository.HasProcessedAsync(envelope.EventId, cancellationToken))
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<StudySessionCompletedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Subject) || payload.EffectiveMinutes <= 0)
        {
            await MarkProcessedAsync(envelope.EventId, cancellationToken);
            return;
        }

        await _masteryService.ApplyStudyAsync(payload.StudentId, payload.Subject, payload.Topic, payload.EffectiveMinutes, cancellationToken);
        await MarkProcessedAsync(envelope.EventId, cancellationToken);
    }

    private async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await _repository.AddProcessedAsync(new ProcessedEvent(eventId, _clock.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private sealed record StudySessionCompletedPayload(
        Guid SessionId,
        Guid StudentId,
        string Subject,
        string? Topic,
        int EffectiveMinutes,
        int BreakMinutes,
        DateTime EndedAtUtc);
}

/// <summary>
/// M08 <c>TestResultRecordedDomainEvent</c> → ilgili ders+konu hâkimiyetine net oranını işler. Idempotent.
/// </summary>
internal sealed class TestResultRecordedProgressHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly IProgressRepository _repository;
    private readonly MasteryService _masteryService;
    private readonly IClock _clock;

    public TestResultRecordedProgressHandler(IProgressRepository repository, MasteryService masteryService, IClock clock)
    {
        _repository = repository;
        _masteryService = masteryService;
        _clock = clock;
    }

    public bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "Study" && integrationEvent.Name == "TestResultRecordedDomainEvent";

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        if (await _repository.HasProcessedAsync(envelope.EventId, cancellationToken))
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<TestResultRecordedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Subject) || payload.TotalQuestions <= 0)
        {
            await MarkProcessedAsync(envelope.EventId, cancellationToken);
            return;
        }

        await _masteryService.ApplyTestAsync(payload.StudentId, payload.Subject, payload.Topic, payload.TotalQuestions, payload.Net, cancellationToken);
        await MarkProcessedAsync(envelope.EventId, cancellationToken);
    }

    private async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await _repository.AddProcessedAsync(new ProcessedEvent(eventId, _clock.UtcNow), cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private sealed record TestResultRecordedPayload(
        Guid TestResultId,
        Guid StudentId,
        string Subject,
        string? Topic,
        int TotalQuestions,
        int Correct,
        int Wrong,
        int Blank,
        decimal Net,
        DateTime TakenOnUtc);
}
