using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>M07-2: Tamamlanan ücretli dersten otomatik ödeme kaydı üretimi (integration event tüketimi).</summary>
public sealed class LessonSessionCompletedPaymentHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LessonSessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Completed_Chargeable_Session_Should_Create_Payment_Record()
    {
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, Session(isChargeable: true), Rate(250m, "TRY"));

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        var record = await dbContext.PaymentRecords.SingleAsync();
        Assert.Equal(TeacherUserId, record.TeacherUserId);
        Assert.Equal(StudentId, record.StudentId);
        Assert.Equal(LessonSessionId, record.RelatedLessonSessionId);
        Assert.Equal(BillingItemType.LessonFee, record.ItemType);
        Assert.Equal(250m, record.ExpectedAmount);
        Assert.Equal(0m, record.CollectedAmount);
        Assert.Equal("TRY", record.Currency);
        Assert.Equal(PaymentStatus.Pending, record.Status);
    }

    [Fact]
    public async Task Same_Event_Twice_Should_Create_Single_Payment_Record()
    {
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, Session(isChargeable: true), Rate(250m, "TRY"));
        var envelope = Envelope(Guid.NewGuid());

        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);

        Assert.Equal(1, await dbContext.PaymentRecords.CountAsync());
    }

    [Fact]
    public async Task Second_Event_For_Same_Session_Should_Not_Duplicate_Payment_Record()
    {
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, Session(isChargeable: true), Rate(250m, "TRY"));

        // Aynı ders yeniden tamamlanabilir (öğretmen konu/not düzeltmesi) → yeni EventId, aynı oturum.
        await handler.HandleAsync(Envelope(Guid.NewGuid()));
        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        Assert.Equal(1, await dbContext.PaymentRecords.CountAsync());
    }

    [Fact]
    public async Task Non_Chargeable_Session_Should_Not_Create_Payment()
    {
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, Session(isChargeable: false), Rate(250m, "TRY"));

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        Assert.Equal(0, await dbContext.PaymentRecords.CountAsync());
        Assert.Single(await dbContext.InboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Missing_Agreed_Rate_Should_Not_Create_Payment()
    {
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, Session(isChargeable: true), agreedRate: null);

        await handler.HandleAsync(Envelope(Guid.NewGuid()));

        // Sessizce yanlış tutar üretmektense hiç kayıt üretme (plan Task 5 Step 4).
        Assert.Equal(0, await dbContext.PaymentRecords.CountAsync());
        Assert.Single(await dbContext.InboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Missing_Source_Session_Throws_And_Remains_Retryable_Without_Inbox()
    {
        await using var dbContext = NewDbContext();
        var handler = NewHandler(dbContext, Session(true) with { Id = Guid.NewGuid() }, Rate(250m, "TRY"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Envelope(Guid.NewGuid())));
        Assert.Empty(await dbContext.InboxMessages.ToListAsync());
        Assert.Empty(await dbContext.PaymentRecords.ToListAsync());
    }

    private static LessonSessionCompletedPaymentHandler NewHandler(
        PaymentsDbContext dbContext,
        LessonSessionDetails session,
        AgreedLessonRate? agreedRate) =>
        new(
            dbContext,
            new FakeLessonSessionAccess(session),
            new FakeStudentDirectory(agreedRate),
            new SequentialIdGenerator(),
            new FixedClock(Now),
            NullLogger<LessonSessionCompletedPaymentHandler>.Instance);

    private static PaymentsDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new NoOpDomainEventMapper());

    private static LessonSessionDetails Session(bool isChargeable) =>
        new(
            LessonSessionId,
            LessonScheduleId: null,
            TeacherUserId,
            StudentId,
            IsCompleted: true,
            CompletedOnUtc: Now,
            TopicTitle: "Türev",
            CoveredContent: null,
            TeacherNotes: null,
            IsChargeable: isChargeable);

    private static AgreedLessonRate Rate(decimal amount, string currency) => new(amount, currency);

    private static IntegrationEvent Envelope(Guid eventId) =>
        new(
            eventId,
            Now,
            "LessonSessionCompletedDomainEvent",
            "LessonSessions",
            $$"""{"lessonSessionId":"{{LessonSessionId}}","lessonScheduleId":null,"teacherUserId":"{{TeacherUserId}}","studentId":"{{StudentId}}","completedOnUtc":"2026-09-04T12:00:00Z"}""");

    private sealed class FakeLessonSessionAccess : ILessonSessionAccessService
    {
        private readonly LessonSessionDetails _session;

        public FakeLessonSessionAccess(LessonSessionDetails session) => _session = session;

        public Task<LessonSessionDetails?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult<LessonSessionDetails?>(lessonSessionId == _session.Id ? _session : null);
    }

    private sealed class FakeStudentDirectory : IStudentDirectory
    {

        public Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guid>>([]);
        private readonly AgreedLessonRate? _rate;

        public FakeStudentDirectory(AgreedLessonRate? rate) => _rate = rate;

        public Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<Guid?>(null);

        public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(_rate);

        public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(false);
    
        public Task<string?> GetDisplayNameAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<string?>("Test Öğrenci");
}

    private sealed class SequentialIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
