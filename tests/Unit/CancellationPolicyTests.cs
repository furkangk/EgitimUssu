using System.Text.Json;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

public sealed class CancellationPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(3, true, true, 100)]
    [InlineData(48, true, true, 0)]
    [InlineData(24, true, true, 0)]
    [InlineData(-1, true, true, 0)]
    [InlineData(3, false, true, 0)]
    [InlineData(3, true, false, 0)]
    public void Notice_window_and_waiver_bound_charge(int hours, bool student, bool allow, decimal expected)
        => Assert.Equal(expected, new TeacherCancellationPolicy(24, 100).Charge(Now.AddHours(hours), Now, 100, student, allow));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(24, 0)]
    public void Client_cannot_force_charge_when_policy_is_disabled(int hours, decimal percent)
        => Assert.Equal(0m, new TeacherCancellationPolicy(hours, percent).Charge(Now.AddHours(3), Now, 100, true, true));

    [Fact]
    public void Charge_rounds_to_currency_minor_unit()
        => Assert.Equal(33.34m, new TeacherCancellationPolicy(24, 33.335m).Charge(Now.AddHours(3), Now, 100, true, true));

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(721, 100)]
    [InlineData(24, -1)]
    [InlineData(24, 101)]
    public void Invalid_teacher_policy_is_rejected(int hours, decimal percent)
        => Assert.True(Teacher().SetCancellationPolicy(new CancellationPolicy(hours, percent), Now).IsFailure);

    [Fact]
    public void Policy_is_saved_and_disabled_by_default()
    {
        var teacher = Teacher();
        Assert.Equal(0, teacher.CancellationNoticeHours);
        Assert.Equal(0, teacher.CancellationChargePercent);
        Assert.True(teacher.SetCancellationPolicy(new(24, 50), Now).IsSuccess);
        Assert.Equal(24, teacher.CancellationNoticeHours);
        Assert.Equal(50, teacher.CancellationChargePercent);
    }

    [Fact]
    public void Future_occurrence_cannot_be_completed_even_with_past_actual_times()
    {
        var session = Session(Guid.NewGuid(), Now.AddHours(3));
        var result = session.Complete(Now.AddHours(-2), Now.AddHours(-1), StudentAttendanceStatus.Attended, "Konu", null, null, true, Now);
        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.future_occurrence", result.Error.Code);
        Assert.Equal(LessonSessionStatus.Planned, session.Status);
    }

    [Theory]
    [InlineData("Single")]
    [InlineData("ThisAndFuture")]
    [InlineData("All")]
    public async Task Cancellation_cleans_only_matching_unfinished_sessions(string scope)
    {
        await using var db = new LessonSessionsDbContext(new DbContextOptionsBuilder<LessonSessionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new NoOpDomainEventMapper());
        var schedule = Guid.NewGuid();
        var keys = new[] { Now, Now.AddDays(7), Now.AddDays(14) };
        db.LessonSessions.AddRange(keys.Select(key => Session(schedule, key)));
        await db.SaveChangesAsync();
        var handler = new LessonCancelledSessionHandler(db, new Clock());
        var envelope = Event("LessonCancellationAppliedDomainEvent", new LessonCancellationApplied(schedule, null, Guid.Empty, scope, keys[1], keys[1], Now));
        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);
        var sessions = await db.LessonSessions.OrderBy(item => item.PlannedStartAtUtc).ToArrayAsync();
        Assert.Equal(scope == "All" ? LessonSessionStatus.Cancelled : LessonSessionStatus.Planned, sessions[0].Status);
        Assert.Equal(LessonSessionStatus.Cancelled, sessions[1].Status);
        Assert.Equal(scope == "Single" ? LessonSessionStatus.Planned : LessonSessionStatus.Cancelled, sessions[2].Status);
    }

    [Theory]
    [InlineData(0, PaymentStatus.Pending, true)]
    [InlineData(0, PaymentStatus.Overdue, true)]
    [InlineData(20, PaymentStatus.PartiallyPaid, false)]
    [InlineData(100, PaymentStatus.Paid, false)]
    public void Cancellation_preserves_collections(decimal collected, PaymentStatus status, bool cancelled)
    {
        var record = new PaymentRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, BillingItemType.LessonFee,
            "Ders", "TRY", 100, collected, Now, null, status, null, null, null, Now);
        Assert.Equal(cancelled, record.CancelOutstanding(Now));
        Assert.Equal(cancelled ? PaymentStatus.Cancelled : status, record.Status);
        Assert.Equal(collected, record.CollectedAmount);
    }

    private static TeacherProfile Teacher() => new(Guid.NewGuid(), Guid.NewGuid(), "Öğretmen", "Matematik", "İstanbul", "Kadıköy",
        null, null, TeacherLessonFormat.Online, 1, "Lisans", 100, "TRY", false, null, Now);
    private static LessonSession Session(Guid schedule, DateTime key) => new(Guid.NewGuid(), schedule, Guid.NewGuid(), Guid.NewGuid(),
        "Matematik", key, null, null, null, StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned, "", null, null, Now, null, key);
    private static IntegrationEvent Event(string name, object payload) => new(Guid.NewGuid(), Now, name, "Scheduling",
        JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));
    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
}
