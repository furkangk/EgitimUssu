using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Application;

// ---- Öğretmen ödev aksiyonları (onay + geri gönder) ----

public sealed record ApproveAssignmentCommand(Guid AssignmentId, string? Feedback) : ICommand<Result<AssignmentResponse>>;

public sealed record ReturnAssignmentCommand(Guid AssignmentId, string Feedback) : ICommand<Result<AssignmentResponse>>;

public sealed class ApproveAssignmentCommandHandler : ICommandHandler<ApproveAssignmentCommand, Result<AssignmentResponse>>
{
    private static readonly Error NotFound = new("assignments.assignment_not_found", "Odev bulunamadi.");
    private readonly IAssignmentRepository _repository;
    private readonly IClock _clock;

    public ApproveAssignmentCommandHandler(IAssignmentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<AssignmentResponse>> Handle(ApproveAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetAssignmentByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result<AssignmentResponse>.Failure(NotFound);
        }

        assignment.Approve(command.Feedback?.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<AssignmentResponse>.Success(assignment.ToResponse());
    }
}

public sealed class ReturnAssignmentCommandHandler : ICommandHandler<ReturnAssignmentCommand, Result<AssignmentResponse>>
{
    private static readonly Error NotFound = new("assignments.assignment_not_found", "Odev bulunamadi.");
    private static readonly Error FeedbackRequired = new("assignments.feedback_required", "Geri gonderme icin aciklama zorunlu.");
    private readonly IAssignmentRepository _repository;
    private readonly IClock _clock;

    public ReturnAssignmentCommandHandler(IAssignmentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<AssignmentResponse>> Handle(ReturnAssignmentCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Feedback))
        {
            return Result<AssignmentResponse>.Failure(FeedbackRequired);
        }

        var assignment = await _repository.GetAssignmentByIdAsync(command.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result<AssignmentResponse>.Failure(NotFound);
        }

        assignment.ReturnForRevision(command.Feedback.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<AssignmentResponse>.Success(assignment.ToResponse());
    }
}

/// <summary>
/// Öğretmen ödev aksiyonlarının (onay/geri gönder) sahiplik yetkilendiricisi.
/// Yalnızca ödevin öğretmeni (veya admin) işlem yapabilir.
/// </summary>
public sealed class AssignmentTeacherAuthorizer :
    ICommandAuthorizer<ApproveAssignmentCommand>,
    ICommandAuthorizer<ReturnAssignmentCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu odev uzerinde islem yapma yetkiniz yok.");
    private static readonly Error NotFound = new("assignments.assignment_not_found", "Odev bulunamadi.");
    private readonly ICurrentUser _currentUser;
    private readonly IAssignmentRepository _repository;

    public AssignmentTeacherAuthorizer(ICurrentUser currentUser, IAssignmentRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(ApproveAssignmentCommand command, CancellationToken cancellationToken)
        => AuthorizeByAssignment(command.AssignmentId, cancellationToken);

    public Task<Result> Authorize(ReturnAssignmentCommand command, CancellationToken cancellationToken)
        => AuthorizeByAssignment(command.AssignmentId, cancellationToken);

    private async Task<Result> AuthorizeByAssignment(Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetAssignmentByIdAsync(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return Result.Failure(NotFound);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        return _currentUser.IsAuthenticated
            && Guid.TryParse(_currentUser.UserId, out var userId)
            && userId == assignment.TeacherUserId
            ? Result.Success()
            : Result.Failure(Forbidden);
    }
}
