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
    string? MeetingUrl,
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
    string? MeetingUrl,
    string? Notes) : ICommand<Result<LessonScheduleResponse>>;

public enum OccurrenceScope
{
    Single = 1,
    ThisAndFuture = 2,
    All = 3
}

public sealed record CancelLessonScheduleCommand(
    Guid LessonId,
    CancellationReason Reason,
    bool IsChargeable,
    string? CancellationNote,
    OccurrenceScope Scope = OccurrenceScope.All,
    DateTime? OccurrenceStartAtUtc = null) : ICommand<Result<LessonScheduleResponse>>;

public sealed record RescheduleLessonScheduleCommand(
    Guid LessonId,
    DateTime NewStartAtUtc,
    DateTime NewEndAtUtc,
    string? Note,
    OccurrenceScope Scope = OccurrenceScope.All,
    DateTime? OccurrenceStartAtUtc = null) : ICommand<Result<LessonScheduleResponse>>;

public sealed record CompleteLessonScheduleCommand(Guid LessonId) : ICommand<Result<LessonScheduleResponse>>;

public sealed record DeleteLessonScheduleCommand(Guid LessonId) : ICommand<Result>;

public sealed record GetLessonScheduleByIdQuery(Guid LessonId) : IQuery<Result<LessonScheduleResponse>>;

