using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonSessionTests
{
    private static readonly DateTime Planned = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    private static LessonSession NewSession() => new(
        Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Matematik",
        Planned, null, null, null,
        StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned, "Konu", null, null, Planned, null);

    [Fact]
    public void Complete_AbsentChargeable_IsRecorded()
    {
        var session = NewSession();

        var result = session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Absent,
            "Konu", null, "gelmedi", isChargeable: true, Planned.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonSessionStatus.Completed, session.Status);
        Assert.Equal(StudentAttendanceStatus.Absent, session.AttendanceStatus);
        Assert.True(session.IsChargeable);
    }

    // --- M05-1: başlat/iptal geçişleri ---

    [Fact]
    public void Start_Should_Move_Planned_To_InProgress()
    {
        var session = NewSession();

        var result = session.Start(Planned.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonSessionStatus.InProgress, session.Status);
        Assert.Equal(Planned.AddMinutes(2), session.ActualStartAtUtc);
    }

    [Fact]
    public void Second_Start_Should_Fail_With_Invalid_Transition()
    {
        var session = NewSession();
        Assert.True(session.Start(Planned).IsSuccess);

        var result = session.Start(Planned.AddMinutes(5));

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_transition", result.Error.Code);
        // İlk başlangıç saati korunur.
        Assert.Equal(Planned, session.ActualStartAtUtc);
    }

    [Fact]
    public void Cancel_Should_Move_Planned_To_Cancelled_With_Reason()
    {
        var session = NewSession();

        var result = session.Cancel("Öğrenci hastalandı", Planned.AddMinutes(-30));

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonSessionStatus.Cancelled, session.Status);
        Assert.Equal("Öğrenci hastalandı", session.CancellationReason);
    }

    [Fact]
    public void Cancel_Should_Be_Allowed_While_InProgress()
    {
        var session = NewSession();
        Assert.True(session.Start(Planned).IsSuccess);

        var result = session.Cancel(null, Planned.AddMinutes(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(LessonSessionStatus.Cancelled, session.Status);
    }

    [Fact]
    public void Cancel_After_Complete_Should_Fail()
    {
        var session = NewSession();
        Assert.True(session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Attended,
            "Konu", null, null, isChargeable: true, Planned.AddHours(1)).IsSuccess);

        var result = session.Cancel("vazgeçtim", Planned.AddHours(2));

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_transition", result.Error.Code);
        Assert.Equal(LessonSessionStatus.Completed, session.Status);
    }

    // --- M05-3: zaman aralığı doğrulaması ---

    [Fact]
    public void Complete_With_Reversed_Time_Range_Should_Fail()
    {
        var session = NewSession();

        var result = session.Complete(Planned.AddHours(1), Planned, StudentAttendanceStatus.Attended,
            "Konu", null, null, isChargeable: true, Planned.AddHours(2));

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_time_range", result.Error.Code);
        Assert.Equal(LessonSessionStatus.Planned, session.Status);
        Assert.Null(session.CompletedOnUtc);
    }

    [Fact]
    public void Complete_With_Equal_Start_And_End_Should_Fail()
    {
        var session = NewSession();

        var result = session.Complete(Planned, Planned, StudentAttendanceStatus.Attended,
            "Konu", null, null, isChargeable: true, Planned.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_time_range", result.Error.Code);
    }

    [Fact]
    public void Complete_After_Cancel_Should_Fail()
    {
        var session = NewSession();
        Assert.True(session.Cancel("iptal", Planned.AddMinutes(-10)).IsSuccess);

        var result = session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Attended,
            "Konu", null, null, isChargeable: true, Planned.AddHours(1));

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_transition", result.Error.Code);
        Assert.Equal(LessonSessionStatus.Cancelled, session.Status);
    }

    [Fact]
    public void Completed_Session_Can_Be_Completed_Again_To_Fix_Notes()
    {
        // Öğretmen tamamlanmış dersin konusunu/notunu düzeltebilir (roles/ogretmen §5.1 #12).
        var session = NewSession();
        Assert.True(session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Attended,
            "Konu", null, null, isChargeable: true, Planned.AddHours(1)).IsSuccess);

        var result = session.Complete(Planned, Planned.AddHours(1), StudentAttendanceStatus.Attended,
            "Duzeltilmis konu", "icerik", "not", isChargeable: true, Planned.AddHours(2));

        Assert.True(result.IsSuccess);
        Assert.Equal("Duzeltilmis konu", session.TopicTitle);
    }

    [Fact]
    public void Start_After_Cancel_Should_Fail()
    {
        var session = NewSession();
        Assert.True(session.Cancel(null, Planned.AddMinutes(-5)).IsSuccess);

        var result = session.Start(Planned);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_transition", result.Error.Code);
    }

    // --- M05-4: toplantı/kayıt bağlantısı ---

    [Fact]
    public void MeetingUrl_Can_Be_Set_And_Cleared()
    {
        var session = NewSession();

        session.SetMeetingUrl("  https://meet.example.com/abc  ");
        Assert.Equal("https://meet.example.com/abc", session.MeetingUrl);

        session.SetMeetingUrl("   ");
        Assert.Null(session.MeetingUrl);
    }

    [Fact]
    public void RecordingUrl_Can_Be_Set()
    {
        var session = NewSession();

        session.SetRecordingUrl("https://kayit.example.com/1");

        Assert.Equal("https://kayit.example.com/1", session.RecordingUrl);
    }

    [Fact]
    public void Cancel_Should_Trim_And_Null_Empty_Reason()
    {
        var session = NewSession();

        Assert.True(session.Cancel("   ", Planned).IsSuccess);

        Assert.Null(session.CancellationReason);
    }

    // --- Handler + yetki (M05-1) ---

    [Fact]
    public async Task Start_Handler_Should_Persist_Transition()
    {
        var session = NewSession();
        var repository = new FakeLessonSessionRepository(session);
        var handler = new StartLessonSessionCommandHandler(repository, new FixedClock(Planned.AddMinutes(1)));

        var result = await handler.Handle(new StartLessonSessionCommand(session.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("InProgress", result.Value!.Status);
        Assert.Equal(Planned.AddMinutes(1), session.ActualStartAtUtc);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Start_Handler_Should_Not_Save_On_Invalid_Transition()
    {
        var session = NewSession();
        Assert.True(session.Start(Planned).IsSuccess);
        var repository = new FakeLessonSessionRepository(session);
        var handler = new StartLessonSessionCommandHandler(repository, new FixedClock(Planned.AddMinutes(5)));

        var result = await handler.Handle(new StartLessonSessionCommand(session.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_transition", result.Error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Start_Handler_Should_Return_Not_Found_For_Unknown_Session()
    {
        var repository = new FakeLessonSessionRepository(null);
        var handler = new StartLessonSessionCommandHandler(repository, new FixedClock(Planned));

        var result = await handler.Handle(new StartLessonSessionCommand(Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.not_found", result.Error.Code);
    }

    [Fact]
    public async Task Cancel_Handler_Should_Persist_Reason()
    {
        var session = NewSession();
        var repository = new FakeLessonSessionRepository(session);
        var handler = new CancelLessonSessionCommandHandler(repository, new FixedClock(Planned));

        var result = await handler.Handle(new CancelLessonSessionCommand(session.Id, "Öğrenci hastalandı"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal("Öğrenci hastalandı", result.Value.CancellationReason);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Complete_Handler_Should_Fail_After_Cancel()
    {
        var session = NewSession();
        Assert.True(session.Cancel(null, Planned).IsSuccess);
        var repository = new FakeLessonSessionRepository(session);
        var handler = new CompleteLessonSessionCommandHandler(repository, new FixedClock(Planned.AddHours(1)));

        var result = await handler.Handle(
            new CompleteLessonSessionCommand(session.Id, Planned, Planned.AddHours(1),
                StudentAttendanceStatus.Attended, "Konu", null, null, IsChargeable: true),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.invalid_transition", result.Error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Start_Should_Be_Forbidden_For_Other_Teacher()
    {
        var session = NewSession();
        var authorizer = new LessonSessionCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid(), ["Teacher"]), new FakeLessonSessionRepository(session));

        var result = await authorizer.Authorize(new StartLessonSessionCommand(session.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Cancel_Should_Be_Allowed_For_Owner_Teacher()
    {
        var session = NewSession();
        var authorizer = new LessonSessionCommandAuthorizer(
            new FakeCurrentUser(session.TeacherUserId, ["Teacher"]), new FakeLessonSessionRepository(session));

        var result = await authorizer.Authorize(new CancelLessonSessionCommand(session.Id, null), default);

        Assert.True(result.IsSuccess);
    }

    private sealed class FakeLessonSessionRepository : ILessonSessionRepository
    {
        private readonly LessonSession? _session;

        public FakeLessonSessionRepository(LessonSession? session) => _session = session;

        public int SaveCount { get; private set; }

        public Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult(_session is not null && _session.Id == lessonSessionId ? _session : null);

        public Task<LessonSession?> GetByScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult(_session is not null && _session.LessonScheduleId == lessonScheduleId ? _session : null);

        public Task<IReadOnlyCollection<LessonSession>> ListAsync(
            Guid? teacherUserId, Guid? studentId, DateTime? dateFromUtc, DateTime? dateToUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSession>>(_session is null ? [] : [_session]);

        public Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(Guid? userId, string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId?.ToString();

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsAuthenticated => userId is not null;
    }
}
