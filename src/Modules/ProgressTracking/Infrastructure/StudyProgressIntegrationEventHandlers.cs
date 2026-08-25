using System.Text.Json;
using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

/// <summary>
/// M08 <c>StudySessionCompletedDomainEvent</c> → ilgili ders+konu hâkimiyetine çalışma süresi ekler.
/// Idempotent: aynı olay iki kez işlenmez (ortak inbox, bkz. <see cref="IdempotentIntegrationEventHandler"/>).
/// </summary>
internal sealed class StudySessionCompletedProgressHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly MasteryService _masteryService;

    public StudySessionCompletedProgressHandler(ProgressTrackingDbContext dbContext, MasteryService masteryService, IClock clock)
        : base(dbContext, clock)
        => _masteryService = masteryService;

    public override bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "Study" && integrationEvent.Name == "StudySessionCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudySessionCompletedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Subject) || payload.EffectiveMinutes <= 0)
        {
            return true; // işlenecek bir şey yok ama dedup kaydı yazılsın (eski davranış: MarkProcessed'di)
        }

        await _masteryService.ApplyStudyAsync(payload.StudentId, payload.Subject, payload.Topic, payload.EffectiveMinutes, cancellationToken);
        return true;
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
internal sealed class TestResultRecordedProgressHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly MasteryService _masteryService;

    public TestResultRecordedProgressHandler(ProgressTrackingDbContext dbContext, MasteryService masteryService, IClock clock)
        : base(dbContext, clock)
        => _masteryService = masteryService;

    public override bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "Study" && integrationEvent.Name == "TestResultRecordedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TestResultRecordedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Subject) || payload.TotalQuestions <= 0)
        {
            return true; // işlenecek bir şey yok ama dedup kaydı yazılsın (eski davranış: MarkProcessed'di)
        }

        await _masteryService.ApplyTestAsync(payload.StudentId, payload.Subject, payload.Topic, payload.TotalQuestions, payload.Net, cancellationToken);
        return true;
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
