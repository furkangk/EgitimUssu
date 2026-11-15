using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

public sealed class AssignmentDueScannerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ILocalCalendar Calendar = new LocalCalendar("Europe/Istanbul", new FixedClock(Now));
    private static DateTime Deadline => Calendar.DayRangeUtc(Calendar.ToLocalDate(Now)).EndUtc.AddTicks(-10);

    [Fact]
    public void MarkDueSoon_Is_Idempotent_And_Event_Uses_Local_End_Of_Day()
    {
        var assignment = New(AssignmentStatus.Pending, Now.AddHours(-1));
        Assert.True(assignment.MarkDueSoon(Now, Calendar));
        Assert.False(assignment.MarkDueSoon(Now.AddHours(1), Calendar));
        Assert.Equal(Now, assignment.DueSoonNotifiedOnUtc);
        Assert.Equal(Deadline, Assert.Single(assignment.DomainEvents.OfType<AssignmentDueSoonDomainEvent>()).DueDateUtc);
    }

    [Fact]
    public void MarkMissed_Is_Idempotent_And_Requires_Local_Deadline()
    {
        var today = New(AssignmentStatus.Pending, Now.AddHours(-1));
        Assert.False(today.MarkMissed(Now, Calendar));
        Assert.Null(today.MissedNotifiedOnUtc);
        var past = New(AssignmentStatus.Pending, Now.AddDays(-1));
        Assert.True(past.MarkMissed(Now, Calendar));
        Assert.False(past.MarkMissed(Now.AddHours(1), Calendar));
        Assert.Equal(Now, past.MissedNotifiedOnUtc);
        Assert.Equal(Deadline.AddDays(-1), Assert.Single(past.DomainEvents.OfType<AssignmentMissedDomainEvent>()).DueDateUtc);
    }

    [Fact]
    public void MarkDueSoon_Does_Not_Warn_Outside_24_Hour_Window()
    {
        var assignment = New(AssignmentStatus.Pending, Now.AddDays(2));
        Assert.False(assignment.MarkDueSoon(Now, Calendar));
        Assert.Null(assignment.DueSoonNotifiedOnUtc);
    }

    [Theory]
    [InlineData(AssignmentStatus.InProgress)]
    [InlineData(AssignmentStatus.Completed)]
    [InlineData(AssignmentStatus.Approved)]
    [InlineData(AssignmentStatus.Cancelled)]
    public void Submitted_Or_Closed_Assignment_Does_Not_Raise_Warnings(AssignmentStatus status)
    {
        Assert.False(New(status, Now).MarkDueSoon(Now, Calendar));
        Assert.False(New(status, Now.AddDays(-1)).MarkMissed(Now, Calendar));
    }

    [Fact]
    public void Returned_Submission_Does_Not_Raise_Either_Warning()
    {
        var assignment = New(AssignmentStatus.Pending, Now);
        Assert.True(assignment.SubmitWork("https://example.test/work", Now.AddHours(-2), Calendar).IsSuccess);
        Assert.True(assignment.ReturnForRevision("Düzelt", Now.AddHours(-1)).IsSuccess);
        Assert.False(assignment.MarkDueSoon(Now, Calendar));
        Assert.False(assignment.MarkMissed(Now.AddDays(1), Calendar));
    }

    [Fact]
    public async Task Scanner_Emits_Each_Warning_Once_And_Excludes_Submitted_And_Dateless()
    {
        await using var db = NewDbContext();
        var returned = New(AssignmentStatus.Pending, Now.AddDays(-1));
        returned.MarkCompleted(Now.AddDays(-2), Calendar);
        returned.ReturnForRevision("Tekrar bak", Now);
        db.Assignments.AddRange(
            New(AssignmentStatus.Pending, Now.AddHours(-1)),
            New(AssignmentStatus.ReturnedForRevision, Now.AddHours(2)),
            New(AssignmentStatus.Pending, Now.AddDays(3)),
            New(AssignmentStatus.Pending, Now.AddDays(-1)),
            New(AssignmentStatus.InProgress, Now.AddDays(-1)),
            New(AssignmentStatus.Approved, Now),
            New(AssignmentStatus.Pending, null), returned);
        await db.SaveChangesAsync();
        var first = await Scan(db, Now);
        Assert.Equal(new AssignmentDueScanResult(2, 1), first);
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Now));
        Assert.Equal(2, await db.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentDueSoonDomainEvent)));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentMissedDomainEvent)));
    }

    [Fact]
    public async Task Scanner_Warns_Then_Marks_Missed_At_Local_Deadline()
    {
        await using var db = NewDbContext();
        db.Assignments.Add(New(AssignmentStatus.Pending, Now));
        await db.SaveChangesAsync();
        Assert.Equal(new AssignmentDueScanResult(1, 0), await Scan(db, Now));
        Assert.Equal(new AssignmentDueScanResult(0, 1), await Scan(db, Deadline));
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Deadline.AddTicks(10)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Old_250_Assignments_Do_Not_Starve_New_DueSoon(bool alreadyMissed)
    {
        await using var db = NewDbContext();
        for (var i = 0; i < 250; i++)
        {
            var old = New(AssignmentStatus.Pending, Now.AddDays(-3));
            if (alreadyMissed) old.MarkMissed(Now.AddDays(-1), Calendar);
            db.Assignments.Add(old);
        }
        var next = New(AssignmentStatus.Pending, Now);
        db.Assignments.Add(next);
        await db.SaveChangesAsync();
        var first = await Scan(db, Now);
        Assert.Equal(1, first.DueSoonCount);
        Assert.Equal(alreadyMissed ? 0 : 200, first.MissedCount);
        Assert.NotNull(next.DueSoonNotifiedOnUtc);
        var second = await Scan(db, Now);
        Assert.Equal(new AssignmentDueScanResult(0, alreadyMissed ? 0 : 50), second);
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Now));
    }

    [Theory]
    [InlineData("Europe/Berlin", 2026, 3, 29)]
    [InlineData("Europe/Berlin", 2026, 10, 25)]
    public async Task Scanner_Uses_Calendar_Day_Ranges_Through_Dst(string zone, int year, int month, int day)
    {
        var cal = new LocalCalendar(zone, new FixedClock(Now));
        var date = new DateOnly(year, month, day);
        var range = cal.DayRangeUtc(date);
        var deadline = range.EndUtc.AddTicks(-10);
        await using var db = NewDbContext();
        db.Assignments.Add(New(AssignmentStatus.Pending, range.StartUtc));
        await db.SaveChangesAsync();
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, deadline.AddHours(-24).AddTicks(-10), cal));
        Assert.Equal(new AssignmentDueScanResult(1, 0), await Scan(db, deadline.AddHours(-24), cal));
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, deadline.AddTicks(-10), cal));
        Assert.Equal(new AssignmentDueScanResult(0, 1), await Scan(db, deadline, cal));
    }

    private static Task<AssignmentDueScanResult> Scan(AssignmentsDbContext db, DateTime now, ILocalCalendar? calendar = null)
        => new AssignmentDueScanProcessor(db, new FixedClock(now), calendar ?? Calendar).ScanAsync();

    private static AssignmentsDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<AssignmentsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new JsonDomainEventMapper());

    private static Assignment New(AssignmentStatus status, DateTime? dueDateUtc) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Ödev", null, dueDateUtc, status, null, Now.AddDays(-5), null);

    private sealed record FixedClock(DateTime UtcNow) : IClock;
}
