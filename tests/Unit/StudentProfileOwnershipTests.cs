using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 3 / N-23: Profilin hangi hesaba ait olduğu istek gövdesinden gelmez. Öğretmenin açtığı profilde
/// <c>UserId</c>/<c>ParentUserId</c>, öğrencinin açtığında <c>CreatedByTeacherUserId</c>/<c>ParentUserId</c>
/// sunucuda boştur; gövdede dolu gelirse istek <c>400 students.field_not_allowed</c> ile reddedilir.
/// Hesaba bağlama yalnız davetle (<see cref="StudentProfile.LinkUser"/>) olur.
/// </summary>
public sealed class StudentProfileOwnershipTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();

    private static CreateStudentProfileCommand Command(
        StudentOrigin origin, Guid? userId = null, Guid? createdBy = null, Guid? parentUserId = null)
        => new(userId, createdBy, parentUserId, "Ada Şahin", "7. Sınıf", null, null, null, null, origin, []);

    // ---------------------------------------------------------------- validator

    [Fact]
    public async Task Teacher_Cannot_Bind_A_New_Profile_To_Another_Users_Account()
    {
        var victim = Guid.NewGuid();

        var result = await new CreateStudentProfileCommandValidator().Validate(
            Command(StudentOrigin.TeacherManaged, userId: victim, createdBy: TeacherId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.field_not_allowed", result.Error.Code);
    }

    [Theory]
    [InlineData(StudentOrigin.TeacherManaged)]
    [InlineData(StudentOrigin.SelfRegistered)]
    public async Task ParentUserId_Is_Never_Accepted_At_Creation(StudentOrigin origin)
    {
        var command = origin == StudentOrigin.TeacherManaged
            ? Command(origin, createdBy: TeacherId, parentUserId: Guid.NewGuid())
            : Command(origin, userId: StudentUserId, parentUserId: Guid.NewGuid());

        var result = await new CreateStudentProfileCommandValidator().Validate(command, CancellationToken.None);

        Assert.Equal("students.field_not_allowed", result.Error.Code);
    }

    [Fact]
    public async Task SelfRegistered_Profile_Cannot_Claim_A_Creating_Teacher()
    {
        var result = await new CreateStudentProfileCommandValidator().Validate(
            Command(StudentOrigin.SelfRegistered, userId: StudentUserId, createdBy: TeacherId), CancellationToken.None);

        Assert.Equal("students.field_not_allowed", result.Error.Code);
    }

    [Fact]
    public async Task Clean_Requests_Pass_Validation()
    {
        var validator = new CreateStudentProfileCommandValidator();

        Assert.True((await validator.Validate(Command(StudentOrigin.TeacherManaged, createdBy: TeacherId), CancellationToken.None)).IsSuccess);
        Assert.True((await validator.Validate(Command(StudentOrigin.SelfRegistered, userId: StudentUserId), CancellationToken.None)).IsSuccess);
    }

    // ---------------------------------------------------------------- domain invariants

    [Fact]
    public void TeacherManaged_Profile_Cannot_Be_Constructed_With_A_User()
    {
        Assert.Throws<ArgumentException>(() => new StudentProfile(
            Guid.NewGuid(), Guid.NewGuid(), TeacherId, null, "Ada", "7", null, null, null, null,
            StudentOrigin.TeacherManaged, true, Now));
    }

    [Fact]
    public void SelfRegistered_Profile_Cannot_Be_Constructed_With_A_Parent()
    {
        Assert.Throws<ArgumentException>(() => new StudentProfile(
            Guid.NewGuid(), StudentUserId, null, Guid.NewGuid(), "Ada", "7", null, null, null, null,
            StudentOrigin.SelfRegistered, true, Now));
    }

    [Fact]
    public void LinkUser_Binds_Once_And_Announces_It()
    {
        var profile = new StudentProfile(
            Guid.NewGuid(), null, TeacherId, null, "Ada", "7", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

        Assert.True(profile.LinkUser(StudentUserId, Now).IsSuccess);
        // Aynı kullanıcı için tekrar: değişiklik yok, ikinci olay yok.
        Assert.True(profile.LinkUser(StudentUserId, Now).IsSuccess);
        var other = profile.LinkUser(Guid.NewGuid(), Now);

        Assert.Equal(StudentUserId, profile.UserId);
        Assert.Equal("students.profile_already_claimed", other.Error.Code);
        var linked = Assert.Single(profile.DomainEvents.OfType<StudentProfileLinkedToUserDomainEvent>());
        Assert.Equal(profile.Id, linked.StudentId);
        Assert.Equal(StudentUserId, linked.UserId);
        Assert.Equal(Now, linked.OccurredOnUtc);
    }

    // ---------------------------------------------------------------- read access after merge (N-28)

    [Fact]
    public async Task Linked_Teacher_Can_Read_A_Profile_They_Did_Not_Create()
    {
        // Birleşme sonrası kanonik profil öğrencinin kendi açtığıdır (CreatedByTeacherUserId = null);
        // bağı taşınan öğretmen öğrenciyi listesinde görür, profilini de açabilmeli.
        var canonical = new StudentProfile(
            Guid.NewGuid(), StudentUserId, null, null, "Ada", "7", null, null, null, null, StudentOrigin.SelfRegistered, true, Now);
        var authorizer = new StudentProfileQueryAuthorizer(
            new FakeCurrentUser(TeacherId, "Teacher"),
            new SingleProfileRepository(canonical),
            new FakeRequestEntityCache(),
            new FakeTeacherStudentAccess(TeacherAccessScope.Private));

        var result = await authorizer.Authorize(new GetStudentProfileByIdQuery(canonical.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unlinked_Teacher_Still_Cannot_Read_The_Profile()
    {
        var canonical = new StudentProfile(
            Guid.NewGuid(), StudentUserId, null, null, "Ada", "7", null, null, null, null, StudentOrigin.SelfRegistered, true, Now);
        var authorizer = new StudentProfileQueryAuthorizer(
            new FakeCurrentUser(TeacherId, "Teacher"),
            new SingleProfileRepository(canonical),
            new FakeRequestEntityCache(),
            new FakeTeacherStudentAccess(TeacherAccessScope.None));

        var result = await authorizer.Authorize(new GetStudentProfileByIdQuery(canonical.Id), CancellationToken.None);

        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => true;
    }

    private sealed class SingleProfileRepository(StudentProfile profile) : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult(profile.Id == studentId ? profile : null);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct)
            => Task.FromResult(profile.UserId == userId ? profile : null);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => throw new NotImplementedException();
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
