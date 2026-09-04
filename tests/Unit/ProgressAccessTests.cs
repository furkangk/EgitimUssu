using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P08 Task 2 / M10-2, M10-3, M10-4 — öğretmen ve veli gelişim görünümü.
/// <para>
/// Üç kural birlikte doğrulanır:
/// <list type="bullet">
///   <item>Yetki: bağlı olmayan öğretmen ve onaysız veli 403 alır (varsayılan-deny).</item>
///   <item>Gizlilik: <see cref="IStudentPrivacyDirectory"/> kapalıysa ya da
///         <see cref="StudyPrivacyLevel.Hidden"/> ise özet boş döner; <c>Limited</c> yalnız sayaç bırakır.</item>
///   <item>Besleme: tamamlanan ders konuya katılım katkısı yazar, hedefe ulaşan konu otomatik kapanır.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ProgressAccessTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 6, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StudentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StudentUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TeacherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ParentUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid StrangerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // --- Yetki: öğretmen -----------------------------------------------------------------------

    [Fact]
    public async Task Teacher_Without_Link_Should_Be_Forbidden()
    {
        var authorizer = new TeacherStudentProgressAuthorizer(
            new FakeCurrentUser { UserId = TeacherUserId.ToString(), Roles = ["Teacher"] },
            new FakeStudentDirectory { LinkedTeacherUserId = null });

        var result = await authorizer.Authorize(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Teacher_With_Link_Should_Be_Allowed()
    {
        var authorizer = new TeacherStudentProgressAuthorizer(
            new FakeCurrentUser { UserId = TeacherUserId.ToString(), Roles = ["Teacher"] },
            new FakeStudentDirectory { LinkedTeacherUserId = TeacherUserId });

        var result = await authorizer.Authorize(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Teacher_Should_Not_Read_Another_Teachers_Route()
    {
        // Rotadaki öğretmen kimliği oturumdakinden farklı: bağ olsa bile IDOR'a izin verilmez.
        var authorizer = new TeacherStudentProgressAuthorizer(
            new FakeCurrentUser { UserId = StrangerUserId.ToString(), Roles = ["Teacher"] },
            new FakeStudentDirectory { LinkedTeacherUserId = TeacherUserId });

        var result = await authorizer.Authorize(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    // --- Yetki: veli ---------------------------------------------------------------------------

    [Fact]
    public async Task Unapproved_Parent_Should_Be_Forbidden()
    {
        var authorizer = new ParentChildProgressAuthorizer(
            new FakeCurrentUser { UserId = ParentUserId.ToString(), Roles = ["Parent"] },
            new FakeParentAccessDirectory { ApprovedStudentId = null });

        var result = await authorizer.Authorize(
            new GetParentChildProgressOverviewQuery(ParentUserId, StudentId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Approved_Parent_Should_Be_Allowed()
    {
        var authorizer = new ParentChildProgressAuthorizer(
            new FakeCurrentUser { UserId = ParentUserId.ToString(), Roles = ["Parent"] },
            new FakeParentAccessDirectory { ApprovedStudentId = StudentId });

        var result = await authorizer.Authorize(
            new GetParentChildProgressOverviewQuery(ParentUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Admin_Should_Be_Allowed_Without_Link()
    {
        var authorizer = new ParentChildProgressAuthorizer(
            new FakeCurrentUser { UserId = StrangerUserId.ToString(), Roles = ["Admin"] },
            new FakeParentAccessDirectory { ApprovedStudentId = null });

        var result = await authorizer.Authorize(
            new GetParentChildProgressOverviewQuery(ParentUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- Gizlilik ------------------------------------------------------------------------------

    [Fact]
    public async Task Parent_Should_Get_Empty_Overview_When_Privacy_Is_Hidden()
    {
        var handler = new GetParentChildProgressOverviewQueryHandler(
            Repository(),
            new FakeStudentDirectory { OwnerUserId = StudentUserId },
            new FakePrivacyDirectory(new StudentPrivacy(true, true, StudyPrivacyLevel.Hidden)));

        var result = await handler.Handle(
            new GetParentChildProgressOverviewQuery(ParentUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;
        Assert.Equal(0, overview.MasteredCount);
        Assert.Equal(0, overview.ActiveGoalCount);
        Assert.Empty(overview.WeakSpots);
        Assert.Empty(overview.Strengths);
    }

    [Fact]
    public async Task Parent_Should_Get_Empty_Overview_When_Sharing_Is_Off()
    {
        var handler = new GetParentChildProgressOverviewQueryHandler(
            Repository(),
            new FakeStudentDirectory { OwnerUserId = StudentUserId },
            new FakePrivacyDirectory(new StudentPrivacy(ShareStudyDataWithParent: false, ShareStudyDataWithTeacher: true)));

        var result = await handler.Handle(
            new GetParentChildProgressOverviewQuery(ParentUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;
        Assert.Empty(overview.WeakSpots);
        Assert.Equal(0, overview.MasteredCount);
    }

    [Fact]
    public async Task Teacher_Should_Get_Empty_Overview_When_Teacher_Sharing_Is_Off()
    {
        var handler = new GetTeacherStudentProgressOverviewQueryHandler(
            Repository(),
            new FakeStudentDirectory { OwnerUserId = StudentUserId },
            new FakePrivacyDirectory(new StudentPrivacy(ShareStudyDataWithParent: true, ShareStudyDataWithTeacher: false)));

        var result = await handler.Handle(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;
        Assert.Equal(0, overview.ProficientCount);
        Assert.Empty(overview.Strengths);
    }

    [Fact]
    public async Task Limited_Privacy_Should_Keep_Counts_But_Hide_Detail_Lists()
    {
        var handler = new GetTeacherStudentProgressOverviewQueryHandler(
            Repository(),
            new FakeStudentDirectory { OwnerUserId = StudentUserId },
            new FakePrivacyDirectory(new StudentPrivacy(true, true, StudyPrivacyLevel.Limited)));

        var result = await handler.Handle(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;
        Assert.Equal(1, overview.WeakCount);          // sayaçlar görünür
        Assert.Equal(1, overview.ActiveGoalCount);
        Assert.Empty(overview.WeakSpots);             // konu kırılımı gizli
        Assert.Empty(overview.Strengths);
    }

    [Fact]
    public async Task Standard_Privacy_Should_Return_Full_Overview()
    {
        var handler = new GetTeacherStudentProgressOverviewQueryHandler(
            Repository(),
            new FakeStudentDirectory { OwnerUserId = StudentUserId },
            new FakePrivacyDirectory(new StudentPrivacy(true, true)));

        var result = await handler.Handle(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var overview = result.Value!;
        Assert.Equal(1, overview.WeakCount);
        Assert.Single(overview.WeakSpots);
        Assert.Equal(1, overview.ActiveGoalCount);
    }

    [Fact]
    public async Task Missing_Student_User_Should_Fall_Back_To_Standard()
    {
        // Öğrencinin bağlı kullanıcısı yoksa ayar kaydı da yoktur; sözleşme "paylaşım açık" varsayar.
        var handler = new GetTeacherStudentProgressOverviewQueryHandler(
            Repository(),
            new FakeStudentDirectory { OwnerUserId = null },
            new FakePrivacyDirectory(new StudentPrivacy(false, false, StudyPrivacyLevel.Hidden)));

        var result = await handler.Handle(
            new GetTeacherStudentProgressOverviewQuery(TeacherUserId, StudentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.WeakSpots);
    }

    // --- M10-4: ders katılımı ------------------------------------------------------------------

    [Fact]
    public void Lesson_Attendance_Should_Raise_Score_And_Mark_Source()
    {
        var mastery = new TopicMastery(Guid.NewGuid(), StudentId, "Matematik", "Türev", Now);

        mastery.RegisterLessonAttendance(60, Now);

        Assert.Equal(1, mastery.AttendedLessonCount);
        Assert.Equal(60, mastery.TotalLessonMinutes);
        Assert.Equal(MasterySource.LessonOnly, mastery.Source);
        Assert.True(mastery.MasteryScore > 0m);
        Assert.NotEqual(MasteryLevel.NotStarted, mastery.MasteryLevel);
    }

    [Fact]
    public void Lesson_And_Study_Together_Should_Be_Combined_Source()
    {
        var mastery = new TopicMastery(Guid.NewGuid(), StudentId, "Matematik", "Türev", Now);

        mastery.RegisterStudy(60, Now);
        mastery.RegisterLessonAttendance(60, Now);

        Assert.Equal(MasterySource.Combined, mastery.Source);
        // Katılım süresi çalışma bileşenine eklenir: 120/180 * 30 = 20
        Assert.Equal(20m, mastery.MasteryScore);
    }

    [Fact]
    public async Task Lesson_Completed_Event_Should_Register_Attendance()
    {
        await using var dbContext = NewDbContext();
        var repository = new ProgressRepository(dbContext);
        var masteryService = new MasteryService(repository, new SeqIdGenerator(), new FixedClock(Now));
        var lessonSessionId = Guid.NewGuid();
        var lessonScheduleId = Guid.NewGuid();

        var handler = new LessonSessionCompletedProgressHandler(
            dbContext,
            masteryService,
            new FakeLessonScheduleDirectory(new LessonScheduleDetails(
                lessonScheduleId, TeacherUserId, StudentId,
                Now.AddHours(-2), Now.AddHours(-1), "Matematik", null, "Completed")),
            new FakeLessonSessionAccessService(new LessonSessionDetails(
                lessonSessionId, lessonScheduleId, TeacherUserId, StudentId, true, Now, "Türev", null, null)),
            new FixedClock(Now));

        await handler.HandleAsync(Envelope(lessonSessionId, lessonScheduleId), CancellationToken.None);

        var mastery = await dbContext.TopicMasteries.SingleAsync();
        Assert.Equal("Matematik", mastery.Subject);
        Assert.Equal("Türev", mastery.Topic);
        Assert.Equal(1, mastery.AttendedLessonCount);
        Assert.Equal(60, mastery.TotalLessonMinutes);
    }

    [Fact]
    public async Task Lesson_Completed_Event_Should_Be_Idempotent()
    {
        await using var dbContext = NewDbContext();
        var repository = new ProgressRepository(dbContext);
        var masteryService = new MasteryService(repository, new SeqIdGenerator(), new FixedClock(Now));
        var lessonSessionId = Guid.NewGuid();
        var lessonScheduleId = Guid.NewGuid();
        var envelope = Envelope(lessonSessionId, lessonScheduleId);

        var handler = new LessonSessionCompletedProgressHandler(
            dbContext,
            masteryService,
            new FakeLessonScheduleDirectory(new LessonScheduleDetails(
                lessonScheduleId, TeacherUserId, StudentId,
                Now.AddHours(-2), Now.AddHours(-1), "Matematik", null, "Completed")),
            new FakeLessonSessionAccessService(new LessonSessionDetails(
                lessonSessionId, lessonScheduleId, TeacherUserId, StudentId, true, Now, "Türev", null, null)),
            new FixedClock(Now));

        await handler.HandleAsync(envelope, CancellationToken.None);
        await handler.HandleAsync(envelope, CancellationToken.None);

        var mastery = await dbContext.TopicMasteries.SingleAsync();
        Assert.Equal(1, mastery.AttendedLessonCount);
    }

    [Fact]
    public async Task Lesson_Without_Schedule_Should_Be_Skipped_But_Deduplicated()
    {
        // Plansız (ad-hoc) oturumda ders/branş bilgisi yoktur; hâkimiyet satırı üretilmez.
        await using var dbContext = NewDbContext();
        var repository = new ProgressRepository(dbContext);
        var masteryService = new MasteryService(repository, new SeqIdGenerator(), new FixedClock(Now));
        var lessonSessionId = Guid.NewGuid();

        var handler = new LessonSessionCompletedProgressHandler(
            dbContext,
            masteryService,
            new FakeLessonScheduleDirectory(null),
            new FakeLessonSessionAccessService(null),
            new FixedClock(Now));

        await handler.HandleAsync(Envelope(lessonSessionId, lessonScheduleId: null), CancellationToken.None);

        Assert.Empty(await dbContext.TopicMasteries.ToListAsync());
        Assert.Single(await dbContext.Set<InboxMessage>().ToListAsync());
    }

    // --- M10-3: hedef otomatik kapanışı --------------------------------------------------------

    [Fact]
    public async Task Goal_Should_Close_Automatically_When_Target_Level_Reached()
    {
        await using var dbContext = NewDbContext();
        var repository = new ProgressRepository(dbContext);
        var masteryService = new MasteryService(repository, new SeqIdGenerator(), new FixedClock(Now));
        var goal = new TopicGoal(
            Guid.NewGuid(), StudentId, "Matematik", "Türev", MasteryLevel.Developing,
            null, StudentUserId, TopicGoalSetterRole.Student, null, Now);
        dbContext.TopicGoals.Add(goal);
        await dbContext.SaveChangesAsync();

        // 20 soruluk testte 18 net → skor 45'in üstüne çıkar (Proficient ≥ Developing).
        await masteryService.ApplyTestAsync(StudentId, "Matematik", "Türev", 20, 18m, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(TopicGoalStatus.Achieved, goal.Status);
        Assert.Equal(Now, goal.AchievedOnUtc);
    }

    [Fact]
    public async Task Goal_With_Net_Target_Should_Stay_Active_Until_Net_Is_Reached()
    {
        await using var dbContext = NewDbContext();
        var repository = new ProgressRepository(dbContext);
        var masteryService = new MasteryService(repository, new SeqIdGenerator(), new FixedClock(Now));
        var goal = new TopicGoal(
            Guid.NewGuid(), StudentId, "Matematik", "Türev", MasteryLevel.Developing,
            targetNetRatio: 0.95m, StudentUserId, TopicGoalSetterRole.Student, null, Now);
        dbContext.TopicGoals.Add(goal);
        await dbContext.SaveChangesAsync();

        // Seviye hedefi tutar (0.90 net → Proficient) ama net hedefi (0.95) tutmaz.
        await masteryService.ApplyTestAsync(StudentId, "Matematik", "Türev", 20, 18m, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(TopicGoalStatus.Active, goal.Status);
    }

    [Fact]
    public void Achieved_Goal_Should_Raise_Domain_Event()
    {
        var goal = new TopicGoal(
            Guid.NewGuid(), StudentId, "Matematik", "Türev", MasteryLevel.Developing,
            null, StudentUserId, TopicGoalSetterRole.Student, null, Now);

        goal.MarkAchieved(Now);

        var raised = Assert.Single(goal.DomainEvents.OfType<TopicGoalAchievedDomainEvent>());
        Assert.Equal(StudentId, raised.StudentId);
        Assert.Equal("Türev", raised.Topic);
    }

    // --- Yardımcılar ---------------------------------------------------------------------------

    private static IntegrationEvent Envelope(Guid lessonSessionId, Guid? lessonScheduleId)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                lessonSessionId,
                lessonScheduleId,
                teacherUserId = TeacherUserId,
                studentId = StudentId,
                completedOnUtc = Now
            },
            IntegrationEventSerialization.Options);

        return new IntegrationEvent(
            Guid.NewGuid(), Now, "LessonSessionCompletedDomainEvent", "LessonSessions", payload);
    }

    private static ProgressTrackingDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ProgressTrackingDbContext>()
            .UseInMemoryDatabase($"progress-access-{Guid.NewGuid():N}")
            .Options;
        return new ProgressTrackingDbContext(options, new NoOpDomainEventMapper());
    }

    /// <summary>Bir zayıf konu, bir güçlü konu ve bir aktif hedef içeren sabit veri.</summary>
    private static FakeOverviewRepository Repository()
    {
        var weak = new TopicMastery(Guid.NewGuid(), StudentId, "Matematik", "Türev", Now);
        weak.RegisterTest(20, 4m, Now);          // 0.20 net → Weak

        var strong = new TopicMastery(Guid.NewGuid(), StudentId, "Fizik", "Kuvvet", Now);
        strong.RegisterTest(20, 19m, Now);       // 0.95 net → Mastered

        var goal = new TopicGoal(
            Guid.NewGuid(), StudentId, "Matematik", "Türev", MasteryLevel.Proficient,
            null, StudentUserId, TopicGoalSetterRole.Student, null, Now);

        return new FakeOverviewRepository([weak, strong], [goal]);
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
        public Guid? OwnerUserId { get; init; }

        public Guid? LinkedTeacherUserId { get; init; }

        public Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(OwnerUserId);

        public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<AgreedLessonRate?>(null);

        public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(LinkedTeacherUserId == teacherUserId);
    }

    private sealed class FakeParentAccessDirectory : IParentAccessDirectory
    {
        public Guid? ApprovedStudentId { get; init; }

        public Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(ApprovedStudentId == studentId);

        public Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(Guid parentUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guid>>(ApprovedStudentId is { } id ? [id] : Array.Empty<Guid>());
    }

    private sealed class FakePrivacyDirectory(StudentPrivacy privacy) : IStudentPrivacyDirectory
    {
        public Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(privacy);
    }

    private sealed class FakeLessonScheduleDirectory(LessonScheduleDetails? details) : ILessonScheduleDirectory
    {
        public Task<LessonScheduleDetails?> GetAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult(details);

        public Task<IReadOnlyCollection<TeacherBusyInterval>> ListTeacherBusyIntervalsAsync(
            Guid teacherUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<TeacherBusyInterval>>(Array.Empty<TeacherBusyInterval>());

    }

    private sealed class FakeLessonSessionAccessService(LessonSessionDetails? details) : ILessonSessionAccessService
    {
        public Task<LessonSessionDetails?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult(details);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class SeqIdGenerator : IIdGenerator
    {
        private int _next;

        public Guid New() => new(++_next, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
    }

    private sealed class FakeOverviewRepository(
        IReadOnlyList<TopicMastery> masteries,
        IReadOnlyList<TopicGoal> goals) : IProgressRepository
    {
        public Task<TopicMastery?> GetMasteryAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken)
            => Task.FromResult(masteries.FirstOrDefault(m => m.Subject == subject && m.Topic == topic));

        public Task<IReadOnlyList<TopicMastery>> ListMasteryAsync(Guid studentId, string? subject, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TopicMastery>>(
                masteries.Where(m => subject is null || m.Subject == subject).ToArray());

        public Task AddMasteryAsync(TopicMastery mastery, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<TopicGoal?> GetGoalAsync(Guid goalId, CancellationToken cancellationToken)
            => Task.FromResult(goals.FirstOrDefault(g => g.Id == goalId));

        public Task<IReadOnlyList<TopicGoal>> ListGoalsAsync(Guid studentId, TopicGoalStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TopicGoal>>(
                goals.Where(g => status is null || g.Status == status).ToArray());

        public Task<IReadOnlyList<TopicGoal>> ListActiveGoalsForTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TopicGoal>>(Array.Empty<TopicGoal>());

        public Task AddGoalAsync(TopicGoal goal, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ProgressSnapshot>> ListSnapshotsAsync(
            Guid studentId, string? subject, SnapshotPeriod? period, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ProgressSnapshot>>(Array.Empty<ProgressSnapshot>());

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
