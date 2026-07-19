using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

// Ç-06: Öğrencinin kendi dersi (öğretmensiz) artık ayrı bir StudyScheduleEntry değil; birleşik
// LessonSchedule'da TeacherUserId is null olarak tutulur. Aşağıdaki komutlar /study-entries rotalarının
// (mobil geriye-uyum) bağlandığı self-lesson yoludur.

public sealed record CreateSelfLessonCommand(
    Guid StudentId,
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes) : ICommand<Result<SelfLessonResponse>>;

public sealed record UpdateSelfLessonCommand(
    Guid LessonId,
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes) : ICommand<Result<SelfLessonResponse>>;

public sealed record DeleteSelfLessonCommand(Guid LessonId) : ICommand<Result<SelfLessonResponse>>;

/// <summary>
/// Öğrencinin kendi ders/program girdisinin yanıt şekli. Alan adları eski <c>StudyScheduleEntryResponse</c>
/// ile birebir aynıdır (mobil geriye-uyum). <c>Source</c> her zaman "Self".
/// </summary>
public sealed record SelfLessonResponse(
    Guid Id,
    Guid StudentId,
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes,
    string Status,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

/// <summary>
/// Self ders ile öğretmen dersleri arasındaki saat çakışmasını, tekrar kurallarını genişleterek denetler.
/// (Ç-06 öncesi StudyScheduleConflict; birleşik modele taşındı.)
/// </summary>
internal static class SelfLessonConflict
{
    public const int HorizonDays = 180;

    public static bool OverlapsTeacherLesson(
        DateTime startAtUtc,
        DateTime endAtUtc,
        string? recurrenceRule,
        IReadOnlyCollection<LessonSchedule> teacherLessons,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<OccurrenceOverride>> exceptionsByLesson)
    {
        if (teacherLessons.Count == 0)
        {
            return false;
        }

        var windowStart = startAtUtc;
        var windowEnd = startAtUtc.AddDays(HorizonDays);

        var candidate = RecurrenceExpander
            .Expand(startAtUtc, endAtUtc, recurrenceRule, windowStart, windowEnd)
            .ToArray();
        if (candidate.Length == 0)
        {
            return false;
        }

        foreach (var lesson in teacherLessons)
        {
            var lessonExceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var ex)
                ? ex
                : Array.Empty<OccurrenceOverride>();

            foreach (var lessonOccurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, windowStart, windowEnd, lessonExceptions))
            {
                if (lessonOccurrence.IsCancelled)
                {
                    continue;
                }

                foreach (var own in candidate)
                {
                    if (own.StartAtUtc < lessonOccurrence.EndAtUtc && own.EndAtUtc > lessonOccurrence.StartAtUtc)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<OccurrenceOverride>>> LoadExceptionsByLessonAsync(
        ILessonScheduleRepository lessonRepository,
        IEnumerable<LessonSchedule> lessons,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, IReadOnlyCollection<OccurrenceOverride>>();
        foreach (var lesson in lessons)
        {
            if (string.IsNullOrWhiteSpace(lesson.RecurrenceRule) || map.ContainsKey(lesson.Id))
            {
                continue;
            }

            var exceptions = await lessonRepository.ListExceptionsForSeriesAsync(lesson.Id, cancellationToken);
            map[lesson.Id] = exceptions
                .Select(x => new OccurrenceOverride(x.OriginalStartAtUtc, x.Action, x.OverrideStartAtUtc, x.OverrideEndAtUtc))
                .ToArray();
        }

        return map;
    }

    /// <summary>Öğrencinin kendi ders çakışması için yalnızca öğretmen derslerini süzer (self-self çakışma serbest).</summary>
    public static IReadOnlyCollection<LessonSchedule> TeacherOnly(IReadOnlyCollection<LessonSchedule> lessons)
        => lessons.Where(l => l.TeacherUserId is not null).ToArray();
}

public sealed class CreateSelfLessonCommandHandler : ICommandHandler<CreateSelfLessonCommand, Result<SelfLessonResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error TeacherConflict = new("scheduling.teacher_conflict", "Bu saatte ogretmeninin planladigi bir ders var; kendi dersini bu saate ekleyemezsin.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateSelfLessonCommandHandler(ILessonScheduleRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<SelfLessonResponse>> Handle(CreateSelfLessonCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<SelfLessonResponse>.Failure(InvalidRange);
        }

        var teacherLessons = SelfLessonConflict.TeacherOnly(await _repository.ListActiveForStudentUntilAsync(
            command.StudentId, command.StartAtUtc.AddDays(SelfLessonConflict.HorizonDays), cancellationToken));

        var exceptionsByLesson = await SelfLessonConflict.LoadExceptionsByLessonAsync(_repository, teacherLessons, cancellationToken);

        if (SelfLessonConflict.OverlapsTeacherLesson(command.StartAtUtc, command.EndAtUtc, command.RecurrenceRule?.Trim(), teacherLessons, exceptionsByLesson))
        {
            return Result<SelfLessonResponse>.Failure(TeacherConflict);
        }

        var lesson = LessonSchedule.CreateSelfLesson(
            _idGenerator.New(),
            command.StudentId,
            command.Subject.Trim(),
            Trim(command.Topic),
            command.StartAtUtc,
            command.EndAtUtc,
            command.TimeZone.Trim(),
            Trim(command.RecurrenceRule),
            command.ReminderOffsetMinutes,
            Trim(command.ColorHex),
            Trim(command.Notes),
            _clock.UtcNow);

        await _repository.AddAsync(lesson, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<SelfLessonResponse>.Success(lesson.ToSelfResponse());
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateSelfLessonCommandHandler : ICommandHandler<UpdateSelfLessonCommand, Result<SelfLessonResponse>>
{
    private static readonly Error NotFound = new("scheduling.entry_not_found", "Program girdisi bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error TeacherConflict = new("scheduling.teacher_conflict", "Bu saatte ogretmeninin planladigi bir ders var; kendi dersini bu saate ekleyemezsin.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;

    public UpdateSelfLessonCommandHandler(ILessonScheduleRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<SelfLessonResponse>> Handle(UpdateSelfLessonCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<SelfLessonResponse>.Failure(InvalidRange);
        }

        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null || !lesson.IsSelfPlanned || !lesson.IsEditable)
        {
            return Result<SelfLessonResponse>.Failure(NotFound);
        }

        var teacherLessons = SelfLessonConflict.TeacherOnly(await _repository.ListActiveForStudentUntilAsync(
            lesson.StudentId, command.StartAtUtc.AddDays(SelfLessonConflict.HorizonDays), cancellationToken));

        var exceptionsByLesson = await SelfLessonConflict.LoadExceptionsByLessonAsync(_repository, teacherLessons, cancellationToken);

        if (SelfLessonConflict.OverlapsTeacherLesson(command.StartAtUtc, command.EndAtUtc, command.RecurrenceRule?.Trim(), teacherLessons, exceptionsByLesson))
        {
            return Result<SelfLessonResponse>.Failure(TeacherConflict);
        }

        lesson.UpdateSelfDetails(
            command.Subject.Trim(),
            Trim(command.Topic),
            command.StartAtUtc,
            command.EndAtUtc,
            command.TimeZone.Trim(),
            Trim(command.RecurrenceRule),
            command.ReminderOffsetMinutes,
            Trim(command.ColorHex),
            Trim(command.Notes),
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<SelfLessonResponse>.Success(lesson.ToSelfResponse());
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DeleteSelfLessonCommandHandler : ICommandHandler<DeleteSelfLessonCommand, Result<SelfLessonResponse>>
{
    private static readonly Error NotFound = new("scheduling.entry_not_found", "Program girdisi bulunamadi.");
    private readonly ILessonScheduleRepository _repository;
    private readonly IClock _clock;

    public DeleteSelfLessonCommandHandler(ILessonScheduleRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<SelfLessonResponse>> Handle(DeleteSelfLessonCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null || !lesson.IsSelfPlanned || lesson.Status == LessonScheduleStatus.Cancelled)
        {
            return Result<SelfLessonResponse>.Failure(NotFound);
        }

        // Soft-cancel: hatırlatma LessonScheduleCancelledDomainEvent ile iptal edilir.
        lesson.Cancel(CancellationReason.Other, isChargeable: false, cancellationNote: null, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<SelfLessonResponse>.Success(lesson.ToSelfResponse());
    }
}

internal static class SelfLessonMappings
{
    public static SelfLessonResponse ToSelfResponse(this LessonSchedule lesson)
        => new(
            lesson.Id,
            lesson.StudentId,
            lesson.Subject,
            lesson.Topic,
            lesson.StartAtUtc,
            lesson.EndAtUtc,
            lesson.TimeZone,
            lesson.RecurrenceRule,
            lesson.ReminderOffsetMinutes,
            lesson.ColorHex,
            lesson.Notes,
            lesson.Status.ToString(),
            lesson.CreatedOnUtc,
            lesson.UpdatedOnUtc);
}
