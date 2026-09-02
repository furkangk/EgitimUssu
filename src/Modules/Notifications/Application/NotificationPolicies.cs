using EgitimUssu.Modules.Notifications.Domain;
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

public sealed class LessonReminderQueryAuthorizer :
    IQueryAuthorizer<ListTeacherLessonRemindersQuery>,
    IQueryAuthorizer<ListParentNotificationsQuery>
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

    // Veli yalnız kendi bildirimlerini görür (self/Admin).
    public Task<Result> Authorize(ListParentNotificationsQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Task.FromResult(Result.Success());
        }

        var ownsList = Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == query.ParentUserId;
        return Task.FromResult(ownsList ? Result.Success() : Result.Failure(Forbidden));
    }
}

// M11-2: Cihaz token'i yalniz sahibi (veya Admin) kaydeder/siler — varsayilan-deny.
public sealed class DeviceTokenCommandAuthorizer :
    ICommandAuthorizer<RegisterDeviceTokenCommand>,
    ICommandAuthorizer<UnregisterDeviceTokenCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu islemi yapma yetkiniz yok.");

    private readonly ICurrentUser _currentUser;

    public DeviceTokenCommandAuthorizer(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result> Authorize(RegisterDeviceTokenCommand command, CancellationToken cancellationToken)
        => Task.FromResult(AuthorizeOwner(command.UserId));

    public Task<Result> Authorize(UnregisterDeviceTokenCommand command, CancellationToken cancellationToken)
        => Task.FromResult(AuthorizeOwner(command.UserId));

    private Result AuthorizeOwner(Guid userId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        var isSelf = Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == userId;
        return isSelf ? Result.Success() : Result.Failure(Forbidden);
    }
}
