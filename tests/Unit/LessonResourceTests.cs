using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04 Task 4 (M06-1): Ders kaynağı paylaşımı. Kritik davranışlar: dosya kaynağı anahtarsız,
/// bağlantı kaynağı URL'siz oluşturulamaz; arşivleme yumuşak silmedir; öğrenci yalnız
/// kendisine açık kaynakları görür.
/// </summary>
public sealed class LessonResourceTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void File_Resource_Requires_StorageKey()
        => Assert.Throws<ArgumentException>(() => LessonResource.CreateFile(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "Konu anlatimi", null,
            storageKey: "", fileName: "a.pdf", contentType: "application/pdf", sizeBytes: 1, createdOnUtc: Now));

    [Fact]
    public void Link_Resource_Requires_Url()
        => Assert.Throws<ArgumentException>(() => LessonResource.CreateLink(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "Video", null, url: "", createdOnUtc: Now));

    [Fact]
    public void Resource_Requires_Title()
        => Assert.Throws<ArgumentException>(() => LessonResource.CreateLink(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "  ", null, "https://ornek.com", Now));

    [Fact]
    public void Archive_Should_Deactivate()
    {
        var resource = CreateLink();

        resource.Archive(Now.AddDays(1));

        Assert.False(resource.IsActive);
    }

    [Fact]
    public void File_Resource_Should_Carry_Metadata()
    {
        var resource = LessonResource.CreateFile(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "Konu anlatimi", "PDF",
            "assignments/resources/abc.pdf", "konu.pdf", "application/pdf", 2048, Now);

        Assert.Equal(LessonResourceKind.File, resource.Kind);
        Assert.Equal("assignments/resources/abc.pdf", resource.StorageKey);
        Assert.Equal("konu.pdf", resource.FileName);
        Assert.Equal(2048, resource.SizeBytes);
        Assert.Null(resource.Url);
        Assert.True(resource.IsActive);
    }

    [Fact]
    public async Task Create_Link_Should_Persist_Resource()
    {
        var teacherUserId = Guid.NewGuid();
        var repository = new FakeLessonResourceRepository();
        var handler = new CreateLessonResourceCommandHandler(
            repository, new RecordingFileStorage(), new SeqIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(
            new CreateLessonResourceCommand(teacherUserId, null, null, "Video", null, "https://ornek.com/ders", null, null, 0, null),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Link", result.Value!.Kind);
        Assert.Single(repository.Added);
    }

    [Fact]
    public async Task Create_Without_File_Or_Url_Should_Fail()
    {
        var handler = new CreateLessonResourceCommandHandler(
            new FakeLessonResourceRepository(), new RecordingFileStorage(), new SeqIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(
            new CreateLessonResourceCommand(Guid.NewGuid(), null, null, "Kaynak", null, null, null, null, 0, null),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("assignments.invalid_resource", result.Error.Code);
    }

    [Fact]
    public async Task Create_File_Should_Reject_Unsupported_Type()
    {
        var storage = new RecordingFileStorage();
        var handler = new CreateLessonResourceCommandHandler(
            new FakeLessonResourceRepository(), storage, new SeqIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(
            new CreateLessonResourceCommand(
                Guid.NewGuid(), null, null, "Kaynak", null, null, "virus.exe", "application/octet-stream", 16,
                new MemoryStream(new byte[16])),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.unsupported_type", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Create_File_Should_Use_Resource_Key_Prefix()
    {
        var teacherUserId = Guid.NewGuid();
        var storage = new RecordingFileStorage();
        var handler = new CreateLessonResourceCommandHandler(
            new FakeLessonResourceRepository(), storage, new SeqIdGenerator(), new FixedClock(Now));

        var result = await handler.Handle(
            new CreateLessonResourceCommand(
                teacherUserId, null, null, "Konu anlatimi", null, null, "konu.pdf", "application/pdf", 16,
                new MemoryStream(new byte[16])),
            default);

        Assert.True(result.IsSuccess);
        Assert.Equal($"lesson-resources/{teacherUserId}", storage.Saved.Single());
        Assert.Equal("File", result.Value!.Kind);
    }

    [Fact]
    public async Task Archive_Should_Fail_For_Unknown_Resource()
    {
        var handler = new ArchiveLessonResourceCommandHandler(
            new FakeLessonResourceRepository(), new FixedClock(Now));

        var result = await handler.Handle(new ArchiveLessonResourceCommand(Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal("assignments.resource_not_found", result.Error.Code);
    }

    [Fact]
    public async Task List_Should_Return_Only_Active_Resources()
    {
        var teacherUserId = Guid.NewGuid();
        var archived = CreateLink(teacherUserId);
        archived.Archive(Now);
        var repository = new FakeLessonResourceRepository(CreateLink(teacherUserId), archived);
        var handler = new ListLessonResourcesQueryHandler(repository);

        var result = await handler.Handle(
            new ListLessonResourcesQuery(teacherUserId, null, null),
            default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task Teacher_Can_Manage_Own_Resource_Only()
    {
        var teacherUserId = Guid.NewGuid();
        var resource = CreateLink(teacherUserId);
        var repository = new FakeLessonResourceRepository(resource);

        var owner = new LessonResourceAuthorizer(new FakeCurrentUser(teacherUserId, ["Teacher"]), repository);
        var stranger = new LessonResourceAuthorizer(new FakeCurrentUser(Guid.NewGuid(), ["Teacher"]), repository);

        Assert.True((await owner.Authorize(new ArchiveLessonResourceCommand(resource.Id), default)).IsSuccess);
        Assert.False((await stranger.Authorize(new ArchiveLessonResourceCommand(resource.Id), default)).IsSuccess);
    }

    [Fact]
    public async Task Student_Can_Only_List_Own_Resources()
    {
        var studentId = Guid.NewGuid();
        var repository = new FakeLessonResourceRepository();
        var authorizer = new LessonResourceAuthorizer(new FakeCurrentUser(studentId, ["Student"]), repository);

        var own = await authorizer.Authorize(new ListLessonResourcesQuery(null, studentId, null), default);
        var others = await authorizer.Authorize(new ListLessonResourcesQuery(null, Guid.NewGuid(), null), default);

        Assert.True(own.IsSuccess);
        Assert.False(others.IsSuccess);
    }

    [Fact]
    public async Task Anonymous_Should_Be_Denied()
    {
        var authorizer = new LessonResourceAuthorizer(
            new FakeCurrentUser(null, []), new FakeLessonResourceRepository());

        Assert.False((await authorizer.Authorize(new ListLessonResourcesQuery(null, null, null), default)).IsSuccess);
    }

    private static LessonResource CreateLink(Guid? teacherUserId = null)
        => LessonResource.CreateLink(
            Guid.NewGuid(), teacherUserId ?? Guid.NewGuid(), null, null, "Video", null, "https://ornek.com/ders", Now);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SeqIdGenerator : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class FakeCurrentUser(Guid? userId, string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId?.ToString();

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsAuthenticated { get; } = userId.HasValue;
    }

    private sealed class RecordingFileStorage : IFileStorage
    {
        public List<string> Saved { get; } = [];

        public Task<StoredFile> SaveAsync(
            string keyPrefix,
            string fileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            Saved.Add(keyPrefix);
            return Task.FromResult(
                new StoredFile($"{keyPrefix}/{Saved.Count}{Path.GetExtension(fileName)}", fileName, contentType, content.Length));
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLessonResourceRepository(params LessonResource[] resources) : ILessonResourceRepository
    {
        private readonly List<LessonResource> _items = [.. resources];

        public List<LessonResource> Added { get; } = [];

        public Task<LessonResource?> GetByIdAsync(Guid resourceId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(r => r.Id == resourceId));

        public Task<IReadOnlyCollection<LessonResource>> ListAsync(
            Guid? teacherUserId,
            Guid? studentId,
            Guid? lessonSessionId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonResource>>(
                _items
                    .Where(r => r.IsActive)
                    .Where(r => teacherUserId is null || r.TeacherUserId == teacherUserId)
                    .Where(r => studentId is null || r.StudentId == studentId || r.StudentId is null)
                    .Where(r => lessonSessionId is null || r.LessonSessionId == lessonSessionId)
                    .ToArray());

        public Task AddAsync(LessonResource resource, CancellationToken cancellationToken)
        {
            _items.Add(resource);
            Added.Add(resource);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
