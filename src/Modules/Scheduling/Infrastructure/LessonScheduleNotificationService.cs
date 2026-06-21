using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class LessonScheduleNotificationService : ILessonScheduleNotificationService
{
    private readonly ILessonReminderRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public LessonScheduleNotificationService(
        ILessonReminderRepository repository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task ScheduleReminderAsync(LessonSchedule lesson, CancellationToken cancellationToken)
    {
        var existingReminder = await _repository.GetByLessonScheduleIdAsync(lesson.Id, cancellationToken);
        if (existingReminder is not null)
        {
            return;
        }

        var reminder = new LessonReminder(
            _idGenerator.New(),
            lesson.Id,
            lesson.TeacherUserId,
            lesson.StudentId,
            "Yaklasan ders hatirlatmasi",
            $"Ders {lesson.StartAtUtc:O} tarihinde baslayacak.",
            lesson.StartAtUtc,
            lesson.StartAtUtc.AddMinutes(-Math.Max(lesson.ReminderOffsetMinutes, 0)),
            NotificationChannel.InApp,
            ReminderStatus.Pending,
            _clock.UtcNow);

        await _repository.AddAsync(reminder, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelReminderAsync(LessonSchedule lesson, CancellationToken cancellationToken)
    {
        var reminder = await _repository.GetByLessonScheduleIdAsync(lesson.Id, cancellationToken);
        if (reminder is null)
        {
            return;
        }

        reminder.Cancel(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
