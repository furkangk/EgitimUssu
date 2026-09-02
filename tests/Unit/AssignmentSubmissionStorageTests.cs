using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Kernel.Storage;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04 Task 2: Ödev teslimi ortak <see cref="IFileStorage"/> üzerinden yapılır.
/// Kritik davranışlar: sınır/tür doğrulaması **sunucuda**, anahtar öneki `assignments/{assignmentId}`,
/// dosya yalnız yetki+komut başarılıysa yazılır, indirmede bilinmeyen anahtar 404 üretir.
/// </summary>
public sealed class AssignmentSubmissionStorageTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Upload_Should_Reject_Oversized_File()
    {
        var storage = new RecordingFileStorage();
        var assignment = CreateAssignment();
        var handler = CreateHandler(assignment, storage);

        var result = await handler.Handle(
            Command(assignment.Id, "odev.pdf", FileValidation.MaxSizeBytes + 1),
            default);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.file_too_large", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Upload_Should_Reject_Unsupported_Type()
    {
        var storage = new RecordingFileStorage();
        var assignment = CreateAssignment();
        var handler = CreateHandler(assignment, storage);

        var result = await handler.Handle(Command(assignment.Id, "virus.exe", 16), default);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.unsupported_type", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Upload_Should_Save_With_Assignments_Key_Prefix()
    {
        var storage = new RecordingFileStorage();
        var assignment = CreateAssignment();
        var handler = CreateHandler(assignment, storage);

        var result = await handler.Handle(Command(assignment.Id, "odev.pdf", 16), default);

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(storage.Saved);
        Assert.Equal($"assignments/{assignment.Id}", saved.KeyPrefix);
        Assert.Equal("odev.pdf", saved.FileName);
    }

    [Fact]
    public async Task Upload_Should_Record_Storage_Key_On_Assignment()
    {
        var storage = new RecordingFileStorage();
        var assignment = CreateAssignment();
        var handler = CreateHandler(assignment, storage);

        await handler.Handle(Command(assignment.Id, "odev.pdf", 16), default);

        Assert.Equal(storage.Saved.Single().ResultKey, assignment.AttachmentStorageKey);
        Assert.Equal("odev.pdf", assignment.AttachmentFileName);
        Assert.Equal("application/pdf", assignment.AttachmentContentType);
        Assert.Equal(16, assignment.AttachmentSizeBytes);
        Assert.Equal(AssignmentStatus.InProgress, assignment.Status);
        Assert.Equal($"/api/assignments/{assignment.Id}/attachment", assignment.AttachmentUrl);
    }

    [Fact]
    public async Task Upload_Should_Delete_Previous_Attachment()
    {
        var storage = new RecordingFileStorage();
        var assignment = CreateAssignment();
        var handler = CreateHandler(assignment, storage);

        await handler.Handle(Command(assignment.Id, "ilk.pdf", 16), default);
        var firstKey = assignment.AttachmentStorageKey;
        await handler.Handle(Command(assignment.Id, "ikinci.pdf", 16), default);

        Assert.Equal(new[] { firstKey }, storage.Deleted);
        Assert.NotEqual(firstKey, assignment.AttachmentStorageKey);
    }

    [Fact]
    public async Task Upload_To_Unknown_Assignment_Should_Not_Touch_Storage()
    {
        var storage = new RecordingFileStorage();
        var handler = CreateHandler(CreateAssignment(), storage);

        var result = await handler.Handle(Command(Guid.NewGuid(), "odev.pdf", 16), default);

        Assert.True(result.IsFailure);
        Assert.Equal("assignments.assignment_not_found", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Attachment_Query_Should_Return_Not_Found_When_No_Submission()
    {
        var assignment = CreateAssignment();
        var handler = new GetAssignmentAttachmentQueryHandler(new FakeAssignmentRepository(assignment));

        var result = await handler.Handle(new GetAssignmentAttachmentQuery(assignment.Id), default);

        Assert.True(result.IsFailure);
        Assert.Equal("assignments.attachment_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Attachment_Query_Should_Return_Stored_Metadata()
    {
        var storage = new RecordingFileStorage();
        var assignment = CreateAssignment();
        await CreateHandler(assignment, storage).Handle(Command(assignment.Id, "odev.pdf", 16), default);

        var result = await new GetAssignmentAttachmentQueryHandler(new FakeAssignmentRepository(assignment))
            .Handle(new GetAssignmentAttachmentQuery(assignment.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(assignment.AttachmentStorageKey, result.Value!.StorageKey);
        Assert.Equal("odev.pdf", result.Value.FileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
    }

    private static SubmitAssignmentWorkCommand Command(Guid assignmentId, string fileName, long sizeBytes)
        => new(
            assignmentId,
            fileName,
            fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream",
            sizeBytes,
            new MemoryStream(new byte[Math.Min(sizeBytes, 64)]));

    private static SubmitAssignmentWorkCommandHandler CreateHandler(Assignment assignment, IFileStorage storage)
        => new(new FakeAssignmentRepository(assignment), storage, new FixedClock(Now));

    private static Assignment CreateAssignment()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Matematik ödevi",
            null,
            null,
            AssignmentStatus.Pending,
            null,
            Now.AddDays(-1),
            null);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class RecordingFileStorage : IFileStorage
    {
        public List<(string KeyPrefix, string FileName, string ContentType, string ResultKey)> Saved { get; } = [];

        public List<string?> Deleted { get; } = [];

        public Task<StoredFile> SaveAsync(
            string keyPrefix,
            string fileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            var key = $"{keyPrefix}/{Saved.Count}{Path.GetExtension(fileName)}";
            Saved.Add((keyPrefix, fileName, contentType, key));
            return Task.FromResult(new StoredFile(key, fileName, contentType, content.Length));
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Deleted.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAssignmentRepository(Assignment assignment) : IAssignmentRepository
    {
        public Task<Assignment?> GetAssignmentByIdAsync(Guid assignmentId, CancellationToken cancellationToken)
            => Task.FromResult(assignment.Id == assignmentId ? assignment : null);

        public Task<LessonNote?> GetLessonNoteByLessonSessionIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult<LessonNote?>(null);

        public Task<IReadOnlyCollection<Assignment>> ListByLessonSessionIdAsync(Guid lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Assignment>>([assignment]);

        public Task<IReadOnlyCollection<Assignment>> ListAsync(Guid? teacherUserId, Guid? studentId, Guid? lessonSessionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Assignment>>([assignment]);

        public Task AddLessonNoteAsync(LessonNote lessonNote, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAssignmentsAsync(IEnumerable<Assignment> assignments, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
