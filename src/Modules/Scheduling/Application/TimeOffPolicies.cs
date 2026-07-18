using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed class CreateTimeOffBlockCommandValidator : ICommandValidator<CreateTimeOffBlockCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Tatil bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.TeacherUserId != Guid.Empty && !string.IsNullOrWhiteSpace(command.Title);
        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class TimeOffBlockAuthorizer :
    ICommandAuthorizer<CreateTimeOffBlockCommand>,
    ICommandAuthorizer<DeleteTimeOffBlockCommand>,
    IQueryAuthorizer<ListTimeOffBlocksForTeacherQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("scheduling.timeoff_not_found", "Tatil bloğu bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly ITimeOffBlockRepository _repository;

    public TimeOffBlockAuthorizer(ICurrentUser currentUser, ITimeOffBlockRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateTimeOffBlockCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CanManageTeacher(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    public async Task<Result> Authorize(DeleteTimeOffBlockCommand command, CancellationToken cancellationToken)
    {
        var block = await _repository.GetByIdAsync(command.TimeOffId, cancellationToken);
        return block is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(block.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(ListTimeOffBlocksForTeacherQuery query, CancellationToken cancellationToken)
        => Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    private bool CanManageTeacher(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated) return false;
        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        return isAdmin || (isTeacher && Guid.TryParse(_currentUser.UserId, out var id) && id == teacherUserId);
    }
}
