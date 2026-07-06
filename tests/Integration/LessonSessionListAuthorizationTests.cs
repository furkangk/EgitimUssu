using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Kernel;

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
        var handler = new ListLessonSessionsQueryHandler(repository, FakeUser.Teacher(teacherId));

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
        var handler = new ListLessonSessionsQueryHandler(repository, FakeUser.Anonymous());

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
        var handler = new ListLessonSessionsQueryHandler(repository, FakeUser.Student(studentId));

        var result = await handler.Handle(
            new ListLessonSessionsQuery(Guid.NewGuid(), Guid.NewGuid(), null, null),
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
}
