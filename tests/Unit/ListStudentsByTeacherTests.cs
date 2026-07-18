using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class ListStudentsByTeacherTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherId = Guid.NewGuid();

    private static StudentProfile Profile(Guid id, string name)
        => new(id, null, TeacherId, null, name, "9", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

    private static TeacherStudentLink Link(Guid studentId, bool archived, decimal? rate)
    {
        var link = new TeacherStudentLink(Guid.NewGuid(), TeacherId, studentId, TeacherStudentLinkStatus.Manual, Now);
        if (rate is { } r)
        {
            link.SetRate(r, "TRY", Now);
        }

        if (archived)
        {
            link.Archive(Now);
        }

        return link;
    }

    private sealed class StubProfileRepo : IStudentProfileRepository
    {
        private readonly IReadOnlyDictionary<Guid, StudentProfile> _byId;
        public StubProfileRepo(params StudentProfile[] profiles) => _byId = profiles.ToDictionary(p => p.Id);

        public Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<StudentProfile>>(ids.Where(_byId.ContainsKey).Select(id => _byId[id]).ToArray());

        public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(StudentProfile profile, CancellationToken ct) => throw new NotImplementedException();
        public Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class StubLinkRepo : ITeacherStudentLinkRepository
    {
        private readonly IReadOnlyCollection<TeacherStudentLink> _links;
        public StubLinkRepo(params TeacherStudentLink[] links) => _links = links;

        public Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<TeacherStudentLink>>(
                _links.Where(l => includeArchived || !l.IsArchived).ToArray());

        public Task AddAsync(TeacherStudentLink link, CancellationToken ct) => throw new NotImplementedException();
        public Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TeacherStudentLink?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    [Fact]
    public async Task List_ExcludesArchived_ByDefault()
    {
        var active = Profile(Guid.NewGuid(), "Ali");
        var archived = Profile(Guid.NewGuid(), "Zeynep");
        var handler = new ListStudentsByTeacherQueryHandler(
            new StubProfileRepo(active, archived),
            new StubLinkRepo(Link(active.Id, archived: false, rate: 450m), Link(archived.Id, archived: true, rate: null)));

        var result = await handler.Handle(new ListStudentsByTeacherQuery(TeacherId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.Equal("Ali", item.FullName);
        Assert.False(item.IsArchived);
        Assert.Equal(450m, item.AgreedRateAmount);
        Assert.Equal("Manual", item.LinkStatus);
    }

    [Fact]
    public async Task List_IncludesArchived_WhenRequested()
    {
        var active = Profile(Guid.NewGuid(), "Ali");
        var archived = Profile(Guid.NewGuid(), "Zeynep");
        var handler = new ListStudentsByTeacherQueryHandler(
            new StubProfileRepo(active, archived),
            new StubLinkRepo(Link(active.Id, archived: false, rate: null), Link(archived.Id, archived: true, rate: null)));

        var result = await handler.Handle(new ListStudentsByTeacherQuery(TeacherId, IncludeArchived: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, s => s.IsArchived);
    }
}
