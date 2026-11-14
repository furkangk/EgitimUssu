using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// Y2 / K2 regresyon koruması: liste sorgusu sahiplik filtresini <b>server tarafında</b> zorlar (varsayılan-deny);
/// istemcinin geçtiği filtreye güvenilmez, böylece IDOR (başkasının verisini listeleme) engellenir.
/// </summary>
public sealed class LessonSessionListAuthorizationTests
{
    [Fact]
    public async Task Teacher_Cannot_List_Other_Teachers_Sessions_Server_Forces_Own_Id()
    {
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var repository = new CapturingRepository();
        var handler = new ListLessonSessionsQueryHandler(repository, FakeUser.Teacher(teacherId), new StudentDirectory(), new ParentDirectory());

        // İstemci başka bir öğretmenin id'sini filtre olarak geçmeye çalışır.
        var result = await handler.Handle(
            new ListLessonSessionsQuery(otherTeacherId, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(repository.Called);
        Assert.Equal(teacherId, repository.CapturedTeacherFilter); // server kendi id'sini zorladı
        Assert.Null(repository.CapturedStudentFilter);
    }

    [Fact]
    public async Task Unauthenticated_List_Is_Denied_And_Repository_Not_Queried()
    {
        var repository = new CapturingRepository();
        var handler = new ListLessonSessionsQueryHandler(repository, FakeUser.Anonymous(), new StudentDirectory(), new ParentDirectory());

        var result = await handler.Handle(
            new ListLessonSessionsQuery(null, null, null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("shared.forbidden", result.Error.Code);
        Assert.False(repository.Called); // veri hiç okunmadı
    }

    [Fact]
    public async Task Student_List_Is_Scoped_To_Own_Id()
    {
        var studentId = Guid.NewGuid();
        var repository = new CapturingRepository();
        var handler = new ListLessonSessionsQueryHandler(repository, FakeUser.Student(studentId), new StudentDirectory(studentId), new ParentDirectory());

        var result = await handler.Handle(
            new ListLessonSessionsQuery(null, studentId, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(studentId, repository.CapturedStudentFilter);
    }

    private sealed class CapturingRepository : ILessonSessionRepository
    {
        public bool Called { get; private set; }

        public Guid? CapturedTeacherFilter { get; private set; }

        public Guid? CapturedStudentFilter { get; private set; }

        public Task<IReadOnlyCollection<LessonSession>> ListAsync(
            Guid? teacherUserId,
            Guid? studentId,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            CancellationToken cancellationToken)
        {
            Called = true;
            CapturedTeacherFilter = teacherUserId;
            CapturedStudentFilter = studentId;
            return Task.FromResult<IReadOnlyCollection<LessonSession>>(Array.Empty<LessonSession>());
        }

        public Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LessonSession?> GetByOccurrenceAsync(Guid lessonScheduleId, DateTime occurrenceStartUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public async Task<LessonSession> GetOrCreateAsync(LessonSession lessonSession, CancellationToken cancellationToken)
        {
            await AddAsync(lessonSession, cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return lessonSession;
        }

        public Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeUser : ICurrentUser
    {
        public string? UserId { get; init; }

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

        public bool IsAuthenticated { get; init; }

        public static FakeUser Teacher(Guid id) => new() { UserId = id.ToString(), Roles = ["Teacher"], IsAuthenticated = true };

        public static FakeUser Student(Guid id) => new() { UserId = id.ToString(), Roles = ["Student"], IsAuthenticated = true };

        public static FakeUser Anonymous() => new() { IsAuthenticated = false };
    }
private sealed class StudentDirectory(params Guid[] studentIds) : IStudentDirectory
{
    public Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>(studentIds);
    public Task<Guid?> GetOwnerUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Guid?>(null);
    public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacher, Guid id, CancellationToken ct) => Task.FromResult<AgreedLessonRate?>(null);
    public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacher, Guid id, CancellationToken ct) => Task.FromResult(false);
    public Task<string?> GetDisplayNameAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>(null);
}

    private sealed class ParentDirectory : IParentAccessDirectory
    {
        public Task<bool> IsApprovedParentOfStudentAsync(Guid parent, Guid student, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(Guid parent, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
}
