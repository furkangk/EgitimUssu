using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Application;

public sealed class CreateLessonSessionFollowUpCommandValidator : ICommandValidator<CreateLessonSessionFollowUpCommand>
{
    private static readonly Error InvalidRequest = new("assignments.invalid_request", "Not veya odev bilgileri eksik ya da hatali.");

    public Task<Result> Validate(CreateLessonSessionFollowUpCommand command, CancellationToken cancellationToken)
    {
        var hasValidAssignments = command.Assignments.All(assignment => !string.IsNullOrWhiteSpace(assignment.Title));
        var isValid = command.LessonSessionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Summary)
            && command.Assignments is not null
            && hasValidAssignments;

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class GetLessonSessionFollowUpQueryValidator : IQueryValidator<GetLessonSessionFollowUpQuery>
{
    private static readonly Error InvalidRequest = new("assignments.invalid_request", "Ders oturumu bilgisi eksik ya da hatali.");

    public Task<Result> Validate(GetLessonSessionFollowUpQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(query.LessonSessionId != Guid.Empty ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class ListAssignmentsQueryValidator : IQueryValidator<ListAssignmentsQuery>
{
    private static readonly Error InvalidRequest = new("assignments.invalid_request", "En az bir filtre gerekli.");

    public Task<Result> Validate(ListAssignmentsQuery query, CancellationToken cancellationToken)
    {
        var ok = query.TeacherUserId.HasValue || query.StudentId.HasValue || query.LessonSessionId.HasValue;
        return Task.FromResult(ok ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class AssignmentFollowUpAuthorizer :
    ICommandAuthorizer<CreateLessonSessionFollowUpCommand>,
    IQueryAuthorizer<GetLessonSessionFollowUpQuery>,
    IQueryAuthorizer<ListAssignmentsQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu islemi yapma yetkiniz yok.");
    private static readonly Error LessonSessionNotFound = new("assignments.lesson_session_not_found", "Ders oturumu bulunamadi.");
    private static readonly Error FollowUpNotFound = new("assignments.follow_up_not_found", "Ders oturumu icin not veya odev bulunamadi.");
    private readonly ICurrentUser _currentUser;
    private readonly ILessonSessionAccessService _lessonSessionAccessService;
    private readonly IAssignmentRepository _repository;

    public AssignmentFollowUpAuthorizer(
        ICurrentUser currentUser,
        ILessonSessionAccessService lessonSessionAccessService,
        IAssignmentRepository repository)
    {
        _currentUser = currentUser;
        _lessonSessionAccessService = lessonSessionAccessService;
        _repository = repository;
    }

    public async Task<Result> Authorize(CreateLessonSessionFollowUpCommand command, CancellationToken cancellationToken)
    {
        var lessonSession = await _lessonSessionAccessService.GetByIdAsync(command.LessonSessionId, cancellationToken);
        if (lessonSession is null)
        {
            return Result.Failure(LessonSessionNotFound);
        }

        return CanManageTeacher(lessonSession.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden);
    }

    public async Task<Result> Authorize(GetLessonSessionFollowUpQuery query, CancellationToken cancellationToken)
    {
        var note = await _repository.GetLessonNoteByLessonSessionIdAsync(query.LessonSessionId, cancellationToken);
        if (note is not null)
        {
            return CanManageTeacher(note.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden);
        }

        var lessonSession = await _lessonSessionAccessService.GetByIdAsync(query.LessonSessionId, cancellationToken);
        if (lessonSession is null)
        {
            return Result.Failure(FollowUpNotFound);
        }

        return CanManageTeacher(lessonSession.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden);
    }

    public Task<Result> Authorize(ListAssignmentsQuery query, CancellationToken cancellationToken)
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
