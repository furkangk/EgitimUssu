using System.Text.Json;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

public sealed class ParentNotificationTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    // Handler artık ortak inbox (IdempotentIntegrationEventHandler) tabanlı olduğundan replay-koruması
    // için gerçek bir NotificationsDbContext (InMemory) gerekir; iş-yazımı hâlâ FakeRepo üzerinden izlenir.
    private static NotificationsDbContext NewDbContext() =>
        new(
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new NoOpDomainEventMapper());

    [Fact]
    public void Ctor_StoresFields()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var n = new ParentNotification(Guid.NewGuid(), parentUserId, studentId, ParentNotificationType.NewAssignment, "Yeni ödev", "Ali'ye yeni ödev verildi.", Now);

        Assert.Equal(parentUserId, n.ParentUserId);
        Assert.Equal(studentId, n.StudentId);
        Assert.Equal(ParentNotificationType.NewAssignment, n.Type);
        Assert.Equal("Yeni ödev", n.Title);
        Assert.Equal(Now, n.CreatedOnUtc);
    }

    [Fact]
    public async Task AssignmentCreated_PremiumParentWithPref_CreatesNotification()
    {
        var studentId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var directory = new FakeDirectory(new[]
        {
            new ParentNotificationTarget(parentUserId, MembershipTier.Premium, AllOn())
        });
        var repo = new FakeRepo();
        await using var db = NewDbContext();
        var handler = new ParentEventNotificationHandler(db, directory, repo, new SeqIdGen(), new FixedClock(Now));

        await handler.HandleAsync(AssignmentCreatedEnvelope(studentId), default);

        Assert.Single(repo.Added);
        Assert.Equal(ParentNotificationType.NewAssignment, repo.Added[0].Type);
        Assert.Equal(parentUserId, repo.Added[0].ParentUserId);
    }

    [Fact]
    public async Task AssignmentCreated_FreeParent_SkipsNotification()
    {
        var studentId = Guid.NewGuid();
        var directory = new FakeDirectory(new[]
        {
            new ParentNotificationTarget(Guid.NewGuid(), MembershipTier.Free, AllOn())
        });
        var repo = new FakeRepo();
        await using var db = NewDbContext();
        var handler = new ParentEventNotificationHandler(db, directory, repo, new SeqIdGen(), new FixedClock(Now));

        await handler.HandleAsync(AssignmentCreatedEnvelope(studentId), default);

        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task AssignmentCreated_PremiumParentPrefOff_SkipsNotification()
    {
        var studentId = Guid.NewGuid();
        var prefs = new ParentNotificationPrefs(MissedAssignment: false, WeeklyProgressSummary: true, LessonReminders: true, TestResults: true, Payments: true);
        var directory = new FakeDirectory(new[]
        {
            new ParentNotificationTarget(Guid.NewGuid(), MembershipTier.Premium, prefs)
        });
        var repo = new FakeRepo();
        await using var db = NewDbContext();
        var handler = new ParentEventNotificationHandler(db, directory, repo, new SeqIdGen(), new FixedClock(Now));

        await handler.HandleAsync(AssignmentCreatedEnvelope(studentId), default);

        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task DuplicateEvent_ProcessedOnce()
    {
        var studentId = Guid.NewGuid();
        var directory = new FakeDirectory(new[]
        {
            new ParentNotificationTarget(Guid.NewGuid(), MembershipTier.Premium, AllOn())
        });
        var repo = new FakeRepo();
        await using var db = NewDbContext();
        var handler = new ParentEventNotificationHandler(db, directory, repo, new SeqIdGen(), new FixedClock(Now));
        var envelope = AssignmentCreatedEnvelope(studentId);

        await handler.HandleAsync(envelope, default);
        await handler.HandleAsync(envelope, default);

        Assert.Single(repo.Added);
    }

    private static ParentNotificationPrefs AllOn()
        => new(MissedAssignment: true, WeeklyProgressSummary: true, LessonReminders: true, TestResults: true, Payments: true);

    private static IntegrationEvent AssignmentCreatedEnvelope(Guid studentId)
    {
        var payload = JsonSerializer.Serialize(
            new { AssignmentId = Guid.NewGuid(), StudentId = studentId, TeacherUserId = Guid.NewGuid(), LessonSessionId = (Guid?)null, CreatedOnUtc = Now },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new IntegrationEvent(Guid.NewGuid(), Now, "AssignmentCreatedDomainEvent", "Assignments", payload);
    }

    private sealed class FakeDirectory : IParentNotificationDirectory
    {
        private readonly IReadOnlyCollection<ParentNotificationTarget> _targets;
        public FakeDirectory(IReadOnlyCollection<ParentNotificationTarget> targets) => _targets = targets;

        public Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult(_targets);

        public Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentStudentNotificationTarget>>(
                _targets.Select(t => new ParentStudentNotificationTarget(Guid.NewGuid(), t)).ToArray());
    }

    private sealed class FakeRepo : IParentNotificationRepository
    {
        public List<ParentNotification> Added { get; } = new();
        private readonly HashSet<Guid> _processed = new();

        public Task AddAsync(ParentNotification notification, CancellationToken ct)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ParentNotification>> ListByParentAsync(Guid parentUserId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentNotification>>(Added.Where(n => n.ParentUserId == parentUserId).ToArray());

        public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken ct) => Task.FromResult(_processed.Contains(eventId));

        public void MarkProcessed(Guid eventId, string eventName, DateTime nowUtc) => _processed.Add(eventId);

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SeqIdGen : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }
}
