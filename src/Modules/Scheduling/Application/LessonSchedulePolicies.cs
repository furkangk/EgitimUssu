using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed class CreateLessonScheduleCommandValidator : ICommandValidator<CreateLessonScheduleCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Ders planı bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.TeacherUserId != Guid.Empty
            && command.StudentId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Subject)
            && !string.IsNullOrWhiteSpace(command.TimeZone)
            && command.ReminderOffsetMinutes >= 0;

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class LessonScheduleCommandAuthorizer :
    ICommandAuthorizer<CreateLessonScheduleCommand>,
    ICommandAuthorizer<CancelLessonScheduleCommand>,
    ICommandAuthorizer<CompleteLessonScheduleCommand>,
    IQueryAuthorizer<GetLessonScheduleByIdQuery>,
    IQueryAuthorizer<ListLessonSchedulesForTeacherQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders planı bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly ILessonScheduleRepository _repository;

    public LessonScheduleCommandAuthorizer(ICurrentUser currentUser, ILessonScheduleRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(CancelLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        return lesson is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(lesson.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(CompleteLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        return lesson is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(lesson.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(GetLessonScheduleByIdQuery query, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(query.LessonId, cancellationToken);
        return lesson is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(lesson.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(ListLessonSchedulesForTeacherQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
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
