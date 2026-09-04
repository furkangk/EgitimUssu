using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>M05-2: Planlı dersten (M04) ders oturumu türetme.</summary>
public sealed class CreateSessionFromScheduleTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ScheduleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TeacherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StudentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static LessonScheduleDetails Schedule(string status = "Planned", string? meetingUrl = "https://meet.example.com/abc")
        => new(ScheduleId, TeacherId, StudentId, Now.AddHours(1), Now.AddHours(2), "Matematik", meetingUrl, status);

    [Fact]
    public async Task Should_Derive_Session_From_Schedule()
    {
        var repository = new FakeRepository();
        var handler = new CreateSessionFromScheduleCommandHandler(
            repository, new FakeScheduleDirectory(Schedule()), new FixedIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(ScheduleId, response.LessonScheduleId);
        Assert.Equal(TeacherId, response.TeacherUserId);
        Assert.Equal(StudentId, response.StudentId);
        Assert.Equal("Matematik", response.Subject);
        Assert.Equal(Now.AddHours(1), response.PlannedStartAtUtc);
        Assert.Equal("https://meet.example.com/abc", response.MeetingUrl);
        Assert.Equal("Planned", response.Status);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Second_Call_Should_Return_Existing_Session_Without_Creating()
    {
        var repository = new FakeRepository();
        var directory = new FakeScheduleDirectory(Schedule());
        var handler = new CreateSessionFromScheduleCommandHandler(
            repository, directory, new FixedIdGenerator(), new FixedClock(Now));

        var first = await handler.Handle(new CreateSessionFromScheduleCommand(ScheduleId), default);
        var second = await handler.Handle(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Cancelled_Schedule_Should_Not_Produce_Session()
    {
        var repository = new FakeRepository();
        var handler = new CreateSessionFromScheduleCommandHandler(
            repository, new FakeScheduleDirectory(Schedule(status: "Cancelled")), new FixedIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.schedule_not_derivable", result.Error.Code);
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task Unknown_Schedule_Should_Return_Not_Found()
    {
        var repository = new FakeRepository();
        var handler = new CreateSessionFromScheduleCommandHandler(
            repository, new FakeScheduleDirectory(null), new FixedIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.schedule_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Session_Without_Meeting_Url_Should_Stay_Null()
    {
        var repository = new FakeRepository();
        var handler = new CreateSessionFromScheduleCommandHandler(
            repository, new FakeScheduleDirectory(Schedule(meetingUrl: null)), new FixedIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.MeetingUrl);
    }

    [Fact]
    public async Task Derivation_Should_Be_Forbidden_For_Other_Teacher()
    {
        var authorizer = new CreateSessionFromScheduleCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid(), ["Teacher"]), new FakeScheduleDirectory(Schedule()));

        var result = await authorizer.Authorize(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Derivation_Should_Be_Allowed_For_Owner_Teacher()
    {
        var authorizer = new CreateSessionFromScheduleCommandAuthorizer(
            new FakeCurrentUser(TeacherId, ["Teacher"]), new FakeScheduleDirectory(Schedule()));

        var result = await authorizer.Authorize(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unknown_Schedule_Should_Not_Leak_Existence_To_Authorizer()
    {
        var authorizer = new CreateSessionFromScheduleCommandAuthorizer(
            new FakeCurrentUser(TeacherId, ["Teacher"]), new FakeScheduleDirectory(null));

        var result = await authorizer.Authorize(new CreateSessionFromScheduleCommand(ScheduleId), default);

        Assert.True(result.IsFailure);
        Assert.Equal("lesson_sessions.schedule_not_found", result.Error.Code);
    }

    private sealed class FakeScheduleDirectory(LessonScheduleDetails? details) : ILessonScheduleDirectory
    {
        public Task<LessonScheduleDetails?> GetAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult(details is not null && details.LessonScheduleId == lessonScheduleId ? details : null);

        public Task<IReadOnlyCollection<TeacherBusyInterval>> ListTeacherBusyIntervalsAsync(
            Guid teacherUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<TeacherBusyInterval>>(Array.Empty<TeacherBusyInterval>());

    }

    private sealed class FakeRepository : ILessonSessionRepository
    {
        private readonly List<LessonSession> _sessions = [];

        public int AddCount { get; private set; }

        public int SaveCount { get; private set; }

        public Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.FirstOrDefault(x => x.Id == lessonSessionId));

        public Task<LessonSession?> GetByScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult(_sessions.FirstOrDefault(x => x.LessonScheduleId == lessonScheduleId));

        public Task<IReadOnlyCollection<LessonSession>> ListAsync(
            Guid? teacherUserId, Guid? studentId, DateTime? dateFromUtc, DateTime? dateToUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSession>>(_sessions);

        public Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken)
        {
            _sessions.Add(lessonSession);
            AddCount++;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
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
