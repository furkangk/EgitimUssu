using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed record CreateStudyScheduleEntryCommand(
    Guid StudentId,
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes) : ICommand<Result<StudyScheduleEntryResponse>>;

public sealed record UpdateStudyScheduleEntryCommand(
    Guid EntryId,
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? ColorHex,
    string? Notes) : ICommand<Result<StudyScheduleEntryResponse>>;

public sealed record DeleteStudyScheduleEntryCommand(Guid EntryId) : ICommand<Result<StudyScheduleEntryResponse>>;

/// <summary>
/// Öğrencinin takvimi: öğretmenin planladığı dersler + öğrencinin kendi programı, tekrar kuralları
/// [StartAtUtc, EndAtUtc] aralığına genişletilerek birlikte döner.
/// </summary>
public sealed record GetStudentCalendarQuery(
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>;

public sealed record StudyScheduleEntryResponse(
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

/// <summary>Takvimde gösterilen tek bir somut oluşum. <c>Source</c>: "Teacher" (salt-okunur) veya "Self".</summary>
public sealed record StudentCalendarOccurrenceResponse(
    Guid EntryId,
    string Source,
    string Subject,
    string? Topic,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? LessonFormat,
    string? LocationLabel,
    string? ColorHex,
    string? RecurrenceRule,
    string? Notes,
    bool IsEditable);

public interface IStudyScheduleEntryRepository
{
    Task<StudyScheduleEntry?> GetByIdAsync(Guid entryId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudyScheduleEntry>> ListActiveForStudentAsync(Guid studentId, CancellationToken cancellationToken);

    Task AddAsync(StudyScheduleEntry entry, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal static class StudyScheduleConflict
{
    /// <summary>Çakışma kontrolü için ileriye dönük tarama penceresi.</summary>
    public const int HorizonDays = 180;

    /// <summary>
    /// Öğrencinin kendi programı için önerilen [start,end]+kural aralığındaki oluşumların herhangi biri,
    /// öğretmenin planladığı (iptal edilmemiş) bir ders oluşumuyla zamanda çakışıyor mu?
    /// </summary>
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
                // İptal/atlanan oluşum o an ders yapılmadığı için çakışma sayılmaz.
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

    /// <summary>
    /// Verilen derslerin tekrar serilerine ait occurrence istisnalarını ders kimliğine göre toplar.
    /// Yalnız tekrarlı dersler için sorgu yapılır (tek-seferlik derste istisna anlamsız).
    /// </summary>
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
}

public sealed class CreateStudyScheduleEntryCommandHandler : ICommandHandler<CreateStudyScheduleEntryCommand, Result<StudyScheduleEntryResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error TeacherConflict = new("scheduling.teacher_conflict", "Bu saatte ogretmeninin planladigi bir ders var; kendi dersini bu saate ekleyemezsin.");
    private readonly IStudyScheduleEntryRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateStudyScheduleEntryCommandHandler(
        IStudyScheduleEntryRepository repository,
        ILessonScheduleRepository lessonRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonRepository = lessonRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudyScheduleEntryResponse>> Handle(CreateStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<StudyScheduleEntryResponse>.Failure(InvalidRange);
        }

        var teacherLessons = await _lessonRepository.ListActiveForStudentUntilAsync(
            command.StudentId,
            command.StartAtUtc.AddDays(StudyScheduleConflict.HorizonDays),
            cancellationToken);

        var exceptionsByLesson = await StudyScheduleConflict.LoadExceptionsByLessonAsync(_lessonRepository, teacherLessons, cancellationToken);

        if (StudyScheduleConflict.OverlapsTeacherLesson(command.StartAtUtc, command.EndAtUtc, command.RecurrenceRule?.Trim(), teacherLessons, exceptionsByLesson))
        {
            return Result<StudyScheduleEntryResponse>.Failure(TeacherConflict);
        }

        var entry = new StudyScheduleEntry(
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

        await _repository.AddAsync(entry, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudyScheduleEntryResponse>.Success(entry.ToResponse());
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateStudyScheduleEntryCommandHandler : ICommandHandler<UpdateStudyScheduleEntryCommand, Result<StudyScheduleEntryResponse>>
{
    private static readonly Error NotFound = new("scheduling.entry_not_found", "Program girdisi bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error TeacherConflict = new("scheduling.teacher_conflict", "Bu saatte ogretmeninin planladigi bir ders var; kendi dersini bu saate ekleyemezsin.");
    private readonly IStudyScheduleEntryRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly IClock _clock;

    public UpdateStudyScheduleEntryCommandHandler(
        IStudyScheduleEntryRepository repository,
        ILessonScheduleRepository lessonRepository,
        IClock clock)
    {
        _repository = repository;
        _lessonRepository = lessonRepository;
        _clock = clock;
    }

    public async Task<Result<StudyScheduleEntryResponse>> Handle(UpdateStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<StudyScheduleEntryResponse>.Failure(InvalidRange);
        }

        var entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken);
        if (entry is null || !entry.IsActive)
        {
            return Result<StudyScheduleEntryResponse>.Failure(NotFound);
        }

        var teacherLessons = await _lessonRepository.ListActiveForStudentUntilAsync(
            entry.StudentId,
            command.StartAtUtc.AddDays(StudyScheduleConflict.HorizonDays),
            cancellationToken);

        var exceptionsByLesson = await StudyScheduleConflict.LoadExceptionsByLessonAsync(_lessonRepository, teacherLessons, cancellationToken);

        if (StudyScheduleConflict.OverlapsTeacherLesson(command.StartAtUtc, command.EndAtUtc, command.RecurrenceRule?.Trim(), teacherLessons, exceptionsByLesson))
        {
            return Result<StudyScheduleEntryResponse>.Failure(TeacherConflict);
        }

        entry.UpdateDetails(
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

        return Result<StudyScheduleEntryResponse>.Success(entry.ToResponse());
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DeleteStudyScheduleEntryCommandHandler : ICommandHandler<DeleteStudyScheduleEntryCommand, Result<StudyScheduleEntryResponse>>
{
    private static readonly Error NotFound = new("scheduling.entry_not_found", "Program girdisi bulunamadi.");
    private readonly IStudyScheduleEntryRepository _repository;
    private readonly IClock _clock;

    public DeleteStudyScheduleEntryCommandHandler(IStudyScheduleEntryRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StudyScheduleEntryResponse>> Handle(DeleteStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken);
        if (entry is null || !entry.IsActive)
        {
            return Result<StudyScheduleEntryResponse>.Failure(NotFound);
        }

        entry.Cancel(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudyScheduleEntryResponse>.Success(entry.ToResponse());
    }
}

public sealed class GetStudentCalendarQueryHandler : IQueryHandler<GetStudentCalendarQuery, Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>
{
    private readonly IStudyScheduleEntryRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;

    public GetStudentCalendarQueryHandler(
        IStudyScheduleEntryRepository repository,
        ILessonScheduleRepository lessonRepository)
    {
        _repository = repository;
        _lessonRepository = lessonRepository;
    }

    public async Task<Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>> Handle(GetStudentCalendarQuery query, CancellationToken cancellationToken)
    {
        var occurrences = new List<StudentCalendarOccurrenceResponse>();

        var lessons = await _lessonRepository.ListActiveForStudentUntilAsync(query.StudentId, query.EndAtUtc, cancellationToken);
        var exceptionsByLesson = await StudyScheduleConflict.LoadExceptionsByLessonAsync(_lessonRepository, lessons, cancellationToken);
        foreach (var lesson in lessons)
        {
            var lessonExceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var ex)
                ? ex
                : Array.Empty<OccurrenceOverride>();

            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, query.StartAtUtc, query.EndAtUtc, lessonExceptions))
            {
                // İptal edilen tek oluşum takvimde gösterilmez (bu sürümde soluk-gösterim alanı yok).
                if (occurrence.IsCancelled)
                {
                    continue;
                }

                occurrences.Add(new StudentCalendarOccurrenceResponse(
                    lesson.Id,
                    "Teacher",
                    lesson.Subject,
                    null,
                    occurrence.StartAtUtc,
                    occurrence.EndAtUtc,
                    lesson.LessonFormat.ToString(),
                    lesson.LocationLabel,
                    null,
                    lesson.RecurrenceRule,
                    lesson.Notes,
                    IsEditable: false));
            }
        }

        var entries = await _repository.ListActiveForStudentAsync(query.StudentId, cancellationToken);
        foreach (var entry in entries)
        {
            foreach (var occurrence in RecurrenceExpander.Expand(
                entry.StartAtUtc, entry.EndAtUtc, entry.RecurrenceRule, query.StartAtUtc, query.EndAtUtc))
            {
                occurrences.Add(new StudentCalendarOccurrenceResponse(
                    entry.Id,
                    "Self",
                    entry.Subject,
                    entry.Topic,
                    occurrence.StartAtUtc,
                    occurrence.EndAtUtc,
                    null,
                    null,
                    entry.ColorHex,
                    entry.RecurrenceRule,
                    entry.Notes,
                    IsEditable: true));
            }
        }

        var payload = occurrences
            .OrderBy(occurrence => occurrence.StartAtUtc)
            .ToArray();

        return Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>.Success(payload);
    }
}

internal static class StudyScheduleMappings
{
    public static StudyScheduleEntryResponse ToResponse(this StudyScheduleEntry entry)
    {
        return new StudyScheduleEntryResponse(
            entry.Id,
            entry.StudentId,
            entry.Subject,
            entry.Topic,
            entry.StartAtUtc,
            entry.EndAtUtc,
            entry.TimeZone,
            entry.RecurrenceRule,
            entry.ReminderOffsetMinutes,
            entry.ColorHex,
            entry.Notes,
            entry.Status.ToString(),
            entry.CreatedOnUtc,
            entry.UpdatedOnUtc);
    }
}
