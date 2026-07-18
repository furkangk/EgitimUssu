using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Application;

// ---- Öğrenci ödev aksiyonları (tamamlama + teslim) ----

public sealed record GetAssignmentQuery(Guid AssignmentId) : IQuery<Result<AssignmentResponse>>;

public sealed record MarkAssignmentCompletedCommand(Guid AssignmentId) : ICommand<Result<AssignmentResponse>>;

public sealed record SubmitAssignmentWorkCommand(Guid AssignmentId, string AttachmentUrl)
    : ICommand<Result<AssignmentResponse>>;

internal static class AssignmentErrors
{
    public static readonly Error NotFound = new("assignments.assignment_not_found", "Ödev bulunamadı.");
    public static readonly Error InvalidRequest = new("assignments.invalid_request", "Ödev teslim bilgisi eksik ya da hatalı.");
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

public sealed class SubmitAssignmentWorkCommandHandler
    : ICommandHandler<SubmitAssignmentWorkCommand, Result<AssignmentResponse>>
{
    private readonly IAssignmentRepository _repository;
    private readonly IClock _clock;

    public SubmitAssignmentWorkCommandHandler(IAssignmentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<AssignmentResponse>> Handle(SubmitAssignmentWorkCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.AttachmentUrl))
        {
            return Result<AssignmentResponse>.Failure(AssignmentErrors.InvalidRequest);
        }

        var assignment = await _repository.GetAssignmentByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result<AssignmentResponse>.Failure(AssignmentErrors.NotFound);
        }

        assignment.SubmitWork(command.AttachmentUrl.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<AssignmentResponse>.Success(assignment.ToResponse());
    }
}

/// <summary>
/// Öğrenci ödev aksiyonlarının sahiplik yetkilendiricisi. Tamamlama/teslim yalnızca ödevin öğrencisi
/// (veya admin) tarafından; ödev görüntüleme (dosya indirme dahil) ödevin öğrencisi veya öğretmeni tarafından.
/// </summary>
public sealed class AssignmentStudentActionAuthorizer :
    ICommandAuthorizer<MarkAssignmentCompletedCommand>,
    ICommandAuthorizer<SubmitAssignmentWorkCommand>,
    IQueryAuthorizer<GetAssignmentQuery>
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
