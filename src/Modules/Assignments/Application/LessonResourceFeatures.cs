using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Modules.Assignments.Application;

// ---- M06-1 (P04): Ders kaynagi paylasimi ----

/// <summary>
/// Ders kaynagi olusturur. <see cref="Url"/> doluysa **baglanti**, <see cref="Content"/> doluysa
/// **dosya** kaynagi uretilir; ikisi de bossa istek gecersizdir.
/// </summary>
public sealed record CreateLessonResourceCommand(
    Guid TeacherUserId,
    Guid? StudentId,
    Guid? LessonSessionId,
    string Title,
    string? Description,
    string? Url,
    string? FileName,
    string? ContentType,
    long SizeBytes,
    Stream? Content) : ICommand<Result<LessonResourceResponse>>;

public sealed record ArchiveLessonResourceCommand(Guid ResourceId) : ICommand<Result>;

public sealed record ListLessonResourcesQuery(Guid? TeacherUserId, Guid? StudentId, Guid? LessonSessionId)
    : IQuery<Result<IReadOnlyCollection<LessonResourceResponse>>>;

/// <summary>Indirme icin kaynagin depolama anahtari ve meta verisi.</summary>
public sealed record GetLessonResourceFileQuery(Guid ResourceId) : IQuery<Result<LessonResourceFileResponse>>;

public sealed record LessonResourceFileResponse(string StorageKey, string FileName, string ContentType);

public sealed record LessonResourceResponse(
    Guid Id,
    Guid TeacherUserId,
    Guid? StudentId,
    Guid? LessonSessionId,
    string Title,
    string? Description,
    string Kind,
    string? FileName,
    long? SizeBytes,
    string? Url,
    string? DownloadUrl,
    DateTime CreatedOnUtc);

public interface ILessonResourceRepository
{
    Task<LessonResource?> GetByIdAsync(Guid resourceId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonResource>> ListAsync(
        Guid? teacherUserId,
        Guid? studentId,
        Guid? lessonSessionId,
        CancellationToken cancellationToken);

    Task AddAsync(LessonResource resource, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal static class LessonResourceErrors
{
    public static readonly Error NotFound = new("assignments.resource_not_found", "Ders kaynagi bulunamadi.");
    public static readonly Error Invalid = new("assignments.invalid_resource", "Kaynak icin dosya veya baglanti verilmelidir.");
    public static readonly Error NotAFile = new("assignments.resource_not_a_file", "Bu kaynak bir dosya degil.");
}

internal static class LessonResourceMappings
{
    public static LessonResourceResponse ToResponse(this LessonResource resource)
        => new(
            resource.Id,
            resource.TeacherUserId,
            resource.StudentId,
            resource.LessonSessionId,
            resource.Title,
            resource.Description,
            resource.Kind.ToString(),
            resource.FileName,
            resource.SizeBytes,
            resource.Url,
            resource.Kind == LessonResourceKind.File
                ? $"/api/assignments/lesson-resources/{resource.Id}/file"
                : null,
            resource.CreatedOnUtc);
}

/// <summary>
/// P04/M06-1: Dosya kaynaklarinda dogrulama ve depolama yazimi burada yapilir — yetkisiz istek
/// depoya hicbir sey birakmaz (authorizer dispatcher'da handler'dan once calisir).
/// </summary>
public sealed class CreateLessonResourceCommandHandler
    : ICommandHandler<CreateLessonResourceCommand, Result<LessonResourceResponse>>
{
    private readonly ILessonResourceRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonResourceCommandHandler(
        ILessonResourceRepository repository,
        IFileStorage fileStorage,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonResourceResponse>> Handle(
        CreateLessonResourceCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Result<LessonResourceResponse>.Failure(LessonResourceErrors.Invalid);
        }

        LessonResource resource;

        if (command.Content is not null && !string.IsNullOrWhiteSpace(command.FileName))
        {
            var validation = FileValidation.Validate(command.FileName, command.SizeBytes);
            if (validation.IsFailure)
            {
                return Result<LessonResourceResponse>.Failure(validation.Error);
            }

            var stored = await _fileStorage.SaveAsync(
                $"lesson-resources/{command.TeacherUserId}",
                command.FileName,
                string.IsNullOrWhiteSpace(command.ContentType) ? "application/octet-stream" : command.ContentType,
                command.Content,
                cancellationToken);

            resource = LessonResource.CreateFile(
                _idGenerator.New(),
                command.TeacherUserId,
                command.StudentId,
                command.LessonSessionId,
                command.Title,
                command.Description,
                stored.Key,
                stored.FileName,
                stored.ContentType,
                stored.SizeBytes,
                _clock.UtcNow);
        }
        else if (!string.IsNullOrWhiteSpace(command.Url))
        {
            resource = LessonResource.CreateLink(
                _idGenerator.New(),
                command.TeacherUserId,
                command.StudentId,
                command.LessonSessionId,
                command.Title,
                command.Description,
                command.Url,
                _clock.UtcNow);
        }
        else
        {
            return Result<LessonResourceResponse>.Failure(LessonResourceErrors.Invalid);
        }

        await _repository.AddAsync(resource, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonResourceResponse>.Success(resource.ToResponse());
    }
}

/// <summary>Yumusak silme: kaynak listelerden duser, dosya depoda kalir (gecmis ders kaydi bozulmasin).</summary>
public sealed class ArchiveLessonResourceCommandHandler : ICommandHandler<ArchiveLessonResourceCommand, Result>
{
    private readonly ILessonResourceRepository _repository;
    private readonly IClock _clock;

    public ArchiveLessonResourceCommandHandler(ILessonResourceRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(ArchiveLessonResourceCommand command, CancellationToken cancellationToken)
    {
        var resource = await _repository.GetByIdAsync(command.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Result.Failure(LessonResourceErrors.NotFound);
        }

        resource.Archive(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListLessonResourcesQueryHandler
    : IQueryHandler<ListLessonResourcesQuery, Result<IReadOnlyCollection<LessonResourceResponse>>>
{
    private readonly ILessonResourceRepository _repository;

    public ListLessonResourcesQueryHandler(ILessonResourceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonResourceResponse>>> Handle(
        ListLessonResourcesQuery query,
        CancellationToken cancellationToken)
    {
        var resources = await _repository.ListAsync(
            query.TeacherUserId,
            query.StudentId,
            query.LessonSessionId,
            cancellationToken);

        return Result<IReadOnlyCollection<LessonResourceResponse>>.Success(
            resources
                .OrderByDescending(resource => resource.CreatedOnUtc)
                .Select(resource => resource.ToResponse())
                .ToArray());
    }
}

public sealed class GetLessonResourceFileQueryHandler
    : IQueryHandler<GetLessonResourceFileQuery, Result<LessonResourceFileResponse>>
{
    private readonly ILessonResourceRepository _repository;

    public GetLessonResourceFileQueryHandler(ILessonResourceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<LessonResourceFileResponse>> Handle(
        GetLessonResourceFileQuery query,
        CancellationToken cancellationToken)
    {
        var resource = await _repository.GetByIdAsync(query.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Result<LessonResourceFileResponse>.Failure(LessonResourceErrors.NotFound);
        }

        if (resource.Kind != LessonResourceKind.File || string.IsNullOrWhiteSpace(resource.StorageKey))
        {
            return Result<LessonResourceFileResponse>.Failure(LessonResourceErrors.NotAFile);
        }

        return Result<LessonResourceFileResponse>.Success(
            new LessonResourceFileResponse(
                resource.StorageKey,
                resource.FileName ?? "kaynak",
                resource.ContentType ?? "application/octet-stream"));
    }
}
