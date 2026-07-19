using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class ChildDashboardEnrichmentTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Dashboard_WhenShared_FillsStudyFromDigest()
    {
        var (parentUserId, studentId) = (Guid.NewGuid(), Guid.NewGuid());
        var repo = ApprovedRepo(parentUserId, studentId, studentUserId: Guid.NewGuid());
        var privacy = new FakePrivacy(new StudentPrivacy(ShareStudyDataWithParent: true, ShareStudyDataWithTeacher: true));
        var digest = new FakeStudyDigest(new StudyDigest(120, 5, new[]
        {
            new StudySubjectMinutes("Matematik", 80),
            new StudySubjectMinutes("Fizik", 40)
        }));

        var handler = new GetChildDashboardQueryHandler(repo, privacy, digest, new FakeUpcoming(), new FakeLastLesson(), new FakeNotes(), new FixedClock(Now));
        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Study.IsShared);
        Assert.Equal(120, result.Value.Study.WeeklyStudyMinutes);
        Assert.Equal(5, result.Value.Study.StreakDays);
        Assert.True(result.Value.Study.HasData);
        Assert.Equal(2, result.Value.Study.SubjectBreakdown.Count);
    }

    [Fact]
    public async Task Dashboard_WhenNotShared_MasksStudyAndSkipsDigest()
    {
        var (parentUserId, studentId) = (Guid.NewGuid(), Guid.NewGuid());
        var repo = ApprovedRepo(parentUserId, studentId, studentUserId: Guid.NewGuid());
        var privacy = new FakePrivacy(new StudentPrivacy(ShareStudyDataWithParent: false, ShareStudyDataWithTeacher: true));
        var digest = new FakeStudyDigest(new StudyDigest(120, 5, new[] { new StudySubjectMinutes("Matematik", 120) }));

        var handler = new GetChildDashboardQueryHandler(repo, privacy, digest, new FakeUpcoming(), new FakeLastLesson(), new FakeNotes(), new FixedClock(Now));
        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.False(result.Value.Study.IsShared);
        Assert.Equal(0, result.Value.Study.WeeklyStudyMinutes);
        Assert.Empty(result.Value.Study.SubjectBreakdown);
        Assert.False(digest.Called);
    }

    private static FakeParentRepository ApprovedRepo(Guid parentUserId, Guid studentId, Guid studentUserId)
    {
        var link = new ParentChildLink(Guid.NewGuid(), parentUserId, studentId, "Ayşe", "anne", null, true, Now);
        link.Approve(Guid.NewGuid(), null, Now);
        return new FakeParentRepository(link, new ChildProgressSnapshot(Guid.NewGuid(), studentId, Now), studentUserId);
    }

    [Fact]
    public async Task Dashboard_FillsUpcomingLessonsAndLastLesson()
    {
        var (parentUserId, studentId) = (Guid.NewGuid(), Guid.NewGuid());
        var repo = ApprovedRepo(parentUserId, studentId, studentUserId: Guid.NewGuid());
        var privacy = new FakePrivacy(new StudentPrivacy(true, true));
        var upcoming = new FakeUpcoming(new[]
        {
            new UpcomingLesson(Guid.NewGuid(), "Matematik", Now.AddDays(1), Now.AddDays(1).AddHours(1))
        });
        var lastLesson = new FakeLastLesson(new LastLessonSummary(Guid.NewGuid(), "Türev", null, Now.AddDays(-1)));

        var handler = new GetChildDashboardQueryHandler(repo, privacy, new FakeStudyDigest(new StudyDigest(0, 0, Array.Empty<StudySubjectMinutes>())), upcoming, lastLesson, new FakeNotes(), new FixedClock(Now));
        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.Single(result.Value.UpcomingLessons);
        Assert.Equal("Matematik", result.Value.UpcomingLessons.First().Subject);
        Assert.NotNull(result.Value.LastLesson);
        Assert.Equal("Türev", result.Value.LastLesson!.TopicTitle);
        Assert.Null(result.Value.LastLesson.TeacherNotes); // veli-görünürlük garantisi olmadan not sızmaz
    }

    [Fact]
    public async Task Dashboard_FillsParentVisibleTeacherNotes()
    {
        var (parentUserId, studentId) = (Guid.NewGuid(), Guid.NewGuid());
        var repo = ApprovedRepo(parentUserId, studentId, studentUserId: Guid.NewGuid());
        var notes = new FakeNotes(new[] { new ParentVisibleNote(Guid.NewGuid(), "Bugün türev işledik", Now.AddDays(-1)) });

        var handler = new GetChildDashboardQueryHandler(
            repo, new FakePrivacy(new StudentPrivacy(true, true)),
            new FakeStudyDigest(new StudyDigest(0, 0, Array.Empty<StudySubjectMinutes>())),
            new FakeUpcoming(), new FakeLastLesson(), notes, new FixedClock(Now));
        var result = await handler.Handle(new GetChildDashboardQuery(parentUserId, studentId), default);

        Assert.Single(result.Value.TeacherNotes);
        Assert.Equal("Bugün türev işledik", result.Value.TeacherNotes.First().Content);
    }

    private sealed class FakeUpcoming : IStudentUpcomingLessonsDirectory
    {
        private readonly IReadOnlyCollection<UpcomingLesson> _items;
        public FakeUpcoming(IReadOnlyCollection<UpcomingLesson>? items = null) => _items = items ?? Array.Empty<UpcomingLesson>();
        public Task<IReadOnlyCollection<UpcomingLesson>> GetUpcomingAsync(Guid studentId, DateTime fromUtc, int take, CancellationToken ct)
            => Task.FromResult(_items);
    }

    private sealed class FakeLastLesson : IStudentLastLessonDirectory
    {
        private readonly LastLessonSummary? _last;
        public FakeLastLesson(LastLessonSummary? last = null) => _last = last;
        public Task<LastLessonSummary?> GetLastCompletedAsync(Guid studentId, CancellationToken ct) => Task.FromResult(_last);
    }

    private sealed class FakeNotes : IStudentNotesDirectory
    {
        private readonly IReadOnlyCollection<ParentVisibleNote> _notes;
        public FakeNotes(IReadOnlyCollection<ParentVisibleNote>? notes = null) => _notes = notes ?? Array.Empty<ParentVisibleNote>();
        public Task<IReadOnlyCollection<ParentVisibleNote>> GetParentVisibleNotesAsync(Guid studentId, int take, CancellationToken ct)
            => Task.FromResult(_notes);
    }

    private sealed class FakeStudyDigest : IStudyDigestDirectory
    {
        private readonly StudyDigest _digest;
        public bool Called { get; private set; }
        public FakeStudyDigest(StudyDigest digest) => _digest = digest;
        public Task<StudyDigest> GetWeeklyDigestAsync(Guid studentId, DateTime nowUtc, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult(_digest);
        }
    }

    private sealed class FakePrivacy : IStudentPrivacyDirectory
    {
        private readonly StudentPrivacy _value;
        public FakePrivacy(StudentPrivacy value) => _value = value;
        public Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(_value);
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
