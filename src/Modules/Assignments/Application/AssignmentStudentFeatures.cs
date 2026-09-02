using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Modules.Assignments.Application;

// ---- Öğrenci ödev aksiyonları (tamamlama + teslim) ----

public sealed record GetAssignmentQuery(Guid AssignmentId) : IQuery<Result<AssignmentResponse>>;

public sealed record MarkAssignmentCompletedCommand(Guid AssignmentId) : ICommand<Result<AssignmentResponse>>;

/// <summary>
/// P04: Öğrenci teslimi. Dosya <see cref="Content"/> ile taşınır; handler doğrular, depolar ve
/// anahtarı ödeve bağlar. <see cref="SizeBytes"/> ayrıca taşınır — akış ileri-sarılamayabilir.
/// </summary>
public sealed record SubmitAssignmentWorkCommand(
    Guid AssignmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content) : ICommand<Result<AssignmentResponse>>;

/// <summary>P04: İndirme için teslim dosyasının depolama anahtarı ve meta verisi.</summary>
public sealed record GetAssignmentAttachmentQuery(Guid AssignmentId)
    : IQuery<Result<AssignmentAttachmentResponse>>;

public sealed record AssignmentAttachmentResponse(string StorageKey, string FileName, string ContentType);

internal static class AssignmentErrors
{
    public static readonly Error NotFound = new("assignments.assignment_not_found", "Ödev bulunamadı.");
    public static readonly Error InvalidRequest = new("assignments.invalid_request", "Ödev teslim bilgisi eksik ya da hatalı.");
    public static readonly Error AttachmentNotFound = new("assignments.attachment_not_found", "Ödev eki bulunamadı.");
}

public sealed class GetAssignmentQueryHandler : IQueryHandler<GetAssignmentQuery, Result<AssignmentResponse>>
{
    private readonly IAssignmentRepository _repository;

    public GetAssignmentQueryHandler(IAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<AssignmentResponse>> Handle(GetAssignmentQuery query, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetAssignmentByIdAsync(query.AssignmentId, cancellationToken);
        return assignment is null
            ? Result<AssignmentResponse>.Failure(AssignmentErrors.NotFound)
            : Result<AssignmentResponse>.Success(assignment.ToResponse());
    }
}

public sealed class MarkAssignmentCompletedCommandHandler
    : ICommandHandler<MarkAssignmentCompletedCommand, Result<AssignmentResponse>>
{
    private readonly IAssignmentRepository _repository;
    private readonly IClock _clock;

    public MarkAssignmentCompletedCommandHandler(IAssignmentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<AssignmentResponse>> Handle(MarkAssignmentCompletedCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetAssignmentByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result<AssignmentResponse>.Failure(AssignmentErrors.NotFound);
        }

        assignment.MarkCompleted(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<AssignmentResponse>.Success(assignment.ToResponse());
    }
}

/// <summary>
/// P04 (C-02): Teslim dosyasını ortak <see cref="IFileStorage"/> üzerinden saklar.
/// Sıra önemlidir: **önce** doğrulama ve ödevin varlığı, **sonra** yazma — yetkisiz/hatalı bir istek
/// depoya hiçbir şey bırakmaz. Yeni dosya bağlandığında eskisi silinir (yetim dosya kalmaz).
/// </summary>
public sealed class SubmitAssignmentWorkCommandHandler
    : ICommandHandler<SubmitAssignmentWorkCommand, Result<AssignmentResponse>>
{
    private readonly IAssignmentRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly IClock _clock;

    public SubmitAssignmentWorkCommandHandler(
        IAssignmentRepository repository,
        IFileStorage fileStorage,
        IClock clock)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _clock = clock;
    }

    public async Task<Result<AssignmentResponse>> Handle(SubmitAssignmentWorkCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.FileName))
        {
            return Result<AssignmentResponse>.Failure(AssignmentErrors.InvalidRequest);
        }

        var validation = FileValidation.Validate(command.FileName, command.SizeBytes);
        if (validation.IsFailure)
        {
            return Result<AssignmentResponse>.Failure(validation.Error);
        }

