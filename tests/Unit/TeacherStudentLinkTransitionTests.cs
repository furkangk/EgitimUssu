using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 3 / N-22, N-28: Davet kabul/ret yalnız <c>InviteSent</c> bağda, kodla ve (varsa) hedef kullanıcı
/// tarafından yapılabilir; <c>Linked</c> bağ yeniden davet edilemez; birleştirmede öğretmen bağları kanonik
/// profile taşınır (çakışmada arşivli olan düşer).
/// </summary>
public sealed class TeacherStudentLinkTransitionTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
    private const string Code = "ABCD2345";

    private static TeacherStudentLink InvitedLink(Guid? target = null, Guid? studentId = null, Guid? teacherId = null)
    {
        var link = new TeacherStudentLink(
            Guid.NewGuid(), teacherId ?? Guid.NewGuid(), studentId ?? Guid.NewGuid(), TeacherStudentLinkStatus.Manual, Now);
        Assert.True(link.MarkInviteSent(Code, target, Now.AddDays(7), Now).IsSuccess);
        return link;
    }

    private static StudentProfile ManualProfile(Guid id, Guid teacherId)
        => new(id, null, teacherId, null, "Ali", "9", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

    private static StudentProfile SelfProfile(Guid id, Guid userId)
        => new(id, userId, null, null, "Ali Veli", "9", null, null, null, null, StudentOrigin.SelfRegistered, true, Now);

    // ---------------------------------------------------------------- domain: Accept / Reject

    [Fact]
    public void Accept_WithWrongCode_Fails_And_Keeps_Invite()
    {
        var link = InvitedLink();

        var result = link.Accept(Guid.NewGuid(), "ZZZZ9999", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("students.invite_code_mismatch", result.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }

    [Fact]
    public void Accept_On_Manual_Link_Without_Invite_Fails()
    {
        // N-22: link kimliğini bilen ama davet hiç gönderilmemiş bağ "kabul" edilemez.
        var link = new TeacherStudentLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeacherStudentLinkStatus.Manual, Now);

        var result = link.Accept(Guid.NewGuid(), Code, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("students.invite_code_mismatch", result.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.Manual, link.Status);
    }

    [Fact]
    public void Accept_On_Linked_Link_Fails_With_Invalid_Transition()
    {
        var link = InvitedLink();
        Assert.True(link.Accept(Guid.NewGuid(), Code, Now).IsSuccess);

        var again = link.Accept(Guid.NewGuid(), Code, Now);

        Assert.True(again.IsFailure);
        Assert.Equal("students.invalid_link_transition", again.Error.Code);
    }

    [Fact]
    public void Accept_By_Someone_Other_Than_Target_Fails()
    {
        var target = Guid.NewGuid();
        var link = InvitedLink(target);

        var result = link.Accept(Guid.NewGuid(), Code, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("students.invite_not_for_user", result.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }

    [Fact]
    public void Accept_After_Expiry_Fails()
    {
        var link = InvitedLink();

        var result = link.Accept(Guid.NewGuid(), Code, Now.AddDays(8));

        Assert.True(result.IsFailure);
        Assert.Equal("students.invite_expired", result.Error.Code);
    }

    [Fact]
    public void Accept_By_Target_With_Lowercase_Code_Links_And_Raises_Event()
    {
        var target = Guid.NewGuid();
        var link = InvitedLink(target);

        var result = link.Accept(target, Code.ToLowerInvariant(), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
        Assert.Single(link.DomainEvents.OfType<TeacherStudentLinkAcceptedDomainEvent>());
    }

    [Fact]
    public void Reject_Without_Code_Cannot_Break_A_Linked_Link()
    {
        // N-22: açık davetin link kimliğini bilen yabancı, kurulmuş bağı "reddedip" öğretmeni düşüremez.
        var link = InvitedLink();
        Assert.True(link.Accept(Guid.NewGuid(), Code, Now).IsSuccess);

        var withoutCode = link.Reject(Guid.NewGuid(), "ZZZZ9999", Now);
        var withCode = link.Reject(Guid.NewGuid(), Code, Now);

        Assert.Equal("students.invite_code_mismatch", withoutCode.Error.Code);
        Assert.Equal("students.invalid_link_transition", withCode.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
    }

    [Fact]
    public void Reject_By_Target_With_Code_Rejects()
    {
        var target = Guid.NewGuid();
        var link = InvitedLink(target);

        Assert.Equal("students.invite_not_for_user", link.Reject(Guid.NewGuid(), Code, Now).Error.Code);
        Assert.True(link.Reject(target, Code, Now).IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.Rejected, link.Status);
    }

    [Fact]
    public void MarkInviteSent_On_Linked_Link_Fails()
    {
        // N-22: kurulmuş bağa ikinci davet, ikinci claim'in mevcut sahibin üstüne yazmasının kapısıydı.
        var link = InvitedLink();
        Assert.True(link.Accept(Guid.NewGuid(), Code, Now).IsSuccess);

        var reinvite = link.MarkInviteSent("WXYZ6789", null, Now.AddDays(7), Now);

        Assert.True(reinvite.IsFailure);
        Assert.Equal("students.link_already_active", reinvite.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
        Assert.Equal(Code, link.InviteCode);
    }

    [Fact]
    public void MarkInviteSent_After_Rejection_Is_Allowed()
    {
        var target = Guid.NewGuid();
        var link = InvitedLink(target);
        Assert.True(link.Reject(target, Code, Now).IsSuccess);

        Assert.True(link.MarkInviteSent("WXYZ6789", target, Now.AddDays(7), Now).IsSuccess);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
    }

    // ---------------------------------------------------------------- domain: merge policy

    [Fact]
    public void Merge_Moves_Links_That_Have_No_Counterpart()
    {
        var canonicalId = Guid.NewGuid();
        var fromLink = InvitedLink();

        var dropped = StudentMergePolicy.MergeTeacherLinks([fromLink], [], canonicalId, acceptingLinkId: null, Now);

        Assert.Empty(dropped);
        Assert.Equal(canonicalId, fromLink.StudentId);
    }

    [Fact]
    public void Merge_Drops_The_Archived_Side_Of_A_Conflict()
    {
        var teacherId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var incoming = InvitedLink(teacherId: teacherId);
        incoming.SetRate(750m, "TRY", Now);
        var archived = new TeacherStudentLink(Guid.NewGuid(), teacherId, canonicalId, TeacherStudentLinkStatus.Linked, Now);
        archived.Archive(Now);

        var dropped = StudentMergePolicy.MergeTeacherLinks([incoming], [archived], canonicalId, incoming.Id, Now);

        Assert.Same(archived, Assert.Single(dropped));
        Assert.Equal(canonicalId, incoming.StudentId);
        Assert.Equal(750m, incoming.AgreedRateAmount);
    }

    [Fact]
    public void Merge_Keeps_The_Canonical_Link_When_Both_Live_And_Carries_The_Rate()
    {
        var teacherId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var fromId = Guid.NewGuid();
        var incoming = InvitedLink(teacherId: teacherId, studentId: fromId);
        incoming.SetRate(600m, "TRY", Now);
        var existing = new TeacherStudentLink(Guid.NewGuid(), teacherId, canonicalId, TeacherStudentLinkStatus.Linked, Now);

        var dropped = StudentMergePolicy.MergeTeacherLinks([incoming], [existing], canonicalId, incoming.Id, Now);

        Assert.Same(incoming, Assert.Single(dropped));
        Assert.Equal(canonicalId, existing.StudentId);
        Assert.Equal(600m, existing.AgreedRateAmount);
    }

    [Fact]
    public void Merge_Prefers_The_Accepting_Link_Over_A_Live_Unconfirmed_One()
    {
        // Kanonik profildeki Manual bağ öğretmene kapsam vermez (profil öğretmenin değil) — kabul edilen davet kazanmalı.
        var teacherId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var incoming = InvitedLink(teacherId: teacherId);
        var unconfirmed = new TeacherStudentLink(Guid.NewGuid(), teacherId, canonicalId, TeacherStudentLinkStatus.Manual, Now);

        var dropped = StudentMergePolicy.MergeTeacherLinks([incoming], [unconfirmed], canonicalId, incoming.Id, Now);

        Assert.Same(unconfirmed, Assert.Single(dropped));
        Assert.Equal(canonicalId, incoming.StudentId);
    }

    // ---------------------------------------------------------------- application: accept / reject / claim

    [Fact]
    public async Task Accept_Handler_Without_Code_Records_A_Failed_Attempt()
    {
        var link = InvitedLink();
        var store = new LinkStore(link);
        var limiter = new CountingLimiter();
        var handler = NewAcceptHandler(store, new ProfileStore(ManualProfile(link.StudentId, link.TeacherUserId)), limiter);

        var result = await handler.Handle(new AcceptTeacherStudentLinkCommand(link.Id, Guid.NewGuid(), "ZZZZ9999"), CancellationToken.None);

        Assert.Equal("students.invite_code_mismatch", result.Error.Code);
        Assert.Equal(1, limiter.Failures);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Accept_Handler_For_User_With_Own_Profile_Merges_Instead_Of_Overwriting()
    {
        // Eskiden kabul, kullanıcısı zaten profili olan öğrenciyi LinkUser ile ikinci profile yazıyordu (benzersiz UserId → 500).
        var teacherId = Guid.NewGuid();
        var manualId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var link = InvitedLink(studentUserId, manualId, teacherId);
        var manual = ManualProfile(manualId, teacherId);
        var approvedParent = Guid.NewGuid();
        manual.LinkParent(approvedParent, Now);
        var canonical = SelfProfile(canonicalId, studentUserId);
        var store = new LinkStore(link);
        var invites = new ParentInviteStore(new StudentParentInvite(Guid.NewGuid(), manualId, teacherId, "PQRS2345", "Ali", Now, Now.AddDays(7)));
        var handler = NewAcceptHandler(store, new ProfileStore(manual, canonical), new CountingLimiter(), invites);

        var result = await handler.Handle(new AcceptTeacherStudentLinkCommand(link.Id, studentUserId, Code), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(manual.UserId);
        Assert.True(manual.IsMerged);
        Assert.Equal(canonicalId, manual.MergedIntoStudentId);
        Assert.Equal(canonicalId, link.StudentId);
        Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
        Assert.Equal(canonicalId, Assert.Single(invites.Invites).StudentId);
        // Kanonik profilin velisi boştu → birleşenin onaylı velisi taşındı.
        Assert.Equal(approvedParent, canonical.ParentUserId);
        // Kabul olayı kanonik kimliği taşır (Reporting'in birleştirme tüketicisi yok).
        Assert.Equal(canonicalId, Assert.Single(link.DomainEvents.OfType<TeacherStudentLinkAcceptedDomainEvent>()).StudentId);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Claim_Cannot_Take_Over_A_Profile_Owned_By_Someone_Else()
    {
        // N-23 ile açılmış satırlar: profilin UserId'si dolu, bağ hâlâ Manual — ikinci davet + claim sahibin üstüne yazmamalı.
        var teacherId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var profile = ManualProfile(Guid.NewGuid(), teacherId);
        Assert.True(profile.LinkUser(owner, Now).IsSuccess);
        var link = InvitedLink(studentId: profile.Id, teacherId: teacherId);
        var handler = new ClaimStudentLinkCommandHandler(
            new LinkStore(link), new ProfileStore(profile), new ParentInviteStore(), new CountingLimiter(), new FixedClock());

        var result = await handler.Handle(new ClaimStudentLinkCommand(Code, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("students.profile_already_claimed", result.Error.Code);
        Assert.Equal(owner, profile.UserId);
    }

    [Fact]
    public async Task Reject_Handler_Validates_The_Code()
    {
        var link = InvitedLink();
        var store = new LinkStore(link);
        var limiter = new CountingLimiter();
        var handler = new RejectTeacherStudentLinkCommandHandler(store, limiter, new FixedClock());

        var result = await handler.Handle(new RejectTeacherStudentLinkCommand(link.Id, Guid.NewGuid(), "ZZZZ9999"), CancellationToken.None);

        Assert.Equal("students.invite_code_mismatch", result.Error.Code);
        Assert.Equal(TeacherStudentLinkStatus.InviteSent, link.Status);
        Assert.Equal(1, limiter.Failures);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Response_Authorizer_Requires_Student_Role()
    {
        var link = InvitedLink();
        var authorizer = new TeacherStudentLinkResponseAuthorizer(new FakeCurrentUser(Guid.NewGuid(), "Parent"), new LinkStore(link));

        var accept = await authorizer.Authorize(new AcceptTeacherStudentLinkCommand(link.Id, Guid.NewGuid(), Code), CancellationToken.None);
        var reject = await authorizer.Authorize(new RejectTeacherStudentLinkCommand(link.Id, Guid.NewGuid(), Code), CancellationToken.None);

        Assert.Equal("students.claim_requires_student_role", accept.Error.Code);
        Assert.Equal("students.claim_requires_student_role", reject.Error.Code);
    }

    private static AcceptTeacherStudentLinkCommandHandler NewAcceptHandler(
        LinkStore links, ProfileStore profiles, CountingLimiter limiter, ParentInviteStore? invites = null)
        => new(links, profiles, invites ?? new ParentInviteStore(), limiter, new FixedClock());

    // ---------------------------------------------------------------- test doubles

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => true;
    }

    private sealed class CountingLimiter : IInviteClaimAttemptLimiter
    {
        public int Failures { get; private set; }
        public Task<bool> IsBlockedAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task RecordFailureAsync(Guid userId, CancellationToken cancellationToken)
        {
            Failures++;
            return Task.CompletedTask;
        }

        public Task ResetAsync(Guid userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Bellek içi bağ deposu — aynı örneği her sorguda döndürür (EF kimlik haritası gibi).</summary>
    private sealed class LinkStore(params TeacherStudentLink[] links) : ITeacherStudentLinkRepository
    {
        private readonly List<TeacherStudentLink> _links = [.. links];
        public int SaveCount { get; private set; }

        public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct)
            => Task.FromResult(_links.FirstOrDefault(link => link.Id == linkId));
        public Task<TeacherStudentLink?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct)
            => Task.FromResult(_links.FirstOrDefault(link => link.InviteCode == inviteCode));
        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct)
            => Task.FromResult(_links.FirstOrDefault(link => link.TeacherUserId == teacherUserId && link.StudentId == studentId));
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByStudentAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<TeacherStudentLink>>(_links.Where(link => link.StudentId == studentId).ToArray());
        public void Remove(TeacherStudentLink link) => _links.Remove(link);
        public Task AddAsync(TeacherStudentLink link, CancellationToken ct)
        {
            _links.Add(link);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ProfileStore(params StudentProfile[] profiles) : IStudentProfileRepository
    {
        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult(profiles.FirstOrDefault(profile => profile.Id == studentId));
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct)
            => Task.FromResult(profiles.FirstOrDefault(profile => profile.UserId == userId));
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => throw new NotImplementedException();
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ParentInviteStore(params StudentParentInvite[] invites) : IStudentParentInviteRepository
    {
        public IReadOnlyList<StudentParentInvite> Invites { get; } = invites;

        public Task<IReadOnlyCollection<StudentParentInvite>> ListPendingByStudentAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<StudentParentInvite>>(
                Invites.Where(invite => invite.StudentId == studentId && invite.Status == ParentInviteStatus.Pending).ToArray());

        public Task AddAsync(StudentParentInvite invite, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudentParentInvite?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StudentParentInvite?> GetByIdAsync(Guid inviteId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
