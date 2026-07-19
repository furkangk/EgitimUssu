using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class SelfLessonPolicyTests
{
    private static readonly DateTime Start = new(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Validator_rejects_empty_student_and_reversed_time()
    {
        var validator = new CreateSelfLessonCommandValidator();
        var cmd = new CreateSelfLessonCommand(
            Guid.Empty, "", null, Start, Start.AddMinutes(-10),
            "Europe/Istanbul", null, 30, null, null);

        var result = await validator.Validate(cmd, CancellationToken.None);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Validator_accepts_valid_self_lesson()
    {
        var validator = new CreateSelfLessonCommandValidator();
        var cmd = new CreateSelfLessonCommand(
            Guid.NewGuid(), "Matematik", "Türev", Start, Start.AddMinutes(60),
            "Europe/Istanbul", null, 30, "#20A4A9", null);

        var result = await validator.Validate(cmd, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Authorizer_denies_unauthenticated()
    {
        var authorizer = new SelfLessonAuthorizer(
            new FakeCurrentUser { UserId = null },
            new FakeStudentDirectory(Guid.NewGuid()),
            new StubLessonRepository());

        var result = await authorizer.Authorize(
            new CreateSelfLessonCommand(Guid.NewGuid(), "Fizik", null, Start, Start.AddMinutes(60), "Europe/Istanbul", null, 0, null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Authorizer_allows_owner_student()
    {
        var userId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var authorizer = new SelfLessonAuthorizer(
            new FakeCurrentUser { UserId = userId.ToString(), Roles = new[] { "Student" } },
            new FakeStudentDirectory(userId) { StudentId = studentId },
            new StubLessonRepository());

        var result = await authorizer.Authorize(
            new CreateSelfLessonCommand(studentId, "Fizik", null, Start, Start.AddMinutes(60), "Europe/Istanbul", null, 0, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public string? Email { get; init; }
        public bool IsAuthenticated => UserId is not null;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
    }

    private sealed class FakeStudentDirectory : IStudentDirectory
    {
        private readonly Guid _ownerUserId;
        public Guid? StudentId { get; init; }

        public FakeStudentDirectory(Guid ownerUserId) => _ownerUserId = ownerUserId;

        public Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<Guid?>(StudentId is null || StudentId == studentId ? _ownerUserId : null);
    }

    private sealed class StubLessonRepository : ILessonScheduleRepository
    {
        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken) => Task.FromResult<LessonSchedule?>(null);
        public Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<LessonSchedule>>(Array.Empty<LessonSchedule>());
        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Remove(LessonSchedule lessonSchedule) => throw new NotImplementedException();
        public Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Array.Empty<LessonOccurrenceException>());
        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
