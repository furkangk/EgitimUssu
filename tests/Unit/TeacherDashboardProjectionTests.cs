using System.Text.Json;
using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P07 Task 2 — öğretmen dashboard projeksiyonu. Projeksiyon yalnız integration event'lerden beslenir;
/// her handler idempotent olmalı ve başka öğretmenin satırını etkilememelidir.
/// </summary>
public sealed class TeacherDashboardProjectionTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TodayUtc = new(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);

    private static ReportingDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ReportingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new NoOpDomainEventMapper());

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private static IntegrationEvent Event(string name, string module, object payload, Guid? eventId = null) =>
        new(eventId ?? Guid.NewGuid(), Now, name, module,
            JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));

    private static async Task<TeacherDashboardProjection?> LoadAsync(ReportingDbContext db, Guid teacherUserId) =>
        await db.TeacherDashboardProjections
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.TeacherUserId == teacherUserId);

    // --- Ödevler -------------------------------------------------------------------------------

    [Fact]
    public async Task Created_Assignment_Should_Increase_Pending_Count()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();

        await new AssignmentOpenedProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("AssignmentCreatedDomainEvent", "Assignments", new
            {
                AssignmentId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                TeacherUserId = teacher,
                CreatedOnUtc = Now
            }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(1, projection!.PendingAssignmentCount);
    }

    [Fact]
    public async Task Completed_Assignment_Should_Decrease_Pending_Count()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        await new AssignmentOpenedProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("AssignmentCreatedDomainEvent", "Assignments", new
            {
                AssignmentId = assignmentId,
                StudentId = Guid.NewGuid(),
                TeacherUserId = teacher,
                CreatedOnUtc = Now
            }));
        await new AssignmentClosedProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("AssignmentCompletedDomainEvent", "Assignments", new
            {
                AssignmentId = assignmentId,
                StudentId = Guid.NewGuid(),
                TeacherUserId = teacher,
                CompletedOnUtc = Now
            }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(0, projection!.PendingAssignmentCount);
    }

    [Fact]
    public async Task Approved_Assignment_Should_Also_Decrease_Pending_Count()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        await new AssignmentOpenedProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("AssignmentCreatedDomainEvent", "Assignments", new
            {
                AssignmentId = assignmentId,
                StudentId = Guid.NewGuid(),
                TeacherUserId = teacher,
                CreatedOnUtc = Now
            }));
        await new AssignmentClosedProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("AssignmentApprovedDomainEvent", "Assignments", new
            {
                AssignmentId = assignmentId,
                StudentId = Guid.NewGuid(),
                TeacherUserId = teacher,
                OnUtc = Now
            }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(0, projection!.PendingAssignmentCount);
    }

    // --- Ödemeler ------------------------------------------------------------------------------

    [Fact]
    public async Task Overdue_Payment_Should_Increase_Count_And_Total()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();

        await new PaymentOverdueProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("PaymentBecameOverdueDomainEvent", "Payments", new
            {
                PaymentRecordId = Guid.NewGuid(),
                TeacherUserId = teacher,
                StudentId = Guid.NewGuid(),
                OutstandingAmount = 750.50m,
                Currency = "TRY",
                DueDateUtc = TodayUtc.AddDays(-3),
                MarkedOnUtc = Now
            }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(1, projection!.OverduePaymentCount);
        Assert.Equal(750.50m, projection.OverdueAmountTotal);
        Assert.Equal("TRY", projection.OverdueCurrency);
    }

    [Fact]
    public async Task Settled_Payment_Should_Leave_The_Overdue_Set()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await new PaymentOverdueProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("PaymentBecameOverdueDomainEvent", "Payments", new
            {
                PaymentRecordId = paymentId,
                TeacherUserId = teacher,
                StudentId = Guid.NewGuid(),
                OutstandingAmount = 400m,
                Currency = "TRY",
                DueDateUtc = TodayUtc.AddDays(-1),
                MarkedOnUtc = Now
            }));
        await new PaymentSettledProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("PaymentRecordUpdatedDomainEvent", "Payments", new
            {
                PaymentRecordId = paymentId,
                TeacherUserId = teacher,
                StudentId = Guid.NewGuid(),
                PreviousStatus = 4,
                CurrentStatus = 3,
                PreviousCollectedAmount = 0m,
                CurrentCollectedAmount = 400m,
                UpdatedOnUtc = Now
            }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(0, projection!.OverduePaymentCount);
        Assert.Equal(0m, projection.OverdueAmountTotal);
    }

    [Fact]
    public async Task Payment_Still_Overdue_After_Update_Should_Stay_Counted()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await new PaymentOverdueProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("PaymentBecameOverdueDomainEvent", "Payments", new
            {
                PaymentRecordId = paymentId,
                TeacherUserId = teacher,
                StudentId = Guid.NewGuid(),
                OutstandingAmount = 400m,
                Currency = "TRY",
                DueDateUtc = TodayUtc.AddDays(-1),
                MarkedOnUtc = Now
            }));
        await new PaymentSettledProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("PaymentRecordUpdatedDomainEvent", "Payments", new
            {
                PaymentRecordId = paymentId,
                TeacherUserId = teacher,
                StudentId = Guid.NewGuid(),
                PreviousStatus = 4,
                CurrentStatus = 4,
                PreviousCollectedAmount = 0m,
                CurrentCollectedAmount = 100m,
                UpdatedOnUtc = Now
            }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(1, projection!.OverduePaymentCount);
    }

    // --- Öğrenciler ----------------------------------------------------------------------------

    [Fact]
    public async Task Accepted_Student_Link_Should_Increase_Active_Student_Count()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var handler = new TeacherStudentLinkProjectionHandler(db, new FixedClock());

        await handler.HandleAsync(Event("TeacherStudentLinkAcceptedDomainEvent", "Students", new
        {
            LinkId = Guid.NewGuid(),
            TeacherUserId = teacher,
            StudentId = studentId,
            OnUtc = Now
        }));
        await handler.HandleAsync(Event("TeacherStudentLinkAcceptedDomainEvent", "Students", new
        {
            LinkId = Guid.NewGuid(),
            TeacherUserId = teacher,
            StudentId = studentId,
            OnUtc = Now
        }));

        var projection = await LoadAsync(db, teacher);
        Assert.Equal(1, projection!.ActiveStudentCount);
    }

    // --- İzolasyon -----------------------------------------------------------------------------

    [Fact]
    public async Task Another_Teacher_Should_Not_Be_Affected()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var otherTeacher = Guid.NewGuid();
        var handler = new AssignmentOpenedProjectionHandler(db, new FixedClock());

        await handler.HandleAsync(Event("AssignmentCreatedDomainEvent", "Assignments", new
        {
            AssignmentId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            TeacherUserId = teacher,
            CreatedOnUtc = Now
        }));

        Assert.Equal(1, (await LoadAsync(db, teacher))!.PendingAssignmentCount);
        Assert.Null(await LoadAsync(db, otherTeacher));
    }

    [Fact]
    public void MarkApplied_Should_Track_The_Last_Event()
    {
        var projection = new TeacherDashboardProjection(Guid.NewGuid());
        var eventId = Guid.NewGuid();

        projection.MarkApplied(eventId, Now);

        Assert.Equal(eventId, projection.LastAppliedEventId);
        Assert.Equal(Now, projection.UpdatedOnUtc);
    }

    // --- Sorgu + yetki ---------------------------------------------------------------------------

    [Fact]
    public async Task Query_Should_Return_Todays_Counts_From_The_Projection()
    {
        await using var db = NewContext();
        var teacher = Guid.NewGuid();
        var projection = new TeacherDashboardProjection(teacher);
        projection.TrackLesson(Guid.NewGuid(), TodayUtc.AddHours(14));
        db.TeacherDashboardProjections.Add(projection);
        await db.SaveChangesAsync();
        await new PaymentOverdueProjectionHandler(db, new FixedClock()).HandleAsync(
            Event("PaymentBecameOverdueDomainEvent", "Payments", new
            {
                PaymentRecordId = Guid.NewGuid(),
                TeacherUserId = teacher,
                StudentId = Guid.NewGuid(),
                OutstandingAmount = 250m,
                Currency = "TRY",
                DueDateUtc = TodayUtc.AddDays(-2),
                MarkedOnUtc = Now
            }));

        var handler = new GetTeacherDashboardQueryHandler(
            new TeacherDashboardProjectionReader(db), new EgitimUssu.Shared.Infrastructure.LocalCalendar("Europe/Istanbul", new FixedClock()));

        var result = await handler.Handle(new GetTeacherDashboardQuery(teacher), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TodayLessonCount);
        Assert.Equal(1, result.Value.OverduePaymentCount);
        Assert.Equal(250m, result.Value.OverdueAmountTotal);
        Assert.Equal("TRY", result.Value.OverdueCurrency);
        Assert.Equal(0, result.Value.PendingAssignmentCount);
    }

    [Fact]
    public async Task Query_Should_Fail_When_The_Projection_Was_Never_Fed()
    {
        await using var db = NewContext();
        var handler = new GetTeacherDashboardQueryHandler(
            new TeacherDashboardProjectionReader(db), new EgitimUssu.Shared.Infrastructure.LocalCalendar("Europe/Istanbul", new FixedClock()));

        var result = await handler.Handle(new GetTeacherDashboardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("reporting.dashboard_not_ready", result.Error.Code);
    }

    [Fact]
    public async Task Only_The_Teacher_Or_An_Admin_May_Read_The_Dashboard()
    {
        var teacher = Guid.NewGuid();
        var query = new GetTeacherDashboardQuery(teacher);

        var owner = new ReportingAuthorizer(new FakeCurrentUser(teacher.ToString()));
        var admin = new ReportingAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString(), "Admin"));
        var stranger = new ReportingAuthorizer(new FakeCurrentUser(Guid.NewGuid().ToString()));
        var anonymous = new ReportingAuthorizer(new FakeCurrentUser(null));

        Assert.True((await owner.Authorize(query, CancellationToken.None)).IsSuccess);
        Assert.True((await admin.Authorize(query, CancellationToken.None)).IsSuccess);
        Assert.True((await stranger.Authorize(query, CancellationToken.None)).IsFailure);
        Assert.True((await anonymous.Authorize(query, CancellationToken.None)).IsFailure);
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }
}
