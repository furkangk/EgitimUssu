using System.Text.Json;
using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 4 / N-26, N-27: Veli bağı bitince erişim gerçekten kesilir; gizlilik bilinmiyorsa kapalı varsayılır.
/// <list type="bullet">
/// <item>N-26: profil okuma <c>ParentUserId</c>'ye bakıyordu ve bu alan yalnız onayda yazılıyordu — bağı iptal/ret
/// edilen veli okumaya devam ediyordu. Erişim artık onaylı bağdan (<see cref="IParentAccessDirectory"/>) okunur;
/// bağ bitince <c>ParentChildLinkEnded</c> olayı alanı da temizler.</item>
/// <item>N-26: <c>Approve</c> iptal/ret edilmiş bağı yeniden onaylayabiliyordu — artık yalnız <c>Pending</c>'den.</item>
/// <item>N-27: öğrenci→kullanıcı eşlemesi çözülemezse veli paneli çalışma verisini <b>göstermez</b> (fail-closed).</item>
/// </list>
/// </summary>
public sealed class ParentAccessRevocationTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.NewGuid();

    // ------------------------------------------------------------------ domain: Approve yalnız Pending'den

    [Fact]
    public void Approve_Pending_Link_Succeeds()
    {
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());

        var result = link.Approve(Guid.NewGuid(), null, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParentChildLinkStatus.Approved, link.Status);
        Assert.Single(link.DomainEvents.OfType<ParentChildLinkApprovedDomainEvent>());
    }

    [Fact]
    public void Rejected_Link_Cannot_Be_Approved_Again()
    {
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());
        link.Reject(Guid.NewGuid(), Now);

        var result = link.Approve(Guid.NewGuid(), null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("parents.invalid_link_transition", result.Error.Code);
        Assert.Equal(ParentChildLinkStatus.Rejected, link.Status);
        Assert.Empty(link.DomainEvents.OfType<ParentChildLinkApprovedDomainEvent>());
    }

    [Fact]
    public void Revoked_Link_Cannot_Be_Approved_Again()
    {
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(link.Approve(Guid.NewGuid(), null, Now).IsSuccess);
        link.Revoke(Now);

        var result = link.Approve(Guid.NewGuid(), null, Now);

        Assert.Equal("parents.invalid_link_transition", result.Error.Code);
        Assert.Equal(ParentChildLinkStatus.Revoked, link.Status);
    }

    [Fact]
    public void Approved_Link_Cannot_Be_Approved_Twice()
    {
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(link.Approve(Guid.NewGuid(), null, Now).IsSuccess);

        var result = link.Approve(Guid.NewGuid(), null, Now.AddMinutes(5));

        Assert.Equal("parents.invalid_link_transition", result.Error.Code);
        Assert.Equal(Now, link.LinkedOnUtc);
        Assert.Single(link.DomainEvents.OfType<ParentChildLinkApprovedDomainEvent>());
    }

    // ------------------------------------------------------------------ domain: bağın bitişi olayla duyurulur

    [Fact]
    public void Revoking_An_Approved_Link_Raises_Ended_With_Revoked_Reason()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = NewLink(parentUserId, studentId);
        Assert.True(link.Approve(Guid.NewGuid(), null, Now).IsSuccess);

        link.Revoke(Now);

        var ended = Assert.Single(link.DomainEvents.OfType<ParentChildLinkEndedDomainEvent>());
        Assert.Equal(link.Id, ended.LinkId);
        Assert.Equal(parentUserId, ended.ParentUserId);
        Assert.Equal(studentId, ended.StudentId);
        Assert.Equal(ParentChildLinkEndReason.Revoked, ended.Reason);
    }

    [Fact]
    public void Rejecting_A_Pending_Link_Raises_Ended_With_Rejected_Reason()
    {
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());

        link.Reject(Guid.NewGuid(), Now);

        var ended = Assert.Single(link.DomainEvents.OfType<ParentChildLinkEndedDomainEvent>());
        Assert.Equal(ParentChildLinkEndReason.Rejected, ended.Reason);
    }

    [Fact]
    public void Revoking_An_Already_Rejected_Link_Does_Not_Announce_Another_End()
    {
        // Eski bir ret kaydını sonradan iptale çekmek, velinin daha yeni (onaylı) bağını etkileyecek bir olay üretmemeli.
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());
        link.Reject(Guid.NewGuid(), Now);

        link.Revoke(Now.AddMinutes(1));

        Assert.Single(link.DomainEvents.OfType<ParentChildLinkEndedDomainEvent>());
    }

    // ------------------------------------------------------------------ handler: geçersiz geçiş kaydedilmez

    [Fact]
    public async Task Approve_Handler_Refuses_A_Rejected_Link_Without_Saving()
    {
        var link = NewLink(Guid.NewGuid(), Guid.NewGuid());
        link.Reject(Guid.NewGuid(), Now);
        var repository = new LinkRepository(link);

        var result = await new ApproveChildLinkCommandHandler(repository, new FixedClock(Now))
            .Handle(new ApproveChildLinkCommand(link.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("parents.invalid_link_transition", result.Error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    // ------------------------------------------------------------------ Students: profil okuma onaylı bağdan

    [Fact]
    public async Task Parent_Whose_Link_Ended_Cannot_Read_Profile_Even_While_ParentUserId_Is_Stale()
    {
        // Olay henüz işlenmemiş olsa bile (ParentUserId hâlâ veliyi gösteriyor) erişim bağın kendisinden okunur.
        var parentUserId = Guid.NewGuid();
        var profile = NewTeacherProfile();
        profile.LinkParent(parentUserId, Now);
        var parentAccess = new FakeParentAccessDirectory();

        var result = await NewAuthorizer(parentUserId, "Parent", profile, parentAccess)
            .Authorize(new GetStudentProfileByIdQuery(profile.Id), CancellationToken.None);

        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Approved_Parent_Reads_Profile_Even_If_Not_The_Primary_Contact()
    {
        var parentUserId = Guid.NewGuid();
        var profile = NewTeacherProfile();
        var parentAccess = new FakeParentAccessDirectory();
        parentAccess.Approved.Add((parentUserId, profile.Id));

        var result = await NewAuthorizer(parentUserId, "Parent", profile, parentAccess)
            .Authorize(new GetStudentProfileByIdQuery(profile.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Parent_Access_Is_Not_Queried_For_Non_Parent_Roles()
    {
        var outsider = Guid.NewGuid();
        var profile = NewTeacherProfile();
        var parentAccess = new FakeParentAccessDirectory();
        parentAccess.Approved.Add((outsider, profile.Id));

        var result = await NewAuthorizer(outsider, "Student", profile, parentAccess)
            .Authorize(new GetStudentProfileByIdQuery(profile.Id), CancellationToken.None);

        Assert.Equal("shared.forbidden", result.Error.Code);
        Assert.Equal(0, parentAccess.IsApprovedCallCount);
    }

    // ------------------------------------------------------------------ Students: bağ bitince ParentUserId temizlenir

    [Fact]
    public async Task Ended_Link_Clears_The_Profiles_Parent()
    {
        var parentUserId = Guid.NewGuid();
        var options = NewStudentsOptions();
        var profileId = await SeedProfileWithParentAsync(options, parentUserId);

        await using (var db = new StudentsDbContext(options, new NoOpDomainEventMapper()))
        {
            await new ParentChildLinkEndedIntegrationEventHandler(db, new FixedClock(Now))
                .HandleAsync(EndedEnvelope(parentUserId, profileId), CancellationToken.None);
        }

        await using var verify = new StudentsDbContext(options, new NoOpDomainEventMapper());
        Assert.Null((await verify.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == profileId)).ParentUserId);
    }

    [Fact]
    public async Task Ended_Link_Of_Another_Parent_Keeps_The_Primary_Parent()
    {
        var primaryParentUserId = Guid.NewGuid();
        var options = NewStudentsOptions();
        var profileId = await SeedProfileWithParentAsync(options, primaryParentUserId);

        await using (var db = new StudentsDbContext(options, new NoOpDomainEventMapper()))
        {
            await new ParentChildLinkEndedIntegrationEventHandler(db, new FixedClock(Now))
                .HandleAsync(EndedEnvelope(Guid.NewGuid(), profileId), CancellationToken.None);
        }

        await using var verify = new StudentsDbContext(options, new NoOpDomainEventMapper());
        Assert.Equal(primaryParentUserId, (await verify.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == profileId)).ParentUserId);
    }

    // ------------------------------------------------------------------ Parents: gizlilik fail-closed (N-27)

    [Fact]
    public async Task Dashboard_Hides_Study_Data_When_The_Students_Account_Is_Unknown()
    {
        // Davetle bağlanan öğrencinin eşlemesi henüz gelmediyse öğrencinin "Gizli" ayarı okunamaz → gösterme.
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = NewLink(parentUserId, studentId);
        Assert.True(link.Approve(Guid.NewGuid(), null, Now).IsSuccess);
        var repository = new LinkRepository(link) { KnownStudent = null };

        var result = await new GetChildDashboardQueryHandler(
                repository, new SharingPrivacy(), new RichStudyDigest(), new NoUpcoming(), new NoLastLesson(),
                new NoNotes(), new NoPayments(), new FixedClock(Now))
            .Handle(new GetChildDashboardQuery(parentUserId, studentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Study.IsShared);
        Assert.Equal(0, result.Value.Study.WeeklyStudyMinutes);
        Assert.Empty(result.Value.Study.SubjectBreakdown);
    }

    [Fact]
    public async Task Dashboard_Hides_Study_Data_When_The_Student_Has_No_Account_Yet()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = NewLink(parentUserId, studentId);
        Assert.True(link.Approve(Guid.NewGuid(), null, Now).IsSuccess);
        var repository = new LinkRepository(link) { KnownStudent = new KnownStudent(Guid.NewGuid(), studentId, null, Now) };

        var result = await new GetChildDashboardQueryHandler(
                repository, new SharingPrivacy(), new RichStudyDigest(), new NoUpcoming(), new NoLastLesson(),
                new NoNotes(), new NoPayments(), new FixedClock(Now))
            .Handle(new GetChildDashboardQuery(parentUserId, studentId), CancellationToken.None);

        Assert.False(result.Value!.Study.IsShared);
    }

    // ------------------------------------------------------------------ yardımcılar

    private static ParentChildLink NewLink(Guid parentUserId, Guid studentId)
        => new(Guid.NewGuid(), parentUserId, studentId, "Ada", "Anne", null, true, Now);

    private static StudentProfile NewTeacherProfile()
        => new(Guid.NewGuid(), null, TeacherUserId, null, "Ada", "8", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

    private static StudentProfileQueryAuthorizer NewAuthorizer(
        Guid userId, string role, StudentProfile profile, IParentAccessDirectory parentAccess)
        => new(
            new CurrentUser(userId, role),
            new SingleProfileRepository(profile),
            new FakeRequestEntityCache(),
            new FakeTeacherStudentAccess(TeacherAccessScope.None),
            parentAccess);

    private static DbContextOptions<StudentsDbContext> NewStudentsOptions()
        => new DbContextOptionsBuilder<StudentsDbContext>().UseInMemoryDatabase($"p15-t4-{Guid.NewGuid():N}").Options;

    private static async Task<Guid> SeedProfileWithParentAsync(DbContextOptions<StudentsDbContext> options, Guid parentUserId)
    {
        await using var db = new StudentsDbContext(options, new NoOpDomainEventMapper());
        var profile = NewTeacherProfile();
        profile.LinkParent(parentUserId, Now);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.Id;
    }

    private static IntegrationEvent EndedEnvelope(Guid parentUserId, Guid studentId)
        => new(
            Guid.NewGuid(),
            Now,
            "ParentChildLinkEndedDomainEvent",
            "Parents",
            JsonSerializer.Serialize(
                new ParentChildLinkEndedIntegrationEvent(Guid.NewGuid(), parentUserId, studentId, ParentChildLinkEndReason.Revoked, Now),
                IntegrationEventSerialization.Options));

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsAuthenticated => true;
    }

    private sealed class SingleProfileRepository(StudentProfile profile) : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<StudentProfile?>(studentId == profile.Id ? profile : null);

        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<StudentProfile?>(profile.UserId == userId ? profile : null);

        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAsync(StudentProfile studentProfile, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class LinkRepository(ParentChildLink link) : IParentRepository
    {
        public KnownStudent? KnownStudent { get; init; }

        public int SaveCount { get; private set; }

        public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<ParentProfile?>(null);

        public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken ct)
            => Task.FromResult<ParentChildLink?>(linkId == link.Id ? link : null);

        public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken ct)
            => Task.FromResult<ParentChildLink?>(link);

        public Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentChildLink>>([link]);

        public Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(link.IsApproved ? [link] : []);

        public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult<ChildProgressSnapshot?>(null);

        public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult(KnownStudent);

        public Task AddProfileAsync(ParentProfile profile, CancellationToken ct) => Task.CompletedTask;

        public Task AddLinkAsync(ParentChildLink newLink, CancellationToken ct) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Kayıt varsa paylaşım açık ve Standard — fail-closed kuralını yalnız "kullanıcı bilinmiyor" tetiklemeli.</summary>
    private sealed class SharingPrivacy : IStudentPrivacyDirectory
    {
        public Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken ct)
            => Task.FromResult(new StudentPrivacy(true, true, StudyPrivacyLevel.Standard));
    }

    private sealed class RichStudyDigest : IStudyDigestDirectory
    {
        public Task<StudyDigest> GetWeeklyDigestAsync(Guid studentId, DateTime nowUtc, CancellationToken ct)
            => Task.FromResult(new StudyDigest(180, 4, [new StudySubjectMinutes("Matematik", 120)]));
    }

    private sealed class NoUpcoming : IStudentUpcomingLessonsDirectory
    {
        public Task<IReadOnlyCollection<UpcomingLesson>> GetUpcomingAsync(Guid studentId, DateTime fromUtc, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<UpcomingLesson>>([]);
    }

    private sealed class NoLastLesson : IStudentLastLessonDirectory
    {
        public Task<LastLessonSummary?> GetLastCompletedAsync(Guid studentId, CancellationToken ct) => Task.FromResult<LastLessonSummary?>(null);
    }

    private sealed class NoNotes : IStudentNotesDirectory
    {
        public Task<IReadOnlyCollection<ParentVisibleNote>> GetParentVisibleNotesAsync(Guid studentId, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentVisibleNote>>([]);
    }

    private sealed class NoPayments : IStudentPaymentDigestDirectory
    {
        public Task<IReadOnlyCollection<ParentPaymentLine>> GetLinesAsync(Guid studentId, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentPaymentLine>>([]);
    }
}
