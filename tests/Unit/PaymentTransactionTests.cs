using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;

namespace EgitimUssu.Tests.Unit;

public sealed class PaymentTransactionTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Collection_And_Refund_Derive_Net_And_Status_From_History()
    {
        var payment = NewPayment();
        Assert.True(payment.AddCollection(Guid.NewGuid(), 250m, PaymentMethod.Cash, Now, "elden", Now).IsSuccess);
        Assert.Equal(250m, payment.CollectedAmount);
        Assert.Equal(PaymentStatus.PartiallyPaid, payment.Status);
        Assert.True(payment.AddCollection(Guid.NewGuid(), 250m, PaymentMethod.BankTransfer, Now, null, Now).IsSuccess);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.True(payment.Refund(Guid.NewGuid(), 100m, PaymentMethod.BankTransfer, Now, "iade", Now).IsSuccess);
        Assert.Equal(400m, payment.CollectedAmount);
        Assert.Equal(PaymentStatus.PartiallyPaid, payment.Status);
        Assert.Equal(3, payment.Transactions.Count);
        Assert.Equal(3, payment.DomainEvents.OfType<PaymentTransactionRecordedIntegrationEvent>().Count());
    }

    [Fact]
    public void Detail_Update_After_Parent_Confirmation_Preserves_Collection()
    {
        var payment = NewPayment();
        Assert.True(payment.MarkCollectedByParentConfirmation(500m, Now).IsSuccess);
        Assert.True(payment.UpdateDetails("Yeni açıklama", 500m, Now.AddDays(3), "not", Now).IsSuccess);
        Assert.Equal("Yeni açıklama", payment.Description);
        Assert.Equal(500m, payment.CollectedAmount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentMethod.ParentDeclaration, Assert.Single(payment.Transactions).Method);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    public void Invalid_Amounts_Do_Not_Create_Movements(decimal amount)
    {
        var payment = NewPayment();
        Assert.True(payment.AddCollection(Guid.NewGuid(), amount, PaymentMethod.Cash, Now, null, Now).IsFailure);
        Assert.Empty(payment.Transactions);
    }

    [Fact]
    public void Cannot_Refund_More_Than_Net_Or_Cancel_Until_Money_Is_Returned()
    {
        var payment = NewPayment();
        payment.AddCollection(Guid.NewGuid(), 250m, PaymentMethod.Cash, Now, null, Now);
        Assert.True(payment.Refund(Guid.NewGuid(), 251m, PaymentMethod.Cash, Now, null, Now).IsFailure);
        Assert.True(payment.Cancel(Now).IsFailure);
        Assert.Equal(250m, payment.CollectedAmount);
        Assert.True(payment.Refund(Guid.NewGuid(), 250m, PaymentMethod.Cash, Now, null, Now).IsSuccess);
        Assert.True(payment.Cancel(Now).IsSuccess);
        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.True(payment.AddCollection(Guid.NewGuid(), 1m, PaymentMethod.Cash, Now, null, Now).IsFailure);
    }

    [Fact]
    public async Task Cancelled_Records_Are_Excluded_From_Summary_Expected_Total()
    {
        var cancelled = NewPayment();
        cancelled.Cancel(Now);
        var result = await new GetTeacherPaymentSummaryQueryHandler(new Records(cancelled), new Clock(),
            new LocalCalendar("Europe/Istanbul", new Clock())).Handle(new(cancelled.TeacherUserId), default);
        Assert.Equal(0m, Assert.Single(result.Value!.CurrencySummaries).ExpectedAmountTotal);
    }

    [Fact]
    public void Details_Recompute_Status_When_Expected_Total_Changes()
    {
        var payment = NewPayment();
        payment.AddCollection(Guid.NewGuid(), 500m, PaymentMethod.Cash, Now, null, Now);
        payment.UpdateDetails("Ders", 600m, Now.AddDays(2), null, Now);
        Assert.Equal(PaymentStatus.PartiallyPaid, payment.Status);
        Assert.Equal(500m, payment.CollectedAmount);
        payment.UpdateDetails("Ders", 400m, Now.AddDays(2), null, Now);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public async Task Monthly_Collection_Uses_Transaction_Key_And_Refund_In_Its_Local_Month()
    {
        await using var db = new ReportingDbContext(new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new JsonDomainEventMapper());
        var teacher = Guid.NewGuid();
        var payment = Guid.NewGuid();
        var transaction = Guid.NewGuid();
        var handler = new TeacherMonthlyPaymentCollectedProjectionHandler(db, new Clock(), new LocalCalendar("Europe/Istanbul", new Clock()));
        IntegrationEvent Event(Guid id, int kind, decimal amount, DateTime date) => new(Guid.NewGuid(), Now,
            "PaymentTransactionRecordedIntegrationEvent", "Payments", JsonSerializer.Serialize(new
            { paymentRecordId = payment, transactionId = id, teacherUserId = teacher, kind, amount, currency = "TRY", occurredOnUtc = date }));
        await handler.HandleAsync(Event(transaction, 1, 500m, new DateTime(2026, 10, 31, 22, 0, 0, DateTimeKind.Utc)));
        await handler.HandleAsync(Event(transaction, 1, 500m, new DateTime(2026, 10, 31, 22, 0, 0, DateTimeKind.Utc)));
        await handler.HandleAsync(Event(Guid.NewGuid(), 2, 100m, new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc)));
        var projection = Assert.Single(await db.TeacherMonthlyProjections.ToArrayAsync());
        Assert.Equal(11, projection.Month);
        Assert.Equal(400m, projection.CollectedAmount);
        Assert.Equal(2, projection.Items.Count);
    }

    [Fact]
    public async Task Due_Date_Remains_Open_Throughout_The_Local_Due_Day()
    {
        var payment = NewPayment();
        payment.UpdateDetails("Ders", 500m, new DateTime(2026, 11, 14, 22, 0, 0, DateTimeKind.Utc), null, Now);
        var summary = await new GetTeacherPaymentSummaryQueryHandler(new Records(payment), new Clock(), new LocalCalendar("Europe/Istanbul", new Clock()))
            .Handle(new(payment.TeacherUserId), default);
        Assert.Equal(0, Assert.Single(summary.Value!.CurrencySummaries).OverdueCount);
    }

    [Fact]
    public void Note_Edit_Does_Not_Reissue_Overdue_Notification_For_Unchanged_Due_Date()
    {
        var payment = NewPayment();
        var calendar = new LocalCalendar("Europe/Istanbul", new Clock());
        var due = Now.AddDays(-2);
        payment.UpdateDetails("Ders", 500m, due, null, Now);
        Assert.True(payment.MarkOverdue(Now, calendar));
        payment.UpdateDetails("Ders", 500m, due, "Yalnız not", Now);
        Assert.False(payment.MarkOverdue(Now, calendar));
        Assert.Single(payment.DomainEvents.OfType<PaymentBecameOverdueDomainEvent>());
    }

    internal static PaymentRecord NewPayment() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
        BillingItemType.LessonFee, "Ders", "TRY", 500m, 0m, Now.AddDays(1), null, PaymentStatus.Pending, null, null, null, Now);
    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
    private sealed class Records(PaymentRecord record) : IPaymentRecordRepository
    {
        public Task<PaymentRecord?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<PaymentRecord?>(record);
        public Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<PaymentRecord>>([record]);
        public Task AddAsync(PaymentRecord payment, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
