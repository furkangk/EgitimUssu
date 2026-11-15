using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Application;

public sealed record ListTeacherLessonRemindersQuery(Guid TeacherUserId, bool ActiveOnly) : IQuery<Result<IReadOnlyCollection<LessonReminderResponse>>>;

public sealed record ListParentNotificationsQuery(Guid ParentUserId) : IQuery<Result<IReadOnlyCollection<ParentNotificationResponse>>>;

public sealed record LessonReminderResponse(
    Guid Id,
    Guid LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    string Title,
    string Message,
    DateTime ScheduledLessonStartAtUtc,
    DateTime RemindAtUtc,
    string Channel,
    string Status,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc,
    DateTime OccurrenceStartUtc,
    Guid RecipientUserId);

public sealed record ParentNotificationResponse(
    Guid Id,
    Guid ParentUserId,
    Guid StudentId,
    string Type,
    string Title,
    string Message,
    DateTime CreatedOnUtc);

public interface IParentNotificationRepository
{
    Task AddAsync(ParentNotification notification, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ParentNotification>> ListByParentAsync(Guid parentUserId, CancellationToken cancellationToken);

    Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> GetProcessedAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken);

    void MarkProcessed(Guid eventId, string eventName, DateTime nowUtc);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ILessonReminderRepository
{
    Task<LessonReminder?> GetByLessonScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonReminder>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonReminder>> ListDuePendingAsync(DateTime utcNow, CancellationToken cancellationToken);

    Task AddAsync(LessonReminder reminder, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ListTeacherLessonRemindersQueryHandler : IQueryHandler<ListTeacherLessonRemindersQuery, Result<IReadOnlyCollection<LessonReminderResponse>>>
{
    private readonly ILessonReminderRepository _repository;

    public ListTeacherLessonRemindersQueryHandler(ILessonReminderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonReminderResponse>>> Handle(ListTeacherLessonRemindersQuery query, CancellationToken cancellationToken)
    {
        var reminders = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);
        var payload = reminders
            .Where(reminder => !query.ActiveOnly || reminder.Status == ReminderStatus.Pending)
            .OrderBy(reminder => reminder.RemindAtUtc)
            .ThenBy(reminder => reminder.CreatedOnUtc)
            .Select(reminder => reminder.ToResponse())
            .ToArray();

        return Result<IReadOnlyCollection<LessonReminderResponse>>.Success(payload);
    }
}

public sealed class ListParentNotificationsQueryHandler : IQueryHandler<ListParentNotificationsQuery, Result<IReadOnlyCollection<ParentNotificationResponse>>>
{
    private readonly IParentNotificationRepository _repository;

    public ListParentNotificationsQueryHandler(IParentNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<ParentNotificationResponse>>> Handle(ListParentNotificationsQuery query, CancellationToken cancellationToken)
    {
        var items = await _repository.ListByParentAsync(query.ParentUserId, cancellationToken);
        var payload = items
            .Select(n => new ParentNotificationResponse(n.Id, n.ParentUserId, n.StudentId, n.Type.ToString(), n.Title, n.Message, n.CreatedOnUtc))
            .ToArray();
        return Result<IReadOnlyCollection<ParentNotificationResponse>>.Success(payload);
    }
}

internal static class LessonReminderMappings
{
    public static LessonReminderResponse ToResponse(this LessonReminder reminder)
    {
        return new LessonReminderResponse(
            reminder.Id,
            reminder.LessonScheduleId,
            reminder.TeacherUserId,
            reminder.StudentId,
            reminder.Title,
            reminder.Message,
            reminder.ScheduledLessonStartAtUtc,
            reminder.RemindAtUtc,
            reminder.Channel.ToString(),
            reminder.Status.ToString(),
            reminder.CreatedOnUtc,
            reminder.UpdatedOnUtc,
            reminder.OccurrenceStartUtc,
            reminder.RecipientUserId);
    }
}

// M11-2: Cihaz push token'i. Kayit idempotenttir — ayni token yeniden gonderilirse
// yeni satir acilmaz, mevcut satir tazelenir (FCM token'lari rotasyona ugrar).
public sealed record RegisterDeviceTokenCommand(Guid UserId, string Token, DevicePlatform Platform, string? DeviceName)
    : ICommand<Result>;

public sealed record UnregisterDeviceTokenCommand(Guid UserId, string Token) : ICommand<Result>;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceToken>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceToken>> ListActiveByUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task AddAsync(DeviceToken deviceToken, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class RegisterDeviceTokenCommandHandler : ICommandHandler<RegisterDeviceTokenCommand, Result>
{
    private static readonly Error InvalidToken = new("notifications.invalid_device_token", "Cihaz token'i gecersiz.");

    private readonly IDeviceTokenRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public RegisterDeviceTokenCommandHandler(IDeviceTokenRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result> Handle(RegisterDeviceTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(InvalidToken);
        }

        var existing = await _repository.GetByTokenAsync(command.Token, cancellationToken);
        if (existing is null)
        {
            await _repository.AddAsync(
                new DeviceToken(
                    _idGenerator.New(),
                    command.UserId,
                    command.Token,
                    command.Platform,
                    command.DeviceName,
                    _clock.UtcNow),
                cancellationToken);
        }
        else
        {
            existing.Touch(_clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class UnregisterDeviceTokenCommandHandler : ICommandHandler<UnregisterDeviceTokenCommand, Result>
{
    private readonly IDeviceTokenRepository _repository;
    private readonly IClock _clock;

    public UnregisterDeviceTokenCommandHandler(IDeviceTokenRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(UnregisterDeviceTokenCommand command, CancellationToken cancellationToken)
    {
        // Idempotent: bilinmeyen token da basarili sayilir (cikis akisi hata gostermemeli).
        var existing = await _repository.GetByTokenAsync(command.Token, cancellationToken);
        if (existing is null)
        {
            return Result.Success();
        }

        existing.Deactivate(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// M11-3/M11-4: Rolden bagimsiz in-app bildirim merkezi (liste + rozet + okundu).
public sealed record ListUserNotificationsQuery(Guid UserId, bool OnlyUnread, int Skip, int Take, string? After = null, int? Limit = null)
    : IQuery<Result<PagedResult<UserNotificationResponse>>>;

public sealed record GetUnreadNotificationCountQuery(Guid UserId) : IQuery<Result<int>>;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : ICommand<Result>;

public sealed record UserNotificationResponse(
    Guid Id,
    Guid UserId,
    string Kind,
    string Title,
    string Body,
    string? Route,
    bool IsRead,
    DateTime CreatedOnUtc,
    DateTime? ReadOnUtc);

public interface IUserNotificationRepository
{
    Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> ListAsync(
        Guid userId,
        bool onlyUnread,
        int skip,
        int take,
        CancellationToken cancellationToken, EgitimUssu.Shared.Kernel.KeysetPage? page = null);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserNotification?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Zamanı gelmiş veya süresi dolmuş Pending bildirimler; dispatcher UTC anını verir.</summary>
    Task<IReadOnlyList<UserNotification>> ListPendingPushAsync(int maxCount, CancellationToken cancellationToken, DateTime? nowUtc = null);

    Task AddAsync(UserNotification notification, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ListUserNotificationsQueryHandler
    : IQueryHandler<ListUserNotificationsQuery, Result<PagedResult<UserNotificationResponse>>>
{
    /// <summary>Istemcinin asiri sayfa istemesini engeller (varsayilan-guvenli ust sinir).</summary>
    public const int MaxTake = 100;

    private readonly IUserNotificationRepository _repository;

    public ListUserNotificationsQueryHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PagedResult<UserNotificationResponse>>> Handle(
        ListUserNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.After is not null || query.Limit.HasValue)
        {
            if (!KeysetPage.TryCreate(query.After, query.Limit, out var page))
                return Result<PagedResult<UserNotificationResponse>>.Failure(KeysetPage.Invalid);
            var (rows, _) = await _repository.ListAsync(query.UserId, query.OnlyUnread, 0, page.Limit, cancellationToken, page);
            return Result<PagedResult<UserNotificationResponse>>.Success(
                page.Finish(rows.Select(ToResponse).ToArray(), x => x.CreatedOnUtc, x => x.Id));
        }

        var skip = Math.Max(query.Skip, 0);
        var take = Math.Clamp(query.Take <= 0 ? 20 : query.Take, 1, MaxTake);

        var (items, totalCount) = await _repository.ListAsync(query.UserId, query.OnlyUnread, skip, take, cancellationToken);
        var payload = items.Select(ToResponse).ToArray();

        // PagedResult sayfa numarasi bekliyor; skip/take'ten 1-tabanli sayfa turetilir.
        return Result<PagedResult<UserNotificationResponse>>.Success(
            new PagedResult<UserNotificationResponse>(payload, (skip / take) + 1, take, totalCount));
    }

    private static UserNotificationResponse ToResponse(UserNotification notification)
        => new(
            notification.Id,
            notification.UserId,
            notification.Kind.ToString(),
            notification.Title,
            notification.Body,
            notification.Route,
            notification.IsRead,
            notification.CreatedOnUtc,
            notification.ReadOnUtc);
}

public sealed class GetUnreadNotificationCountQueryHandler : IQueryHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IUserNotificationRepository _repository;

    public GetUnreadNotificationCountQueryHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<int>> Handle(GetUnreadNotificationCountQuery query, CancellationToken cancellationToken)
        => Result<int>.Success(await _repository.CountUnreadAsync(query.UserId, cancellationToken));
}

public sealed class MarkNotificationReadCommandHandler : ICommandHandler<MarkNotificationReadCommand, Result>
{
    private static readonly Error NotFound = new("notifications.notification_not_found", "Bildirim bulunamadi.");

    private readonly IUserNotificationRepository _repository;
    private readonly IClock _clock;

    public MarkNotificationReadCommandHandler(IUserNotificationRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        var notification = await _repository.GetByIdAsync(command.NotificationId, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(NotFound);
        }

        notification.MarkRead(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