        var assignment = await _repository.GetAssignmentByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result<AssignmentResponse>.Failure(AssignmentErrors.NotFound);
        }

        var stored = await _fileStorage.SaveAsync(
            $"assignments/{command.AssignmentId}",
            command.FileName,
            command.ContentType,
            command.Content,
            cancellationToken);

        var previousKey = assignment.AttachSubmission(
            stored.Key,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousKey))
        {
            await _fileStorage.DeleteAsync(previousKey, cancellationToken);
        }

        return Result<AssignmentResponse>.Success(assignment.ToResponse());
    }
}

/// <summary>P04: İndirme öncesi teslim dosyasının anahtarını ve adını çözer (yetki authorizer'da).</summary>
public sealed class GetAssignmentAttachmentQueryHandler
    : IQueryHandler<GetAssignmentAttachmentQuery, Result<AssignmentAttachmentResponse>>
{
    private readonly IAssignmentRepository _repository;

    public GetAssignmentAttachmentQueryHandler(IAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<AssignmentAttachmentResponse>> Handle(
        GetAssignmentAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetAssignmentByIdAsync(query.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result<AssignmentAttachmentResponse>.Failure(AssignmentErrors.NotFound);
        }

        if (string.IsNullOrWhiteSpace(assignment.AttachmentStorageKey))
        {
            return Result<AssignmentAttachmentResponse>.Failure(AssignmentErrors.AttachmentNotFound);
        }

        return Result<AssignmentAttachmentResponse>.Success(
            new AssignmentAttachmentResponse(
                assignment.AttachmentStorageKey,
                assignment.AttachmentFileName ?? "odev",
                assignment.AttachmentContentType ?? "application/octet-stream"));
    }
}

/// <summary>
/// Öğrenci ödev aksiyonlarının sahiplik yetkilendiricisi. Tamamlama/teslim yalnızca ödevin öğrencisi
/// (veya admin) tarafından; ödev görüntüleme (dosya indirme dahil) ödevin öğrencisi veya öğretmeni tarafından.
/// </summary>
public sealed class AssignmentStudentActionAuthorizer :
    ICommandAuthorizer<MarkAssignmentCompletedCommand>,
    ICommandAuthorizer<SubmitAssignmentWorkCommand>,
    IQueryAuthorizer<GetAssignmentQuery>,
    IQueryAuthorizer<GetAssignmentAttachmentQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    private readonly IAssignmentRepository _repository;

    public AssignmentStudentActionAuthorizer(ICurrentUser currentUser, IAssignmentRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(MarkAssignmentCompletedCommand command, CancellationToken cancellationToken) =>
        AuthorizeAsync(command.AssignmentId, allowTeacher: false, cancellationToken);

    public Task<Result> Authorize(SubmitAssignmentWorkCommand command, CancellationToken cancellationToken) =>
        AuthorizeAsync(command.AssignmentId, allowTeacher: false, cancellationToken);

    public Task<Result> Authorize(GetAssignmentQuery query, CancellationToken cancellationToken) =>
        AuthorizeAsync(query.AssignmentId, allowTeacher: true, cancellationToken);

    // Ek indirme: ödevin öğrencisi veya öğretmeni (ya da Admin).
    public Task<Result> Authorize(GetAssignmentAttachmentQuery query, CancellationToken cancellationToken) =>
        AuthorizeAsync(query.AssignmentId, allowTeacher: true, cancellationToken);

    private async Task<Result> AuthorizeAsync(Guid assignmentId, bool allowTeacher, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var currentUserId))
        {
            return Result.Failure(Forbidden);
        }

        var assignment = await _repository.GetAssignmentByIdAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure(AssignmentErrors.NotFound);
        }

        if (assignment.StudentId == currentUserId)
        {
            return Result.Success();
        }

        if (allowTeacher && assignment.TeacherUserId == currentUserId)
        {
            return Result.Success();
        }

        return Result.Failure(Forbidden);
    }
}
