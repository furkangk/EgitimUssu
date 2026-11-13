using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P06 Task 9 / M03-2: Öğrenci branşlarının profil güncellemesinden bağımsız yönetimi.
/// Kritik kural: aynı branş iki kez eklenemez (büyük/küçük harf duyarsız).
/// </summary>
public sealed class StudentProfileSubjectsTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AddSubject_Should_Append_And_Return_Id()
    {
        var profile = NewProfile();
        var repository = new FakeRepository(profile);
        var handler = new AddStudentSubjectCommandHandler(repository, new FakeIds(), new FakeClock());

        var result = await handler.Handle(
            new AddStudentSubjectCommand(profile.Id, " Matematik ", "TYT"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal("Matematik", result.Value.Subject);
        Assert.Single(profile.Subjects);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AddSubject_Should_Reject_Duplicate_Case_Insensitive()
    {
        var profile = NewProfile();
        profile.AddSubject(Guid.NewGuid(), "Matematik", null, Now);
        var handler = new AddStudentSubjectCommandHandler(new FakeRepository(profile), new FakeIds(), new FakeClock());

        var result = await handler.Handle(
            new AddStudentSubjectCommand(profile.Id, "matematik", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.duplicate_subject", result.Error.Code);
        Assert.Single(profile.Subjects);
    }

    [Fact]
    public async Task AddSubject_Should_Fail_When_Profile_Missing()
    {
        var handler = new AddStudentSubjectCommandHandler(new FakeRepository(null), new FakeIds(), new FakeClock());

        var result = await handler.Handle(
            new AddStudentSubjectCommand(Guid.NewGuid(), "Fizik", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.profile_not_found", result.Error.Code);
    }

    [Fact]
    public async Task RemoveSubject_Should_Delete_Existing()
    {
        var profile = NewProfile();
        var subjectId = Guid.NewGuid();
        profile.AddSubject(subjectId, "Fizik", null, Now);
        var repository = new FakeRepository(profile);
        var handler = new RemoveStudentSubjectCommandHandler(repository, new FakeClock());

        var result = await handler.Handle(
            new RemoveStudentSubjectCommand(profile.Id, subjectId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(profile.Subjects);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RemoveSubject_Should_Fail_When_Subject_Missing()
    {
        var profile = NewProfile();
        var repository = new FakeRepository(profile);
        var handler = new RemoveStudentSubjectCommandHandler(repository, new FakeClock());

        var result = await handler.Handle(
            new RemoveStudentSubjectCommand(profile.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.subject_not_found", result.Error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Subject_Authorizer_Should_Allow_Owning_Teacher()
    {
        var profile = NewProfile();
        var authorizer = new StudentSubjectCommandAuthorizer(
            new FakeCurrentUser(TeacherId.ToString(), "Teacher"),
            new FakeRepository(profile));

        var result = await authorizer.Authorize(
            new AddStudentSubjectCommand(profile.Id, "Fizik", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Subject_Authorizer_Should_Allow_Student_Owner()
    {
        var profile = NewProfile();
        var authorizer = new StudentSubjectCommandAuthorizer(
            new FakeCurrentUser(StudentUserId.ToString(), "Student"),
            new FakeRepository(profile));

        var result = await authorizer.Authorize(
            new RemoveStudentSubjectCommand(profile.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Subject_Authorizer_Should_Forbid_Other_Teacher()
    {
        var profile = NewProfile();
        var authorizer = new StudentSubjectCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid().ToString(), "Teacher"),
            new FakeRepository(profile));

        var result = await authorizer.Authorize(
            new AddStudentSubjectCommand(profile.Id, "Fizik", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    private static StudentProfile NewProfile()
        => new(
            Guid.NewGuid(),
            StudentUserId,
            TeacherId,
            null,
            "Ali Veli",
            "9",
            null,
            null,
            null,
            null,
            StudentOrigin.TeacherManaged,
            true,
            Now);

    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FakeIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class FakeRepository(StudentProfile? profile) : IStudentProfileRepository
    {
        public int SaveCount { get; private set; }

        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct) => Task.FromResult(profile);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(profile);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile p, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