public sealed record ListLessonSchedulesForTeacherQuery(
    Guid TeacherUserId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<LessonScheduleResponse>>>;

public sealed record ListLessonSchedulesForStudentQuery(
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<LessonScheduleResponse>>>;

public sealed record LessonScheduleResponse(
    Guid Id,
    Guid? TeacherUserId,
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
    string? MeetingUrl,
    string? Notes,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc,
    DateTime? OriginalStartAtUtc,
    string? CancellationReason,
    bool IsChargeable,
    DateTime OccurrenceStartAtUtc);

public interface ILessonScheduleRepository
{
    Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken);

    Task<bool> HasTeacherConflictAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, Guid? excludeLessonId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonSchedule>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Öğrencinin iptal edilmemiş dersleri; başlangıcı <paramref name="untilUtc"/>'ye kadar olanlar.
    /// Tekrar kuralları uygulama katmanında genişletildiği için alt sınır uygulanmaz.
    /// </summary>
    Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Öğretmenin iptal edilmemiş dersleri; başlangıcı <paramref name="untilUtc"/>'ye kadar olanlar.
    /// (M04-1) Tekrar kuralları uygulama katmanında genişletildiği için alt sınır uygulanmaz —
    /// aylar önce başlamış haftalık bir seri de bu listeye girer.
    /// </summary>
    Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken);

    Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken);

    void Remove(LessonSchedule lessonSchedule);

    Task AddExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateLessonScheduleCommandHandler : ICommandHandler<CreateLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error Conflict = new("scheduling.teacher_conflict", "Ogretmenin bu zaman araliginda baska bir dersi var.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
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
            command.MeetingUrl?.Trim(),
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.AddAsync(lesson, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        // Y1: Hatirlatma, LessonScheduledDomainEvent -> outbox -> Notifications handler yoluyla olusturulur;
        // Scheduling artik Notifications'a senkron/dogrudan yazmaz (modul izolasyonu + atomiklik).

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
    private readonly IClock _clock;

    public UpdateLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        IClock clock)
    {
        _repository = repository;
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
            lesson.TeacherUserId ?? Guid.Empty,
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
            command.MeetingUrl?.Trim(),
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class CancelLessonScheduleCommandHandler : ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Yalnizca planli veya taslak ders iptal edilebilir.");
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Tekrar kapsami ve ders tarihi eksik veya hatali.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    public CancelLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        IClock clock,
        IIdGenerator idGenerator)
    {
        _repository = repository;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(CancelLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        if (!lesson.IsEditable)
        {
            return Result<LessonScheduleResponse>.Failure(NotEditable);
        }

        var isRecurring = !string.IsNullOrWhiteSpace(lesson.RecurrenceRule);
        if (!Enum.IsDefined(command.Scope)
            || (isRecurring && command.Scope != OccurrenceScope.All && command.OccurrenceStartAtUtc is null))
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRequest);
        }
        if (isRecurring && command.Scope == OccurrenceScope.Single && command.OccurrenceStartAtUtc is { } occStart)
        {
            var occurrenceException = new LessonOccurrenceException(
                _idGenerator.New(), lesson.Id, occStart,
                OccurrenceExceptionAction.Cancelled, null, null, command.CancellationNote?.Trim(), _clock.UtcNow);
            await _repository.AddExceptionAsync(occurrenceException, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }

        if (isRecurring && command.Scope == OccurrenceScope.ThisAndFuture && command.OccurrenceStartAtUtc is { } cutoff)
        {
            lesson.EndSeriesBefore(cutoff, _clock.UtcNow);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }

        lesson.Cancel(command.Reason, command.IsChargeable, command.CancellationNote?.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        // Y1: Hatirlatma iptali LessonScheduleCancelledDomainEvent -> outbox -> Notifications handler yoluyla yapilir.
        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class RescheduleLessonScheduleCommandHandler : ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error Conflict = new("scheduling.teacher_conflict", "Ogretmenin bu zaman araliginda baska bir dersi var.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Yalnizca planli ders ertelenebilir.");
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Tekrar kapsami ve ders tarihi eksik veya hatali.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    public RescheduleLessonScheduleCommandHandler(ILessonScheduleRepository repository, IClock clock, IIdGenerator idGenerator)
    {
        _repository = repository;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(RescheduleLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.NewEndAtUtc <= command.NewStartAtUtc)
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

        if (!Enum.IsDefined(command.Scope)
            || (!string.IsNullOrWhiteSpace(lesson.RecurrenceRule)
                && command.Scope != OccurrenceScope.All && command.OccurrenceStartAtUtc is null))
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRequest);
        }

        var hasConflict = await _repository.HasTeacherConflictAsync(
            lesson.TeacherUserId ?? Guid.Empty, command.NewStartAtUtc, command.NewEndAtUtc, lesson.Id, cancellationToken);
        if (hasConflict)
        {
            return Result<LessonScheduleResponse>.Failure(Conflict);
        }

        if (!string.IsNullOrWhiteSpace(lesson.RecurrenceRule)
            && command.Scope == OccurrenceScope.Single
            && command.OccurrenceStartAtUtc is { } occStart)
        {
            var occurrenceException = new LessonOccurrenceException(
                _idGenerator.New(), lesson.Id, occStart,
                OccurrenceExceptionAction.Rescheduled, command.NewStartAtUtc, command.NewEndAtUtc, command.Note?.Trim(), _clock.UtcNow);
            await _repository.AddExceptionAsync(occurrenceException, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }

        lesson.Reschedule(command.NewStartAtUtc, command.NewEndAtUtc, command.Note?.Trim(), _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class CompleteLessonScheduleCommandHandler : ICommandHandler<CompleteLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error AlreadyCompleted = new("scheduling.already_completed", "Ders zaten tamamlanmis.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Iptal edilmis ders tamamlanamaz.");
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

        if (!lesson.IsEditable)
        {
            return Result<LessonScheduleResponse>.Failure(NotEditable);
        }

        lesson.Complete(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class DeleteLessonScheduleCommandHandler : ICommandHandler<DeleteLessonScheduleCommand, Result>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error NotAllowed = new("scheduling.delete_not_allowed", "Ders silinemez; iptal edin. Silme yalnizca olusturmadan sonraki 24 saat icinde ve ders gelecekteyse mumkundur.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;

    public DeleteLessonScheduleCommandHandler(ILessonScheduleRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(DeleteLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result.Failure(NotFound);
        }

        if (!lesson.CanBeDeletedAt(_clock.UtcNow))
        {
            return Result.Failure(NotAllowed);
        }

        _repository.Remove(lesson);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
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
    private readonly ILocalCalendar _calendar;

    public ListLessonSchedulesForTeacherQueryHandler(ILessonScheduleRepository repository, ILocalCalendar calendar)
    {
        _repository = repository;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyCollection<LessonScheduleResponse>>> Handle(ListLessonSchedulesForTeacherQuery query, CancellationToken cancellationToken)
    {
        // M04-1: Takvim artık serileri değil somut oluşumları döner. Seri tek satır olarak gelir,
        // occurrence istisnaları (iptal/atlama/erteleme) burada uygulanır.
        var lessons = await _repository.ListActiveForTeacherUntilAsync(query.TeacherUserId, query.EndAtUtc, cancellationToken);
        var exceptions = await _repository.ListExceptionsForTeacherAsync(query.TeacherUserId, cancellationToken);
        var exceptionsByLesson = exceptions
            .GroupBy(x => x.SeriesLessonScheduleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<OccurrenceOverride>)group
                    .Select(x => new OccurrenceOverride(x.OriginalStartAtUtc, x.Action, x.OverrideStartAtUtc, x.OverrideEndAtUtc))
                    .ToArray());

        var payload = new List<LessonScheduleResponse>();
        foreach (var lesson in lessons)
        {
            var lessonExceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var found)
                ? found
                : Array.Empty<OccurrenceOverride>();

            // P16 T2 / N-04: seri kendi diliminin yerel takviminde genişletilir (BYDAY yerel gün).
            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone),
                query.StartAtUtc, query.EndAtUtc, lessonExceptions))
            {
                // İptal edilen tek oluşum takvimde gösterilmez (öğrenci takvimiyle aynı kural).
                if (occurrence.IsCancelled)
                {
                    continue;
                }

                payload.Add(lesson.ToResponse(occurrence));
            }
        }

        return Result<IReadOnlyCollection<LessonScheduleResponse>>.Success(
            payload.OrderBy(item => item.StartAtUtc).ToArray());
    }
}

public sealed class ListLessonSchedulesForStudentQueryHandler : IQueryHandler<ListLessonSchedulesForStudentQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>
{
    private readonly ILessonScheduleRepository _repository;

    public ListLessonSchedulesForStudentQueryHandler(ILessonScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonScheduleResponse>>> Handle(ListLessonSchedulesForStudentQuery query, CancellationToken cancellationToken)
    {
        var lessons = await _repository.ListForStudentAsync(query.StudentId, query.StartAtUtc, query.EndAtUtc, cancellationToken);
        var payload = lessons
            .OrderBy(lesson => lesson.StartAtUtc)
            .Select(lesson => lesson.ToResponse())
            .ToArray();

        return Result<IReadOnlyCollection<LessonScheduleResponse>>.Success(payload);
    }
}

internal static class LessonScheduleMappings
{
    /// <summary>
    /// <paramref name="occurrence"/> verildiğinde yanıt somut oluşumun saatlerini taşır;
    /// <c>OccurrenceStartAtUtc</c> ise istisna uçlarının (tek oluşum iptal/erteleme) hedef anahtarıdır.
    /// </summary>
    public static LessonScheduleResponse ToResponse(this LessonSchedule lesson, ScheduleOccurrence? occurrence = null)
    {
        return new LessonScheduleResponse(
            lesson.Id,
            lesson.TeacherUserId,
            lesson.StudentId,
            lesson.Subject,
            // Ç-06: self-lesson'da LessonFormat null; Nullable<T>.ToString() zaten "" döner — davranış aynı, uyarı susturuluyor.
            lesson.LessonFormat?.ToString() ?? string.Empty,
            occurrence?.StartAtUtc ?? lesson.StartAtUtc,
            occurrence?.EndAtUtc ?? lesson.EndAtUtc,
            lesson.TimeZone,
            lesson.RecurrenceRule,
            lesson.Status.ToString(),
            lesson.ReminderOffsetMinutes,
            lesson.LocationLabel,
            lesson.MeetingUrl,
            lesson.Notes,
            lesson.CreatedOnUtc,
            lesson.UpdatedOnUtc,
            lesson.OriginalStartAtUtc,
            lesson.CancellationReason?.ToString(),
            lesson.IsChargeable,
            occurrence?.RecurrenceIdUtc ?? lesson.StartAtUtc);
    }
}
