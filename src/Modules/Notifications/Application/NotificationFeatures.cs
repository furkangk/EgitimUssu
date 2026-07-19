using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Notifications.Application;

public sealed record ListTeacherLessonRemindersQuery(Guid TeacherUserId, bool ActiveOnly) : IQuery<Result<IReadOnlyCollection<LessonReminderResponse>>>;

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
    DateTime UpdatedOnUtc);

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
            reminder.UpdatedOnUtc);
    }
}
