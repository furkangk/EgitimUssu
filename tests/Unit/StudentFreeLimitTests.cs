using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentFreeLimitTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class CountingLinkRepo : ITeacherStudentLinkRepository
    {
        private readonly int _count;
        public CountingLinkRepo(int count) => _count = count;
        public int AddedCount { get; private set; }

        public Task AddAsync(TeacherStudentLink link, CancellationToken ct)
        {
            AddedCount++;
            return Task.CompletedTask;
        }

        public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TeacherStudentLink?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct) => Task.FromResult(_count);
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeStudentProfileRepository : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct) => Task.FromResult<StudentProfile?>(null);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<StudentProfile?>(null);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static CreateStudentProfileCommand ManualCommand()
        => new(
            UserId: null,
            CreatedByTeacherUserId: TeacherId,
            ParentUserId: null,
            FullName: "Ali Veli",
            GradeLevel: "9",
            ContactEmail: null,
            ContactPhone: null,
            GoalSummary: null,
            LevelNotes: null,
            Origin: StudentOrigin.TeacherManaged,
            Subjects: []);

    [Fact]
    public async Task Create_TeacherManaged_AtLimit_Fails()
    {
        var handler = new CreateStudentProfileCommandHandler(
            new FakeStudentProfileRepository(),
            new CountingLinkRepo(5),
            new FakeIds(),
            new FakeClock());

        var result = await handler.Handle(ManualCommand(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("students.free_limit_reached", result.Error.Code);
    }

    [Fact]
    public async Task Create_TeacherManaged_UnderLimit_CreatesLink()
    {
        var linkRepo = new CountingLinkRepo(4);
        var handler = new CreateStudentProfileCommandHandler(
            new FakeStudentProfileRepository(),
            linkRepo,
            new FakeIds(),
            new FakeClock());

        var result = await handler.Handle(ManualCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, linkRepo.AddedCount);
    }
}
