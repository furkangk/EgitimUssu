using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class AssignmentTeacherAuthorizerTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public string? Email { get; init; }
        public bool IsAuthenticated => UserId is not null;
        public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Teacher" };
    }

    private sealed class OneAssignmentRepo : IAssignmentRepository
    {
        private readonly Assignment _a;
        public OneAssignmentRepo(Assignment a) => _a = a;
        public Task<Assignment?> GetAssignmentByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Assignment?>(_a.Id == id ? _a : null);
        public Task<LessonNote?> GetLessonNoteByLessonSessionIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<Assignment>> ListByLessonSessionIdAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<Assignment>> ListAsync(Guid? t, Guid? s, Guid? l, CancellationToken ct) => throw new NotImplementedException();
        public Task AddLessonNoteAsync(LessonNote n, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAssignmentsAsync(IEnumerable<Assignment> a, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Approve_ByOtherTeacher_Forbidden()
    {
        var ownerTeacher = Guid.NewGuid();
        var assignment = new Assignment(Guid.NewGuid(), Guid.NewGuid(), ownerTeacher, null, "Ödev", null, null, AssignmentStatus.Completed, null, Now, null);
        var authorizer = new AssignmentTeacherAuthorizer(new FakeCurrentUser { UserId = Guid.NewGuid().ToString() }, new OneAssignmentRepo(assignment));

        var result = await authorizer.Authorize(new ApproveAssignmentCommand(assignment.Id, null), CancellationToken.None);
        Assert.False(result.IsSuccess);
    }
}
