using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.LessonSessions.Application;

public sealed class CreateLessonSessionCommandValidator : ICommandValidator<CreateLessonSessionCommand>
{
    private static readonly Error InvalidRequest = new("lesson_sessions.invalid_request", "Ders oturumu bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.TeacherUserId != Guid.Empty
            && command.StudentId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Subject)
            && !string.IsNullOrWhiteSpace(command.TopicTitle);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class CompleteLessonSessionCommandValidator : ICommandValidator<CompleteLessonSessionCommand>
{
    private static readonly Error InvalidRequest = new("lesson_sessions.invalid_request", "Ders oturumu tamamlama bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CompleteLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.LessonSessionId != Guid.Empty
            && command.ActualEndAtUtc > command.ActualStartAtUtc
            && !string.IsNullOrWhiteSpace(command.TopicTitle);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class LessonSessionCommandAuthorizer :
    ICommandAuthorizer<CreateLessonSessionCommand>,
    ICommandAuthorizer<CompleteLessonSessionCommand>,
    IQueryAuthorizer<GetLessonSessionByIdQuery>,
    IQueryAuthorizer<ListLessonSessionsQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("lesson_sessions.not_found", "Ders oturumu bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly ILessonSessionRepository _repository;

    public LessonSessionCommandAuthorizer(ICurrentUser currentUser, ILessonSessionRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateLessonSessionCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(CompleteLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.LessonSessionId, cancellationToken);
        return session is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(session.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(GetLessonSessionByIdQuery query, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(query.LessonSessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure(NotFound);
        }

        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var currentUserId = Guid.TryParse(_currentUser.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var canRead = isAdmin || currentUserId == session.TeacherUserId || currentUserId == session.StudentId;
        return canRead ? Result.Success() : Result.Failure(Forbidden);
    }

    public Task<Result> Authorize(ListLessonSessionsQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        if (isAdmin)
        {
            return Task.FromResult(Result.Success());
        }

        if (!Guid.TryParse(_currentUser.UserId, out var currentUserId))
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var isTeacher = _currentUser.Roles.Contains("Teacher");
        if (isTeacher && query.TeacherUserId.HasValue && query.TeacherUserId.Value != currentUserId)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        if (!isTeacher && query.StudentId.HasValue && query.StudentId.Value != currentUserId)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        return Task.FromResult(Result.Success());
    }

    private bool CanManageTeacher(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        return isAdmin || (isTeacher && Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == teacherUserId);
    }
}
