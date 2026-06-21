using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Application;

public sealed class ListTeacherLessonRemindersQueryValidator : IQueryValidator<ListTeacherLessonRemindersQuery>
{
    private static readonly Error InvalidRequest = new("notifications.invalid_request", "Bildirim sorgu bilgileri eksik veya hatali.");

    public Task<Result> Validate(ListTeacherLessonRemindersQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(query.TeacherUserId != Guid.Empty ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class LessonReminderQueryAuthorizer : IQueryAuthorizer<ListTeacherLessonRemindersQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu kaynaga erisim yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public LessonReminderQueryAuthorizer(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result> Authorize(ListTeacherLessonRemindersQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        var ownsList = Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == query.TeacherUserId;
        return Task.FromResult(isAdmin || (isTeacher && ownsList) ? Result.Success() : Result.Failure(Forbidden));
    }
}
