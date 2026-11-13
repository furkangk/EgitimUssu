using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P06 Task 9 / M03-1 + M03-3: Öğretmenin mevcut bir veli kullanıcısını öğrenciye bağlaması
/// ve velinin **onaylı** çocuklarının listelenmesi.
///
/// Modül sınırı: bağın kendisi Parents'ta yaşar. Students yalnızca
/// <see cref="IParentChildLinkRequestService"/> üzerinden **onay bekleyen** bağ talebi açar;
/// <c>StudentProfile.ParentUserId</c> ancak <c>ParentChildLinkApproved</c> olayıyla dolar.
/// </summary>
public sealed class StudentParentLinkTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentUserId = Guid.NewGuid();
    private static readonly Guid ParentUserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LinkParent_Should_Open_Pending_Link()
    {
        var profile = NewProfile();
        var service = new FakeLinkService(ParentLinkRequestOutcome.Created);
        var handler = new LinkStudentParentCommandHandler(new FakeRepository(profile), service);

        var result = await handler.Handle(
            new LinkStudentParentCommand(profile.Id, ParentUserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal(ParentUserId, result.Value.ParentUserId);
        Assert.Equal((ParentUserId, profile.Id), service.LastRequest);
        // Bağ onaylanana kadar profil velisiz kalır.
        Assert.Null(profile.ParentUserId);
    }

    [Fact]
    public async Task LinkParent_Should_Reject_Unregistered_Parent()
    {
        var profile = NewProfile();
        var handler = new LinkStudentParentCommandHandler(
            new FakeRepository(profile),
            new FakeLinkService(ParentLinkRequestOutcome.ParentNotRegistered));

        var result = await handler.Handle(
            new LinkStudentParentCommand(profile.Id, ParentUserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.parent_user_required", result.Error.Code);
    }

    [Fact]
    public async Task LinkParent_Should_Reject_Empty_Parent_Id_Without_Calling_Parents()
    {
        var profile = NewProfile();
        var service = new FakeLinkService(ParentLinkRequestOutcome.Created);
        var handler = new LinkStudentParentCommandHandler(new FakeRepository(profile), service);

        var result = await handler.Handle(
            new LinkStudentParentCommand(profile.Id, Guid.Empty),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.parent_user_required", result.Error.Code);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task LinkParent_Should_Reject_Existing_Link()
    {
        var profile = NewProfile();
        var handler = new LinkStudentParentCommandHandler(
            new FakeRepository(profile),
            new FakeLinkService(ParentLinkRequestOutcome.AlreadyLinked));

        var result = await handler.Handle(
            new LinkStudentParentCommand(profile.Id, ParentUserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.parent_link_exists", result.Error.Code);
    }

    [Fact]
    public async Task LinkParent_Should_Fail_When_Profile_Missing()
    {
        var handler = new LinkStudentParentCommandHandler(
            new FakeRepository(null),
            new FakeLinkService(ParentLinkRequestOutcome.Created));

        var result = await handler.Handle(
            new LinkStudentParentCommand(Guid.NewGuid(), ParentUserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("students.profile_not_found", result.Error.Code);
    }

    [Fact]
    public async Task ListByParent_Should_Return_Only_Approved_Children()
    {
        var approved = NewProfile();
        var other = NewProfile();
        var access = new FakeParentAccess([approved.Id]);
        var handler = new ListStudentsByParentQueryHandler(
            new ListRepository([approved, other]),
            access);

        var result = await handler.Handle(new ListStudentsByParentQuery(ParentUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var child = Assert.Single(result.Value!);
        Assert.Equal(approved.Id, child.Id);
        Assert.Equal([approved.Id], access.RequestedIds);
    }

    [Fact]
    public async Task ListByParent_Should_Return_Empty_When_No_Approved_Link()
    {
        var handler = new ListStudentsByParentQueryHandler(
            new ListRepository([NewProfile()]),
            new FakeParentAccess([]));

        var result = await handler.Handle(new ListStudentsByParentQuery(ParentUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListByParent_Should_Forbid_Other_Parent()
    {
        var authorizer = new StudentProfileQueryAuthorizer(
            new FakeCurrentUser(Guid.NewGuid().ToString(), "Parent"),
            new FakeRepository(null),
            new FakeRequestEntityCache(),
            new FakeTeacherStudentAccess(TeacherAccessScope.None),
            new FakeParentAccessDirectory());

        var result = await authorizer.Authorize(new ListStudentsByParentQuery(ParentUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task ListByParent_Should_Allow_Own_Parent()
    {
        var authorizer = new StudentProfileQueryAuthorizer(
            new FakeCurrentUser(ParentUserId.ToString(), "Parent"),
            new FakeRepository(null),
            new FakeRequestEntityCache(),
            new FakeTeacherStudentAccess(TeacherAccessScope.None),
            new FakeParentAccessDirectory());

        var result = await authorizer.Authorize(new ListStudentsByParentQuery(ParentUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LinkParent_Authorizer_Should_Forbid_Unrelated_Teacher()
    {
        var profile = NewProfile();
        var authorizer = new LinkStudentParentCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid().ToString(), "Teacher"),
            new FakeRepository(profile));

        var result = await authorizer.Authorize(
            new LinkStudentParentCommand(profile.Id, ParentUserId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task LinkParent_Authorizer_Should_Allow_Owning_Teacher()
    {
        var profile = NewProfile();
        var authorizer = new LinkStudentParentCommandAuthorizer(
            new FakeCurrentUser(TeacherId.ToString(), "Teacher"),
            new FakeRepository(profile));

        var result = await authorizer.Authorize(
            new LinkStudentParentCommand(profile.Id, ParentUserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
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

    private sealed class FakeLinkService(ParentLinkRequestOutcome outcome) : IParentChildLinkRequestService
    {
        public (Guid ParentUserId, Guid StudentId)? LastRequest { get; private set; }

        public Task<ParentLinkRequestOutcome> RequestPendingLinkAsync(
            Guid parentUserId,
            Guid studentId,
            string? childDisplayName,
            CancellationToken cancellationToken)
        {
            LastRequest = (parentUserId, studentId);
            return Task.FromResult(outcome);
        }
    }

    private sealed class FakeParentAccess(IReadOnlyCollection<Guid> approvedStudentIds) : IParentAccessDirectory
    {
        public IReadOnlyCollection<Guid>? RequestedIds { get; private set; }

        public Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken ct)
            => Task.FromResult(approvedStudentIds.Contains(studentId));

        public Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(Guid parentUserId, CancellationToken ct)
        {
            RequestedIds = approvedStudentIds;
            return Task.FromResult(approvedStudentIds);
        }
    }

    private sealed class FakeRepository(StudentProfile? profile) : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct) => Task.FromResult(profile);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(profile);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile p, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ListRepository(IReadOnlyCollection<StudentProfile> profiles) : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct) => Task.FromResult<StudentProfile?>(null);
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<StudentProfile?>(null);
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<StudentProfile>>(profiles.Where(p => ids.Contains(p.Id)).ToArray());

        public Task AddAsync(StudentProfile p, CancellationToken ct) => Task.CompletedTask;
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
