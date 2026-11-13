using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Tests.Unit.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 T2 (N-08, N-21): Öğrenciye kayıt <b>yazan</b> her uç, öğretmenin o öğrenciyle yaşayan özel ders
/// bağı olmasını (<see cref="TeacherAccessScope.Private"/>) ister. Eskiden yalnız "komuttaki öğretmen ben miyim"
/// soruluyordu: bağı arşivlenmiş eski öğretmen ders ve <b>borç kaydı</b> ekleyebiliyor, öğrenci kimliğini
/// bilen herhangi bir öğretmen veli daveti üretip kendisi kullanabiliyordu.
/// <para>
/// Kapsam gerçek <see cref="TeacherStudentAccess"/> sorgusundan gelir (EF InMemory) — üç saldırgan
/// gerçekten farklı veri durumlarıdır:
/// <list type="bullet">
/// <item><b>Başka öğretmen</b> (B): A'nın adına ya da A'nın kaydı üzerinde iş yapmaya çalışır.</item>
/// <item><b>Bağı arşivlenmiş öğretmen</b> (C): öğrenciyle <c>Linked</c> bağı vardı, arşivledi.</item>
/// <item><b>Bağsız öğretmen</b> (B): kendi adına, hiç bağı olmayan öğrenciye yazar.</item>
/// </list>
/// </para>
/// </summary>
public sealed class WriteEndpointLinkAuthorizationTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherA = Guid.NewGuid();
    private static readonly Guid TeacherB = Guid.NewGuid();
    private static readonly Guid TeacherC = Guid.NewGuid();
    private static readonly Guid AdminUser = Guid.NewGuid();

    public enum Intruder
    {
        OtherTeacher,
        ArchivedTeacher,
        UnlinkedTeacher
    }

    // --- Scheduling ----------------------------------------------------------------------

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task CreateLessonSchedule_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);

        var result = await world.Scheduling(Teacher(actor)).Authorize(CreateLesson(owner, world.StudentId), default);

        AssertForbidden(result);
    }

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task UpdateLessonSchedule_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);
        var lesson = Lesson(owner, world.StudentId);

        var result = await world.Scheduling(Teacher(actor), lesson).Authorize(UpdateLesson(lesson.Id), default);

        AssertForbidden(result);
    }

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task RescheduleLessonSchedule_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);
        var lesson = Lesson(owner, world.StudentId);

        var result = await world.Scheduling(Teacher(actor), lesson)
            .Authorize(new RescheduleLessonScheduleCommand(lesson.Id, Now.AddDays(2), Now.AddDays(2).AddHours(1), null), default);

        AssertForbidden(result);
    }

    [Fact]
    public async Task Linked_Teacher_Can_Create_Update_And_Reschedule_Lessons()
    {
        var world = await World.CreateAsync();
        var lesson = Lesson(TeacherA, world.StudentId);
        var authorizer = world.Scheduling(Teacher(TeacherA), lesson);

        Assert.True((await authorizer.Authorize(CreateLesson(TeacherA, world.StudentId), default)).IsSuccess);
        Assert.True((await authorizer.Authorize(UpdateLesson(lesson.Id), default)).IsSuccess);
        Assert.True((await authorizer.Authorize(
            new RescheduleLessonScheduleCommand(lesson.Id, Now.AddDays(2), Now.AddDays(2).AddHours(1), null), default)).IsSuccess);
    }

    /// <summary>Bağı biten öğretmen eski kayıtlarını temizleyebilmeli: iptal ve silme kapsam sormaz.</summary>
    [Fact]
    public async Task Archived_Teacher_Can_Still_Cancel_And_Delete_Own_Lessons()
    {
        var world = await World.CreateAsync();
        var lesson = Lesson(TeacherC, world.StudentId);
        var authorizer = world.Scheduling(Teacher(TeacherC), lesson);

        Assert.True((await authorizer.Authorize(
            new CancelLessonScheduleCommand(lesson.Id, CancellationReason.TeacherCancelled, false, null), default)).IsSuccess);
        Assert.True((await authorizer.Authorize(new DeleteLessonScheduleCommand(lesson.Id), default)).IsSuccess);
    }

    [Fact]
    public async Task Admin_Bypasses_Scope_For_Lessons()
    {
        var world = await World.CreateAsync();

        var result = await world.Scheduling(Admin()).Authorize(CreateLesson(TeacherB, world.StudentId), default);

        Assert.True(result.IsSuccess);
    }

    // --- Payments ------------------------------------------------------------------------

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task CreatePayment_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);

        var result = await world.Payments(Teacher(actor)).Authorize(CreatePayment(owner, world.StudentId), default);

        AssertForbidden(result);
    }

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task UpdatePayment_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);
        var record = Payment(owner, world.StudentId);

        var result = await world.Payments(Teacher(actor), record).Authorize(UpdatePayment(record.Id), default);

        AssertForbidden(result);
    }

    [Fact]
    public async Task Linked_Teacher_Can_Create_And_Update_Payments_And_Admin_Bypasses()
    {
        var world = await World.CreateAsync();
        var record = Payment(TeacherA, world.StudentId);

        Assert.True((await world.Payments(Teacher(TeacherA), record)
            .Authorize(CreatePayment(TeacherA, world.StudentId), default)).IsSuccess);
        Assert.True((await world.Payments(Teacher(TeacherA), record)
            .Authorize(UpdatePayment(record.Id), default)).IsSuccess);
        Assert.True((await world.Payments(Admin())
            .Authorize(CreatePayment(TeacherB, world.StudentId), default)).IsSuccess);
    }

    // --- LessonSessions ------------------------------------------------------------------

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task CreateLessonSession_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);

        var result = await world.Sessions(Teacher(actor)).Authorize(CreateSession(null, owner, world.StudentId), default);

        AssertForbidden(result);
    }

    [Fact]
    public async Task Linked_Teacher_Can_Create_A_Manual_Session_With_Or_Without_Own_Schedule()
    {
        var world = await World.CreateAsync();
        var ownSchedule = Schedule(TeacherA, world.StudentId);
        var authorizer = world.Sessions(Teacher(TeacherA), ownSchedule);

        Assert.True((await authorizer.Authorize(CreateSession(null, TeacherA, world.StudentId), default)).IsSuccess);
        Assert.True((await authorizer.Authorize(
            CreateSession(ownSchedule.LessonScheduleId, TeacherA, world.StudentId), default)).IsSuccess);
    }

    /// <summary>
    /// N-08 ikinci yarı: manuel oturuma başka öğretmenin planlı dersi yazılırsa, asıl öğretmenin
    /// <c>from-schedule</c> isteği bu yabancı oturumu döndürür. Bilinmeyen plan ve başka öğrencinin planı
    /// da aynı hatayla reddedilir (plan, oturumun öğretmen–öğrenci ikilisine ait olmalı).
    /// </summary>
    [Fact]
    public async Task Manual_Session_With_A_Schedule_That_Is_Not_The_Teachers_Is_Rejected()
    {
        var world = await World.CreateAsync();
        var foreignSchedule = Schedule(TeacherB, world.StudentId);
        var otherStudentsSchedule = Schedule(TeacherA, Guid.NewGuid());
        var authorizer = world.Sessions(Teacher(TeacherA), foreignSchedule, otherStudentsSchedule);

        var foreign = await authorizer.Authorize(
            CreateSession(foreignSchedule.LessonScheduleId, TeacherA, world.StudentId), default);
        var unknown = await authorizer.Authorize(
            CreateSession(Guid.NewGuid(), TeacherA, world.StudentId), default);
        var otherStudent = await authorizer.Authorize(
            CreateSession(otherStudentsSchedule.LessonScheduleId, TeacherA, world.StudentId), default);

        Assert.Equal("lesson_sessions.schedule_not_owned", foreign.Error.Code);
        Assert.Equal("lesson_sessions.schedule_not_owned", unknown.Error.Code);
        Assert.Equal("lesson_sessions.schedule_not_owned", otherStudent.Error.Code);
    }

    [Fact]
    public async Task Admin_Bypasses_Scope_But_Not_Schedule_Ownership_For_Sessions()
    {
        var world = await World.CreateAsync();
        var foreignSchedule = Schedule(TeacherA, world.StudentId);
        var authorizer = world.Sessions(Admin(), foreignSchedule);

        Assert.True((await authorizer.Authorize(CreateSession(null, TeacherB, world.StudentId), default)).IsSuccess);
        Assert.Equal(
            "lesson_sessions.schedule_not_owned",
            (await authorizer.Authorize(CreateSession(foreignSchedule.LessonScheduleId, TeacherB, world.StudentId), default)).Error.Code);
    }

    // --- Students: veli daveti (N-21) ----------------------------------------------------

    [Theory]
    [InlineData(Intruder.OtherTeacher)]
    [InlineData(Intruder.ArchivedTeacher)]
    [InlineData(Intruder.UnlinkedTeacher)]
    public async Task CreateParentInvite_Is_Forbidden_Without_Private_Scope(Intruder intruder)
    {
        var world = await World.CreateAsync();
        var (actor, owner) = Pair(intruder);

        var result = await world.ParentInvites(Teacher(actor))
            .Authorize(new CreateParentInviteCommand(world.StudentId, owner, "Ada"), default);

        AssertForbidden(result);
    }

    [Fact]
    public async Task Linked_Teacher_And_Admin_Can_Create_Parent_Invites()
    {
        var world = await World.CreateAsync();

        Assert.True((await world.ParentInvites(Teacher(TeacherA))
            .Authorize(new CreateParentInviteCommand(world.StudentId, TeacherA, "Ada"), default)).IsSuccess);
        Assert.True((await world.ParentInvites(Admin())
            .Authorize(new CreateParentInviteCommand(world.StudentId, TeacherB, "Ada"), default)).IsSuccess);
    }

    // --- Yardımcılar ---------------------------------------------------------------------

    private static (Guid Actor, Guid Owner) Pair(Intruder intruder) => intruder switch
    {
        Intruder.OtherTeacher => (TeacherB, TeacherA),
        Intruder.ArchivedTeacher => (TeacherC, TeacherC),
        Intruder.UnlinkedTeacher => (TeacherB, TeacherB),
        _ => throw new ArgumentOutOfRangeException(nameof(intruder))
    };

    private static void AssertForbidden(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    private static FakeCurrentUser Teacher(Guid userId) => new(userId, "Teacher");

    private static FakeCurrentUser Admin() => new(AdminUser, "Admin");

    private static CreateLessonScheduleCommand CreateLesson(Guid teacherUserId, Guid studentId) =>
        new(teacherUserId, studentId, "Matematik", ScheduledLessonFormat.Online, Now.AddDays(1), Now.AddDays(1).AddHours(1),
            "Europe/Istanbul", null, 60, null, null, null);

    private static UpdateLessonScheduleCommand UpdateLesson(Guid lessonId) =>
        new(lessonId, "Matematik", ScheduledLessonFormat.Online, Now.AddDays(1), Now.AddDays(1).AddHours(1),
            "Europe/Istanbul", null, 60, null, null, null);

    private static LessonSchedule Lesson(Guid teacherUserId, Guid studentId) =>
        new(Guid.NewGuid(), teacherUserId, studentId, "Matematik", ScheduledLessonFormat.Online, Now.AddDays(1),
            Now.AddDays(1).AddHours(1), "Europe/Istanbul", null, LessonScheduleStatus.Planned, 60, null, null, null, Now);

    private static CreatePaymentRecordCommand CreatePayment(Guid teacherUserId, Guid studentId) =>
        new(teacherUserId, studentId, null, BillingItemType.LessonFee, "Ders ücreti", "TRY", 100m, 0m,
            Now.AddDays(7), null, PaymentStatus.Pending, null, null, null);

    private static UpdatePaymentRecordCommand UpdatePayment(Guid paymentRecordId) =>
        new(paymentRecordId, null, BillingItemType.LessonFee, "Ders ücreti", "TRY", 150m, 0m,
            Now.AddDays(7), null, PaymentStatus.Pending, null, null, null);

    private static PaymentRecord Payment(Guid teacherUserId, Guid studentId) =>
        new(Guid.NewGuid(), teacherUserId, studentId, null, BillingItemType.LessonFee, "Ders ücreti", "TRY",
            100m, 0m, Now.AddDays(7), null, PaymentStatus.Pending, null, null, null, Now.AddDays(-1));

    private static CreateLessonSessionCommand CreateSession(Guid? lessonScheduleId, Guid teacherUserId, Guid studentId) =>
        new(lessonScheduleId, teacherUserId, studentId, "Matematik", Now.AddDays(1), "Problemler");

    private static LessonScheduleDetails Schedule(Guid teacherUserId, Guid studentId) =>
        new(Guid.NewGuid(), teacherUserId, studentId, Now.AddDays(1), Now.AddDays(1).AddHours(1), "Matematik", null, "Planned");

    /// <summary>
    /// Öğrenci S'yi öğretmen A açtı (<c>Manual</c> bağ). Öğretmen C'nin S ile <c>Linked</c> bağı vardı, arşivledi.
    /// Öğretmen B'nin S ile hiç bağı yok.
    /// </summary>
    private sealed class World
    {
        private readonly StudentsDbContext _context;

        private World(StudentsDbContext context, Guid studentId)
        {
            _context = context;
            StudentId = studentId;
        }

        public Guid StudentId { get; }

        public static async Task<World> CreateAsync()
        {
            var databaseName = Guid.NewGuid().ToString();
            var student = new StudentProfile(
                Guid.NewGuid(), null, TeacherA, null, "Ada Şahin", "7. Sınıf", null, null, null, null,
                StudentOrigin.TeacherManaged, true, Now);
            var archivedLink = new TeacherStudentLink(Guid.NewGuid(), TeacherC, student.Id, TeacherStudentLinkStatus.Linked, Now);
            archivedLink.Archive(Now);

            await using (var seed = NewContext(databaseName))
            {
                seed.StudentProfiles.Add(student);
                seed.TeacherStudentLinks.Add(new TeacherStudentLink(Guid.NewGuid(), TeacherA, student.Id, TeacherStudentLinkStatus.Manual, Now));
                seed.TeacherStudentLinks.Add(archivedLink);
                await seed.SaveChangesAsync();
            }

            return new World(NewContext(databaseName), student.Id);
        }

        private ITeacherStudentAccess Access => new TeacherStudentAccess(_context);

        public LessonScheduleCommandAuthorizer Scheduling(ICurrentUser user, LessonSchedule? lesson = null)
            => new(user, new SingleLessonRepository(lesson), Access);

        public PaymentRecordAuthorizer Payments(ICurrentUser user, PaymentRecord? record = null)
            => new(user, new SinglePaymentRepository(record), new FakeRequestEntityCache(), Access);

        public LessonSessionCommandAuthorizer Sessions(ICurrentUser user, params LessonScheduleDetails[] schedules)
            => new(user, new EmptySessionRepository(), Access, new FakeLessonScheduleDirectory(schedules));

        public CreateParentInviteCommandAuthorizer ParentInvites(ICurrentUser user) => new(user, Access);

        private static StudentsDbContext NewContext(string databaseName) =>
            new(new DbContextOptionsBuilder<StudentsDbContext>().UseInMemoryDatabase(databaseName).Options,
                new JsonDomainEventMapper());
    }

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsAuthenticated => true;
    }

    private sealed class SingleLessonRepository(LessonSchedule? lesson) : ILessonScheduleRepository
    {
        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
            => Task.FromResult(lesson is not null && lesson.Id == lessonId ? lesson : null);

        public Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Remove(LessonSchedule lessonSchedule) => throw new NotSupportedException();

        public Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SinglePaymentRepository(PaymentRecord? record) : IPaymentRecordRepository
    {
        public Task<PaymentRecord?> GetByIdAsync(Guid paymentRecordId, CancellationToken cancellationToken)
            => Task.FromResult(record is not null && record.Id == paymentRecordId ? record : null);

        public Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddAsync(PaymentRecord paymentRecord, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptySessionRepository : ILessonSessionRepository
    {
        public Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult<LessonSession?>(null);

        public Task<LessonSession?> GetByScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult<LessonSession?>(null);

        public Task<IReadOnlyCollection<LessonSession>> ListAsync(
            Guid? teacherUserId, Guid? studentId, DateTime? dateFromUtc, DateTime? dateToUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSession>>([]);

        public Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
