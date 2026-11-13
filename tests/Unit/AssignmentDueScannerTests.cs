using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// M06-3: Son teslim taraması — teslime 24 saat kalanlar için tek bir "yaklaşıyor" uyarısı,
/// vadesi geçip teslim edilmemişler için tek bir "kaçırıldı" uyarısı üretir.
/// </summary>
public sealed class AssignmentDueScannerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkDueSoon_Should_Be_Idempotent()
    {
        var assignment = New(AssignmentStatus.Pending, Now.AddHours(5));

        Assert.True(assignment.MarkDueSoon(Now));
        Assert.False(assignment.MarkDueSoon(Now.AddHours(1)));
        Assert.Equal(Now, assignment.DueSoonNotifiedOnUtc);
        Assert.Single(assignment.DomainEvents.OfType<AssignmentDueSoonDomainEvent>());
    }

    [Fact]
    public void MarkMissed_Should_Be_Idempotent_And_Require_Past_Due()
    {
        var future = New(AssignmentStatus.Pending, Now.AddHours(5));
        Assert.False(future.MarkMissed(Now));
        Assert.Null(future.MissedNotifiedOnUtc);

        var pastDue = New(AssignmentStatus.Pending, Now.AddHours(-1));
        Assert.True(pastDue.MarkMissed(Now));
        Assert.False(pastDue.MarkMissed(Now.AddHours(1)));
        Assert.Equal(Now, pastDue.MissedNotifiedOnUtc);
        Assert.Single(pastDue.DomainEvents.OfType<AssignmentMissedDomainEvent>());
    }

    [Theory]
    [InlineData(AssignmentStatus.InProgress)]
    [InlineData(AssignmentStatus.Completed)]
    [InlineData(AssignmentStatus.Approved)]
    [InlineData(AssignmentStatus.Cancelled)]
    public void Submitted_Or_Closed_Assignment_Should_Not_Raise_Events(AssignmentStatus status)
    {
        var dueSoon = New(status, Now.AddHours(3));
        Assert.False(dueSoon.MarkDueSoon(Now));

        var pastDue = New(status, Now.AddHours(-3));
        Assert.False(pastDue.MarkMissed(Now));
    }

    [Fact]
    public async Task Scanner_Should_Emit_Each_Warning_Once()
    {
        await using var dbContext = NewDbContext();
        dbContext.Assignments.AddRange(
            New(AssignmentStatus.Pending, Now.AddHours(5)),                 // yaklaşıyor
            New(AssignmentStatus.ReturnedForRevision, Now.AddHours(20)),    // yaklaşıyor
            New(AssignmentStatus.Pending, Now.AddDays(3)),                  // penceresi gelmedi
            New(AssignmentStatus.Pending, Now.AddHours(-2)),                // kaçırıldı
            New(AssignmentStatus.InProgress, Now.AddHours(-2)),             // teslim edilmiş → yok
            New(AssignmentStatus.Approved, Now.AddHours(1)),                // kapanmış → yok
            New(AssignmentStatus.Pending, dueDateUtc: null));               // tarihsiz → yok
        await dbContext.SaveChangesAsync();

        var processor = new AssignmentDueScanProcessor(dbContext, new FixedClock(Now));

        var firstRun = await processor.ScanAsync();
        var secondRun = await processor.ScanAsync();

        Assert.Equal(2, firstRun.DueSoonCount);
        Assert.Equal(1, firstRun.MissedCount);
        Assert.Equal(0, secondRun.DueSoonCount);
        Assert.Equal(0, secondRun.MissedCount);

        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentDueSoonDomainEvent)));
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentMissedDomainEvent)));
    }

    [Fact]
    public async Task Scanner_Should_Warn_Then_Mark_Missed_For_Same_Assignment()
    {
        await using var dbContext = NewDbContext();
        var dueAt = Now.AddHours(6);
        dbContext.Assignments.Add(New(AssignmentStatus.Pending, dueAt));
        await dbContext.SaveChangesAsync();

        var warnRun = await new AssignmentDueScanProcessor(dbContext, new FixedClock(Now)).ScanAsync();
        var missedRun = await new AssignmentDueScanProcessor(dbContext, new FixedClock(dueAt.AddMinutes(30))).ScanAsync();

        Assert.Equal(1, warnRun.DueSoonCount);
        Assert.Equal(0, warnRun.MissedCount);
        Assert.Equal(0, missedRun.DueSoonCount);
        Assert.Equal(1, missedRun.MissedCount);
    }

    private static AssignmentsDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AssignmentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new JsonDomainEventMapper());

    private static Assignment New(AssignmentStatus status, DateTime? dueDateUtc) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Ödev", null, dueDateUtc, status, null, Now.AddDays(-5), null);

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
