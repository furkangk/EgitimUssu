using System.Text.Json;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

/// <summary>
/// Veli read-model projeksiyon handler'ları için ortak taban. Idempotency (çift-sayım koruması),
/// snapshot upsert ve JSON çözümleme yardımcılarını sağlar. Outbox en-az-bir-kez teslim eder;
/// bu yüzden her event <see cref="ProcessedIntegrationEvent"/> ile bir kez işlenir.
/// </summary>
internal abstract class ParentReadModelProjectionHandler : IIntegrationEventHandler
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected ParentReadModelProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
    {
        DbContext = dbContext;
        IdGenerator = idGenerator;
        Clock = clock;
    }

    protected ParentsDbContext DbContext { get; }

    protected IIdGenerator IdGenerator { get; }

    protected IClock Clock { get; }

    public abstract bool CanHandle(IIntegrationEvent integrationEvent);

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        var alreadyProcessed = await DbContext.ProcessedIntegrationEvents
            .AnyAsync(processed => processed.Id == envelope.EventId, cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var applied = await ApplyAsync(envelope, cancellationToken);
        if (!applied)
        {
            return;
        }

        DbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent(envelope.EventId, envelope.Name, Clock.UtcNow));
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Event'i projekte eder. İşlenmesi gerekmiyorsa false döner (dedup kaydı da yazılmaz).</summary>
    protected abstract Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken);

    protected async Task<ChildProgressSnapshot> GetOrCreateSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var snapshot = await DbContext.ChildProgressSnapshots
            .FirstOrDefaultAsync(item => item.StudentId == studentId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new ChildProgressSnapshot(IdGenerator.New(), studentId, Clock.UtcNow);
            DbContext.ChildProgressSnapshots.Add(snapshot);
        }

        return snapshot;
    }

    protected static T? Deserialize<T>(IntegrationEvent envelope)
        => JsonSerializer.Deserialize<T>(envelope.Payload, JsonOptions);
}

/// <summary>M05 LessonSessions → çocuk ders sayaçları (planlanan/tamamlanan + son ders tarihi).</summary>
internal sealed class ParentLessonProjectionHandler : ParentReadModelProjectionHandler
{
    public ParentLessonProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, idGenerator, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "LessonSessions"
            && integrationEvent.Name is "LessonSessionCreatedDomainEvent" or "LessonSessionCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (envelope.Name == "LessonSessionCreatedDomainEvent")
        {
            var payload = Deserialize<LessonSessionCreatedPayload>(envelope);
            if (payload is null)
            {
                return false;
            }

            var snapshot = await GetOrCreateSnapshotAsync(payload.StudentId, cancellationToken);
            snapshot.RegisterPlannedLesson(Clock.UtcNow);
            return true;
        }

        var completed = Deserialize<LessonSessionCompletedPayload>(envelope);
        if (completed is null)
        {
            return false;
        }

        var target = await GetOrCreateSnapshotAsync(completed.StudentId, cancellationToken);
        target.RegisterCompletedLesson(completed.CompletedOnUtc, Clock.UtcNow);
        return true;
    }

    private sealed record LessonSessionCreatedPayload(Guid LessonSessionId, Guid StudentId, DateTime CreatedOnUtc);

    private sealed record LessonSessionCompletedPayload(Guid LessonSessionId, Guid StudentId, DateTime CompletedOnUtc);
}

/// <summary>M06 Assignments → açık/tamamlanan ödev sayaçları.</summary>
internal sealed class ParentAssignmentProjectionHandler : ParentReadModelProjectionHandler
{
    public ParentAssignmentProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, idGenerator, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Assignments"
            && integrationEvent.Name is "AssignmentCreatedDomainEvent" or "AssignmentCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = Deserialize<AssignmentPayload>(envelope);
        if (payload is null)
        {
            return false;
        }

        var snapshot = await GetOrCreateSnapshotAsync(payload.StudentId, cancellationToken);
        if (envelope.Name == "AssignmentCreatedDomainEvent")
        {
            snapshot.RegisterAssignmentCreated(Clock.UtcNow);
        }
        else
        {
            snapshot.RegisterAssignmentCompleted(Clock.UtcNow);
        }

        return true;
    }

    private sealed record AssignmentPayload(Guid AssignmentId, Guid StudentId);
}

/// <summary>M07 Payments → beklenen/tahsil edilen/açık tutar özeti.</summary>
internal sealed class ParentPaymentProjectionHandler : ParentReadModelProjectionHandler
{
    private const int PaidStatus = 3; // PaymentStatus.Paid (enum'lar JSON'da int serileşir)

    public ParentPaymentProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, idGenerator, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments"
            && integrationEvent.Name is "PaymentRecordCreatedDomainEvent" or "PaymentRecordUpdatedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (envelope.Name == "PaymentRecordCreatedDomainEvent")
        {
            var created = Deserialize<PaymentCreatedPayload>(envelope);
            if (created is null)
            {
                return false;
            }

            var snapshot = await GetOrCreateSnapshotAsync(created.StudentId, cancellationToken);
            snapshot.RegisterPaymentCreated(created.ExpectedAmount, created.Currency ?? "TRY", created.Status == PaidStatus, created.CreatedOnUtc);
            return true;
        }

        var updated = Deserialize<PaymentUpdatedPayload>(envelope);
        if (updated is null)
        {
            return false;
        }

        var target = await GetOrCreateSnapshotAsync(updated.StudentId, cancellationToken);
        var collectedDelta = updated.CurrentCollectedAmount - updated.PreviousCollectedAmount;
        target.RegisterPaymentUpdated(collectedDelta, updated.UpdatedOnUtc);
        return true;
    }

    private sealed record PaymentCreatedPayload(
        Guid PaymentRecordId,
        Guid StudentId,
        decimal ExpectedAmount,
        string? Currency,
        int Status,
        DateTime CreatedOnUtc);

    private sealed record PaymentUpdatedPayload(
        Guid PaymentRecordId,
        Guid StudentId,
        decimal PreviousCollectedAmount,
        decimal CurrentCollectedAmount,
        DateTime UpdatedOnUtc);
}

/// <summary>M03 Students → öğrenci→kullanıcı eşlemesi (bağ onayı yetkisi için).</summary>
internal sealed class ParentStudentDirectoryProjectionHandler : ParentReadModelProjectionHandler
{
    public ParentStudentDirectoryProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, idGenerator, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfileCreatedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = Deserialize<StudentProfileCreatedPayload>(envelope);
        if (payload is null)
        {
            return false;
        }

        var existing = await DbContext.KnownStudents
            .FirstOrDefaultAsync(student => student.StudentId == payload.StudentProfileId, cancellationToken);
        if (existing is null)
        {
            DbContext.KnownStudents.Add(new KnownStudent(IdGenerator.New(), payload.StudentProfileId, payload.UserId, Clock.UtcNow));
        }
        else
        {
            existing.SetUserId(payload.UserId, Clock.UtcNow);
        }

        return true;
    }

    private sealed record StudentProfileCreatedPayload(Guid StudentProfileId, Guid? UserId, DateTime CreatedOnUtc);
}
