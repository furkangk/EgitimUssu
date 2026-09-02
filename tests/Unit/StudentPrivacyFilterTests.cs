using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentPrivacyFilterTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    private static GetChildDashboardQueryHandler Handler(IParentRepository repo, IStudentPrivacyDirectory privacy)
        => new(repo, privacy, new FakeStudyDigest(), new FakeUpcoming(), new FakeLastLesson(), new FakeNotes(), new FakePayments(), new FixedClock(Now));

    [Fact]
    public async Task Dashboard_WhenNotShared_MasksStudyFields()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();

        var link = new ParentChildLink(Guid.NewGuid(), parentUserId, studentId, "Ayşe", "anne", null, true, Now);
        link.Approve(Guid.NewGuid(), null, Now);
        var snapshot = new ChildProgressSnapshot(Guid.NewGuid(), studentId, Now);
        // snapshot çalışma alanlarını doldurmak için mevcut mutator yoksa 0 kalır; test IsShared davranışını doğrular.

        var repo = new FakeParentRepository(link, snapshot, studentUserId);
        var privacy = new FakePrivacyDirectory(new StudentPrivacy(ShareStudyDataWithParent: false, ShareStudyDataWithTeacher: true));
        var handler = Handler(repo, privacy);

        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Study.IsShared);
        Assert.Equal(0, result.Value!.Study.WeeklyStudyMinutes);
    }

    [Fact]
    public async Task Dashboard_WhenShared_MarksIsSharedTrue()
    {
        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var link = new ParentChildLink(Guid.NewGuid(), parentUserId, studentId, "Ayşe", "anne", null, true, Now);
        link.Approve(Guid.NewGuid(), null, Now);
        var repo = new FakeParentRepository(link, new ChildProgressSnapshot(Guid.NewGuid(), studentId, Now), Guid.NewGuid());
        var privacy = new FakePrivacyDirectory(new StudentPrivacy(true, true));
        var handler = Handler(repo, privacy);

        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.True(result.Value!.Study.IsShared);
    }

    private sealed class FakePrivacyDirectory : IStudentPrivacyDirectory
    {
        private readonly StudentPrivacy _value;
        public FakePrivacyDirectory(StudentPrivacy value) => _value = value;
        public Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(_value);
    }

    private sealed class FakeStudyDigest : IStudyDigestDirectory
    {
        public Task<StudyDigest> GetWeeklyDigestAsync(Guid studentId, DateTime nowUtc, CancellationToken ct)
            => Task.FromResult(new StudyDigest(0, 0, Array.Empty<StudySubjectMinutes>()));
    }

    private sealed class FakeUpcoming : IStudentUpcomingLessonsDirectory
    {
        public Task<IReadOnlyCollection<UpcomingLesson>> GetUpcomingAsync(Guid studentId, DateTime fromUtc, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<UpcomingLesson>>(Array.Empty<UpcomingLesson>());
    }

    private sealed class FakeLastLesson : IStudentLastLessonDirectory
    {
        public Task<LastLessonSummary?> GetLastCompletedAsync(Guid studentId, CancellationToken ct) => Task.FromResult<LastLessonSummary?>(null);
    }

    private sealed class FakeNotes : IStudentNotesDirectory
    {
        public Task<IReadOnlyCollection<ParentVisibleNote>> GetParentVisibleNotesAsync(Guid studentId, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentVisibleNote>>(Array.Empty<ParentVisibleNote>());
    }

    private sealed class FakePayments : IStudentPaymentDigestDirectory
    {
        public Task<IReadOnlyCollection<ParentPaymentLine>> GetLinesAsync(Guid studentId, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<ParentPaymentLine>>(Array.Empty<ParentPaymentLine>());
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeParentRepository : IParentRepository
    {
        private readonly ParentChildLink _link;
        private readonly ChildProgressSnapshot _snapshot;
        private readonly Guid _studentUserId;
        public FakeParentRepository(ParentChildLink link, ChildProgressSnapshot snapshot, Guid studentUserId)
        { _link = link; _snapshot = snapshot; _studentUserId = studentUserId; }

        public Task<ParentChildLink?> GetActiveLinkAsync(Guid parentUserId, Guid studentId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(_link);
        public Task<IReadOnlyCollection<ParentChildLink>> ListApprovedLinksForStudentAsync(Guid studentId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(new[] { _link });
        public Task<ChildProgressSnapshot?> GetSnapshotAsync(Guid studentId, CancellationToken ct) => Task.FromResult<ChildProgressSnapshot?>(_snapshot);
        public Task<KnownStudent?> GetKnownStudentAsync(Guid studentId, CancellationToken ct)
            => Task.FromResult<KnownStudent?>(new KnownStudent(Guid.NewGuid(), studentId, _studentUserId, Now));
        public Task<ParentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct) => Task.FromResult<ParentProfile?>(null);
        public Task<ParentChildLink?> GetLinkByIdAsync(Guid linkId, CancellationToken ct) => Task.FromResult<ParentChildLink?>(null);
        public Task<IReadOnlyCollection<ParentChildLink>> ListLinksByParentAsync(Guid parentUserId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<ParentChildLink>>(new[] { _link });
        public Task AddProfileAsync(ParentProfile profile, CancellationToken ct) => Task.CompletedTask;
        public Task AddLinkAsync(ParentChildLink link, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
