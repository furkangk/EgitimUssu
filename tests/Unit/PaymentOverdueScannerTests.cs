using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>M07-1: Vadesi geçen ödemelerin otomatik <see cref="PaymentStatus.Overdue"/> işaretlenmesi.</summary>
public sealed class PaymentOverdueScannerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void MarkOverdue_Should_Only_Apply_To_Outstanding_Past_Due()
    {
        // Vadesi geçmiş Pending → Overdue + olay
        var pendingPastDue = NewRecord(PaymentStatus.Pending, Now.AddDays(-1));
        Assert.True(pendingPastDue.MarkOverdue(Now, new LocalCalendar("Europe/Istanbul", new FixedClock(Now))));
        Assert.Equal(PaymentStatus.Overdue, pendingPastDue.Status);
        Assert.Equal(Now, pendingPastDue.OverdueMarkedOnUtc);
        Assert.Contains(pendingPastDue.DomainEvents, domainEvent => domainEvent is PaymentBecameOverdueDomainEvent);

        // Vadesi geçmiş PartiallyPaid → Overdue
        var partialPastDue = NewRecord(PaymentStatus.PartiallyPaid, Now.AddDays(-3));
        Assert.True(partialPastDue.MarkOverdue(Now, new LocalCalendar("Europe/Istanbul", new FixedClock(Now))));
        Assert.Equal(PaymentStatus.Overdue, partialPastDue.Status);

        // Vadesi gelmemiş Pending → değişmez
        var pendingFuture = NewRecord(PaymentStatus.Pending, Now.AddDays(1));
        Assert.False(pendingFuture.MarkOverdue(Now, new LocalCalendar("Europe/Istanbul", new FixedClock(Now))));
        Assert.Equal(PaymentStatus.Pending, pendingFuture.Status);
        Assert.DoesNotContain(pendingFuture.DomainEvents, domainEvent => domainEvent is PaymentBecameOverdueDomainEvent);

        // Tahsil edilmiş kayıt → vadesi geçse de değişmez
        var paid = NewRecord(PaymentStatus.Paid, Now.AddDays(-5));
        Assert.False(paid.MarkOverdue(Now, new LocalCalendar("Europe/Istanbul", new FixedClock(Now))));
        Assert.Equal(PaymentStatus.Paid, paid.Status);

        // İptal edilmiş kayıt → değişmez
        var cancelled = NewRecord(PaymentStatus.Cancelled, Now.AddDays(-5));
        Assert.False(cancelled.MarkOverdue(Now, new LocalCalendar("Europe/Istanbul", new FixedClock(Now))));
        Assert.Equal(PaymentStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void MarkOverdue_Should_Be_Idempotent()
    {
        var record = NewRecord(PaymentStatus.Pending, Now.AddDays(-1));

        Assert.True(record.MarkOverdue(Now, new LocalCalendar("Europe/Istanbul", new FixedClock(Now))));
        Assert.False(record.MarkOverdue(Now.AddHours(1), new LocalCalendar("Europe/Istanbul", new FixedClock(Now.AddHours(1)))));
        Assert.Equal(Now, record.OverdueMarkedOnUtc);
        Assert.Single(record.DomainEvents.OfType<PaymentBecameOverdueDomainEvent>());
    }

    [Fact]
    public async Task Scanner_Should_Be_Idempotent()
    {
        await using var dbContext = NewDbContext();
        dbContext.PaymentRecords.AddRange(
            NewRecord(PaymentStatus.Pending, Now.AddDays(-1)),
            NewRecord(PaymentStatus.PartiallyPaid, Now.AddDays(-2)),
            NewRecord(PaymentStatus.Paid, Now.AddDays(-3)),
            NewRecord(PaymentStatus.Pending, Now.AddDays(2)));
        await dbContext.SaveChangesAsync();

        var processor = new PaymentOverdueScanProcessor(dbContext, new FixedClock(Now), new LocalCalendar("Europe/Istanbul", new FixedClock(Now)));

        var firstRun = await processor.ScanAsync();
        var secondRun = await processor.ScanAsync();

        Assert.Equal(2, firstRun);
        Assert.Equal(0, secondRun);
        Assert.Equal(2, await dbContext.PaymentRecords.CountAsync(record => record.Status == PaymentStatus.Overdue));
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(message => message.Type == nameof(PaymentBecameOverdueDomainEvent)));
    }

    private static PaymentsDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new JsonDomainEventMapper());

    private static PaymentRecord NewRecord(PaymentStatus status, DateTime dueDateUtc) =>
        new(
            Guid.NewGuid(),
            TeacherUserId,
            StudentId,
            null,
            BillingItemType.LessonFee,
            "Ders ücreti",
            "TRY",
            expectedAmount: 250m,
            collectedAmount: status == PaymentStatus.Paid ? 250m : status == PaymentStatus.PartiallyPaid ? 100m : 0m,
            dueDateUtc,
            collectedOnUtc: null,
            status,
            billingPeriodStartUtc: null,
            billingPeriodEndUtc: null,
            notes: null,
            createdOnUtc: Now.AddDays(-10));

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
