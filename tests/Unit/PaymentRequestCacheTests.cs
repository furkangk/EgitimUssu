using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P07 Task 3 / O2: Ödeme kaydını authorizer ve handler tek sorguda paylaşır.
/// </summary>
public sealed class PaymentRequestCacheTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.NewGuid();

    [Fact]
    public async Task Authorized_GetById_Should_Load_Payment_Once()
    {
        var record = Record();
        var repository = new CountingPaymentRepository(record);
        var cache = new FakeRequestEntityCache();
        var query = new GetPaymentRecordByIdQuery(record.Id);

        var authorization = await new PaymentRecordAuthorizer(Teacher(), repository, cache, new FakeTeacherStudentAccess())
            .Authorize(query, CancellationToken.None);
        var result = await new GetPaymentRecordByIdQueryHandler(repository, new FixedClock(Now), cache, new LocalCalendar("Europe/Istanbul", new FixedClock(Now)))
            .Handle(query, CancellationToken.None);

        Assert.True(authorization.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.GetByIdCount);
    }

    [Fact]
    public async Task Authorized_Update_Should_Load_Payment_Once()
    {
        var record = Record();
        var repository = new CountingPaymentRepository(record);
        var cache = new FakeRequestEntityCache();
        var command = new UpdatePaymentRecordCommand(
            record.Id, "Ders", 150m, Now.AddDays(7), null);

        var authorization = await new PaymentRecordAuthorizer(Teacher(), repository, cache, new FakeTeacherStudentAccess())
            .Authorize(command, CancellationToken.None);
        var result = await new UpdatePaymentRecordCommandHandler(repository, new FixedClock(Now), cache, new LocalCalendar("Europe/Istanbul", new FixedClock(Now)))
            .Handle(command, CancellationToken.None);

        Assert.True(authorization.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(150m, result.Value!.ExpectedAmount);
        Assert.Equal(1, repository.GetByIdCount);
    }

    [Fact]
    public async Task Handler_Should_Still_Load_When_Cache_Is_Empty()
    {
        var record = Record();
        var repository = new CountingPaymentRepository(record);

        var result = await new GetPaymentRecordByIdQueryHandler(repository, new FixedClock(Now), new FakeRequestEntityCache(), new LocalCalendar("Europe/Istanbul", new FixedClock(Now)))
            .Handle(new GetPaymentRecordByIdQuery(record.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.GetByIdCount);
    }

    private static FakeCurrentUser Teacher() => new(TeacherUserId.ToString(), "Teacher");

    private static PaymentRecord Record()
        => new(
            Guid.NewGuid(),
            TeacherUserId,
            studentId: Guid.NewGuid(),
            relatedLessonSessionId: null,
            BillingItemType.LessonFee,
            description: "Ders ücreti",
            currency: "TRY",
            expectedAmount: 100m,
            collectedAmount: 0m,
            dueDateUtc: Now.AddDays(7),
            collectedOnUtc: null,
            status: PaymentStatus.Pending,
            billingPeriodStartUtc: null,
            billingPeriodEndUtc: null,
            notes: null,
            createdOnUtc: Now.AddDays(-1));

    private sealed class CountingPaymentRepository(PaymentRecord record) : IPaymentRecordRepository
    {
        public int GetByIdCount { get; private set; }

        public Task<PaymentRecord?> GetByIdAsync(Guid paymentRecordId, CancellationToken cancellationToken)
        {
            GetByIdCount++;
            return Task.FromResult<PaymentRecord?>(paymentRecordId == record.Id ? record : null);
        }

        public Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<PaymentRecord>>([record]);

        public Task AddAsync(PaymentRecord paymentRecord, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }
}
