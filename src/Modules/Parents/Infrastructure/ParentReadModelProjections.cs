using System.Text.Json;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

/// <summary>
/// Veli read-model projeksiyon handler'ları için ortak taban. Snapshot upsert ve JSON çözümleme
/// yardımcılarını sağlar. Idempotency (çift-sayım koruması) artık ortak inbox üzerinden
/// <see cref="IdempotentIntegrationEventHandler"/> tabanında sağlanır.
/// </summary>
internal abstract class ParentReadModelProjectionHandler : IdempotentIntegrationEventHandler
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected ParentReadModelProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, clock)
        => IdGenerator = idGenerator;

    protected IIdGenerator IdGenerator { get; }

    protected ParentsDbContext ParentsDb => (ParentsDbContext)DbContext;

    protected async Task<ChildProgressSnapshot> GetOrCreateSnapshotAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var snapshot = await ParentsDb.ChildProgressSnapshots
            .FirstOrDefaultAsync(item => item.StudentId == studentId, cancellationToken);
        if (snapshot is null)
        {
            snapshot = new ChildProgressSnapshot(IdGenerator.New(), studentId, Clock.UtcNow);
            ParentsDb.ChildProgressSnapshots.Add(snapshot);
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
                throw new PoisonMessageException("Olay yükü null olamaz.");
            }

            var snapshot = await GetOrCreateSnapshotAsync(payload.StudentId, cancellationToken);
            snapshot.RegisterPlannedLesson(Clock.UtcNow);
            return true;
        }

        var completed = Deserialize<LessonSessionCompletedPayload>(envelope);
        if (completed is null)
        {
            throw new PoisonMessageException("Olay yükü null olamaz.");
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
            throw new PoisonMessageException("Olay yükü null olamaz.");
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
                throw new PoisonMessageException("Olay yükü null olamaz.");
            }

            var snapshot = await GetOrCreateSnapshotAsync(created.StudentId, cancellationToken);
            snapshot.RegisterPaymentCreated(created.ExpectedAmount, created.Currency ?? "TRY", created.Status == PaidStatus, created.CreatedOnUtc);
            return true;
        }

        var updated = Deserialize<PaymentUpdatedPayload>(envelope);
        if (updated is null)
        {
            throw new PoisonMessageException("Olay yükü null olamaz.");
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

/// <summary>
/// M03 Students → öğrenci→kullanıcı eşlemesi (bağ onayı yetkisi ve gizlilik ayarı için).
/// <para>
/// P15 / N-27: Eşleme iki olaydan dolar — profil oluşturma (öğrencinin kendi açtığı profil hesabıyla doğar) ve
/// <c>StudentProfileLinkedToUser</c> (öğretmenin açtığı profil davet kabulüyle hesaba bağlanır). Eskiden yalnız ilki
/// tüketiliyordu: davetle bağlanan öğrenci veli talebini onaylayamıyor, "Gizli" ayarı veli panelinde okunamıyordu.
/// Bilinen bir kullanıcı hiçbir olayla <c>null</c>'a çekilmez (oluşturma olayı sonradan işlense bile).
/// </para>
/// </summary>
internal sealed class ParentStudentDirectoryProjectionHandler : ParentReadModelProjectionHandler
{
    private const string ProfileCreated = "StudentProfileCreatedDomainEvent";
    private const string ProfileLinkedToUser = "StudentProfileLinkedToUserDomainEvent";

    public ParentStudentDirectoryProjectionHandler(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
        : base(dbContext, idGenerator, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name is ProfileCreated or ProfileLinkedToUser;

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (envelope.Name == ProfileLinkedToUser)
        {
            var linked = Deserialize<StudentProfileLinkedToUserIntegrationEvent>(envelope);
            if (linked is null) throw new PoisonMessageException("Öğrenci bağlantı olay yükü null olamaz.");
            return await UpsertAsync(linked.StudentId, linked.UserId, cancellationToken);
        }

        var created = Deserialize<StudentProfileCreatedPayload>(envelope);
        if (created is null) throw new PoisonMessageException("Öğrenci oluşturma olay yükü null olamaz.");
        return await UpsertAsync(created.StudentProfileId, created.UserId, cancellationToken);
    }

    private async Task<bool> UpsertAsync(Guid studentId, Guid? userId, CancellationToken cancellationToken)
    {
        var existing = await ParentsDb.KnownStudents
            .FirstOrDefaultAsync(student => student.StudentId == studentId, cancellationToken);
        if (existing is null)
        {
            ParentsDb.KnownStudents.Add(new KnownStudent(IdGenerator.New(), studentId, userId, Clock.UtcNow));
            return true;
        }

        if (userId is null || existing.UserId == userId)
        {
            return true;
        }

        existing.SetUserId(userId, Clock.UtcNow);
        return true;
    }

    private sealed record StudentProfileCreatedPayload(Guid StudentProfileId, Guid? UserId, DateTime CreatedOnUtc);
}
