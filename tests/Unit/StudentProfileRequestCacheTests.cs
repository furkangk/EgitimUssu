using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P07 Task 3 / O2: Korumalı sorguda authorizer ve handler aynı varlığı iki kez yüklemez.
/// Authorizer profili yükleyip <see cref="IRequestEntityCache"/>'e <b>sorgu nesnesi anahtarıyla</b>
/// koyar, handler oradan okur. Sorgu record olduğu için değer eşitliği anahtarı garanti eder.
/// </summary>
public sealed class StudentProfileRequestCacheTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Authorized_GetById_Should_Load_Profile_Only_Once()
    {
        var profile = NewProfile();
        var repository = new CountingRepository(profile);
        var cache = new FakeRequestEntityCache();
        var currentUser = new FakeCurrentUser(TeacherId.ToString(), "Teacher");
        var query = new GetStudentProfileByIdQuery(profile.Id);

        var authorization = await new StudentProfileQueryAuthorizer(currentUser, repository, cache, new FakeTeacherStudentAccess(TeacherAccessScope.None))
            .Authorize(query, CancellationToken.None);
        var result = await new GetStudentProfileByIdQueryHandler(repository, cache)
            .Handle(query, CancellationToken.None);

        Assert.True(authorization.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(profile.Id, result.Value!.Id);
        Assert.Equal(1, repository.GetByIdCount);
    }

    [Fact]
    public async Task Handler_Should_Fall_Back_To_Repository_When_Cache_Is_Empty()
    {
        var profile = NewProfile();
        var repository = new CountingRepository(profile);

        var result = await new GetStudentProfileByIdQueryHandler(repository, new FakeRequestEntityCache())
            .Handle(new GetStudentProfileByIdQuery(profile.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.GetByIdCount);
    }

    [Fact]
    public async Task Handler_Should_Not_Reuse_Cache_Entry_Of_Another_Query()
    {
        var profile = NewProfile();
        var repository = new CountingRepository(profile);
        var cache = new FakeRequestEntityCache();
        cache.Set(new GetStudentProfileByIdQuery(Guid.NewGuid()), NewProfile());

        var result = await new GetStudentProfileByIdQueryHandler(repository, cache)
            .Handle(new GetStudentProfileByIdQuery(profile.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(profile.Id, result.Value!.Id);
        Assert.Equal(1, repository.GetByIdCount);
    }

    [Fact]
    public async Task Authorized_GetByUserId_Should_Load_Profile_Only_Once()
    {
        var profile = NewProfile();
        var repository = new CountingRepository(profile);
        var cache = new FakeRequestEntityCache();
        var currentUser = new FakeCurrentUser(StudentUserId.ToString(), "Student");
        var query = new GetStudentProfileByUserIdQuery(StudentUserId);

        var authorization = await new StudentProfileQueryAuthorizer(currentUser, repository, cache, new FakeTeacherStudentAccess(TeacherAccessScope.None))
            .Authorize(query, CancellationToken.None);
        var result = await new GetStudentProfileByUserIdQueryHandler(repository, cache)
            .Handle(query, CancellationToken.None);

        Assert.True(authorization.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.GetByUserIdCount);
    }

    /// <summary>Admin yolunda authorizer profili yüklemez; handler yine de doğru sonucu döndürmeli.</summary>
    [Fact]
    public async Task Admin_Path_Should_Still_Return_Profile_Without_Cache_Entry()
    {
        var profile = NewProfile();
        var repository = new CountingRepository(profile);
        var cache = new FakeRequestEntityCache();
        var query = new GetStudentProfileByIdQuery(profile.Id);

        var authorization = await new StudentProfileQueryAuthorizer(
                new FakeCurrentUser(Guid.NewGuid().ToString(), "Admin"), repository, cache, new FakeTeacherStudentAccess(TeacherAccessScope.None))
            .Authorize(query, CancellationToken.None);
        var result = await new GetStudentProfileByIdQueryHandler(repository, cache)
            .Handle(query, CancellationToken.None);

        Assert.True(authorization.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(profile.Id, result.Value!.Id);
    }

    /// <summary>
    /// Öğretmenin açıp öğrencinin davetle devraldığı profil (sahip öğrenci + açan öğretmen). P15 T3'ten beri bu
    /// durum kurucuyla değil, ürünün kendi yolu olan <see cref="StudentProfile.LinkUser"/> ile kurulur.
    /// </summary>
    private static StudentProfile NewProfile()
    {
        var profile = new StudentProfile(
            Guid.NewGuid(),
            null,
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
        Assert.True(profile.LinkUser(StudentUserId, Now).IsSuccess);
        return profile;
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }

    private sealed class CountingRepository(StudentProfile profile) : IStudentProfileRepository
    {
        public int GetByIdCount { get; private set; }
        public int GetByUserIdCount { get; private set; }

        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct)
        {
            GetByIdCount++;
            return Task.FromResult<StudentProfile?>(studentId == profile.Id ? profile : null);
        }

        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            GetByUserIdCount++;
            return Task.FromResult<StudentProfile?>(userId == profile.UserId ? profile : null);
        }

        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile p, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
