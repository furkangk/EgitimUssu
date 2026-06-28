using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed record CreateLessonScheduleCommand(
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? Notes) : ICommand<Result<LessonScheduleResponse>>;

public sealed record UpdateLessonScheduleCommand(
    Guid LessonId,
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? Notes) : ICommand<Result<LessonScheduleResponse>>;

public sealed record CancelLessonScheduleCommand(
    Guid LessonId,
    string? CancellationNote) : ICommand<Result<LessonScheduleResponse>>;

public sealed record CompleteLessonScheduleCommand(Guid LessonId) : ICommand<Result<LessonScheduleResponse>>;

public sealed record GetLessonScheduleByIdQuery(Guid LessonId) : IQuery<Result<LessonScheduleResponse>>;

public sealed record ListLessonSchedulesForTeacherQuery(
    Guid TeacherUserId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<LessonScheduleResponse>>>;

public sealed record LessonScheduleResponse(
    Guid Id,
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    string LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    string Status,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? Notes,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public interface ILessonScheduleRepository
{
    Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken);

    Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken);

    Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ILessonScheduleNotificationService
{
    Task ScheduleReminderAsync(LessonSchedule lesson, CancellationToken cancellationToken);

    Task CancelReminderAsync(LessonSchedule lesson, CancellationToken cancellationToken);
}

public sealed class CreateLessonScheduleCommandHandler : ICommandHandler<CreateLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error Conflict = new("scheduling.teacher_conflict", "Ogretmenin bu zaman araliginda baska bir dersi var.");
    private readonly ILessonScheduleRepository _repository;
    private readonly ILessonScheduleNotificationService _notificationService;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        ILessonScheduleNotificationService notificationService,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _notificationService = notificationService;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(CreateLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRange);
        }

        var hasConflict = await _repository.HasTeacherConflictAsync(
            command.TeacherUserId,
            command.StartAtUtc,
            command.EndAtUtc,
            null,
            cancellationToken);

        if (hasConflict)
        {
            return Result<LessonScheduleResponse>.Failure(Conflict);
        }

        var lesson = new LessonSchedule(
            _idGenerator.New(),
            command.TeacherUserId,
            command.StudentId,
            command.Subject.Trim(),
            command.LessonFormat,
            command.StartAtUtc,
            command.EndAtUtc,
            command.TimeZone.Trim(),
            command.RecurrenceRule?.Trim(),
            LessonScheduleStatus.Planned,
            command.ReminderOffsetMinutes,
            command.LocationLabel?.Trim(),
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.AddAsync(lesson, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        await _notificationService.ScheduleReminderAsync(lesson, cancellationToken);

        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class UpdateLessonScheduleCommandHandler : ICommandHandler<UpdateLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error Conflict = new("scheduling.teacher_conflict", "Ogretmenin bu zaman araliginda baska bir dersi var.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Yalnizca planli ders duzenlenebilir.");
    private readonly ILessonScheduleRepository _repository;
    private readonly ILessonScheduleNotificationService _notificationService;
    private readonly IClock _clock;

    public UpdateLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        ILessonScheduleNotificationService notificationService,
        IClock clock)
    {
        _repository = repository;
        _notificationService = notificationService;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(UpdateLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRange);
        }

        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        if (!lesson.IsEditable)
        {
            return Result<LessonScheduleResponse>.Failure(NotEditable);
        }

        var hasConflict = await _repository.HasTeacherConflictAsync(
            lesson.TeacherUserId,
            command.StartAtUtc,
            command.EndAtUtc,
            lesson.Id,
            cancellationToken);

        if (hasConflict)
        {
            return Result<LessonScheduleResponse>.Failure(Conflict);
        }

        lesson.UpdateDetails(
            command.Subject.Trim(),
            command.LessonFormat,
            command.StartAtUtc,
            command.EndAtUtc,
            command.TimeZone.Trim(),
            command.RecurrenceRule?.Trim(),
            command.ReminderOffsetMinutes,
            command.LocationLabel?.Trim(),
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        await _notificationService.ScheduleReminderAsync(lesson, cancellationToken);

        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class CancelLessonScheduleCommandHandler : ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private readonly ILessonScheduleRepository _repository;
    private readonly ILessonScheduleNotificationService _notificationService;
    private readonly IClock _clock;

    public CancelLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        ILessonScheduleNotificationService notificationService,
        IClock clock)
    {
        _repository = repository;
        _notificationService = notificationService;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(CancelLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        lesson.Cancel(command.CancellationNote?.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        await _notificationService.CancelReminderAsync(lesson, cancellationToken);
        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class CompleteLessonScheduleCommandHandler : ICommandHandler<CompleteLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error AlreadyCompleted = new("scheduling.already_completed", "Ders zaten tamamlanmis.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;

    public CompleteLessonScheduleCommandHandler(ILessonScheduleRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(CompleteLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        if (lesson.Status == LessonScheduleStatus.Completed)
        {
            return Result<LessonScheduleResponse>.Failure(AlreadyCompleted);
        }

        lesson.Complete(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class GetLessonScheduleByIdQueryHandler : IQueryHandler<GetLessonScheduleByIdQuery, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private readonly ILessonScheduleRepository _repository;

    public GetLessonScheduleByIdQueryHandler(ILessonScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(GetLessonScheduleByIdQuery query, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(query.LessonId, cancellationToken);
        return lesson is null
            ? Result<LessonScheduleResponse>.Failure(NotFound)
            : Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class ListLessonSchedulesForTeacherQueryHandler : IQueryHandler<ListLessonSchedulesForTeacherQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>
{
    private readonly ILessonScheduleRepository _repository;

    public ListLessonSchedulesForTeacherQueryHandler(ILessonScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonScheduleResponse>>> Handle(ListLessonSchedulesForTeacherQuery query, CancellationToken cancellationToken)
    {
        var lessons = await _repository.ListForTeacherAsync(query.TeacherUserId, query.StartAtUtc, query.EndAtUtc, cancellationToken);
        var payload = lessons
            .OrderBy(lesson => lesson.StartAtUtc)
            .Select(lesson => lesson.ToResponse())
            .ToArray();

        return Result<IReadOnlyCollection<LessonScheduleResponse>>.Success(payload);
    }
}

internal static class LessonScheduleMappings
{
    public static LessonScheduleResponse ToResponse(this LessonSchedule lesson)
    {
        return new LessonScheduleResponse(
            lesson.Id,
            lesson.TeacherUserId,
            lesson.StudentId,
            lesson.Subject,
            lesson.LessonFormat.ToString(),
            lesson.StartAtUtc,
            lesson.EndAtUtc,
            lesson.TimeZone,
            lesson.RecurrenceRule,
            lesson.Status.ToString(),
            lesson.ReminderOffsetMinutes,
            lesson.LocationLabel,
            lesson.Notes,
            lesson.CreatedOnUtc,
            lesson.UpdatedOnUtc);
    }
}
