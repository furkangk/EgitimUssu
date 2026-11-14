using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

/// <summary>
/// Öğrencinin takvimi: birleşik <c>lesson_schedules</c>'ten hem öğretmen dersleri (source=Teacher, salt-okunur)
/// hem öğrencinin kendi dersleri (source=Self, düzenlenebilir) tek kaynaktan, tekrar kuralları
/// [StartAtUtc, EndAtUtc] aralığına genişletilerek döner (Ç-06 birleşimi).
/// </summary>
public sealed record GetStudentCalendarQuery(
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>;

/// <summary>
/// Takvimde gösterilen tek bir somut oluşum. <c>Source</c>: "Teacher" (salt-okunur) veya "Self".
/// <c>ConflictsWithLesson</c> (M04-2/M08-1) yalnız bireysel plan (Self) öğelerinde anlamlıdır:
/// öğrencinin kendi planı bir özel dersle aynı saate denk geliyorsa <c>true</c> olur.
/// </summary>
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
    bool IsEditable,
    bool Completed,
    bool ConflictsWithLesson);

public sealed class GetStudentCalendarQueryHandler : IQueryHandler<GetStudentCalendarQuery, Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>
{
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly IStudyPlanCompletionReader _completionReader;
    private readonly ILocalCalendar _calendar;

    public GetStudentCalendarQueryHandler(
        ILessonScheduleRepository lessonRepository,
        IStudyPlanCompletionReader completionReader,
        ILocalCalendar calendar)
    {
        _lessonRepository = lessonRepository;
        _completionReader = completionReader;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>> Handle(GetStudentCalendarQuery query, CancellationToken cancellationToken)
    {
        var occurrences = new List<StudentCalendarOccurrenceResponse>();

        // Tek kaynak: öğretmen + kendi dersleri (TeacherUserId null = self).
        var lessons = await _lessonRepository.ListActiveForStudentUntilAsync(query.StudentId, query.EndAtUtc, cancellationToken);
        var exceptionsByLesson = await SelfLessonConflict.LoadExceptionsByLessonAsync(_lessonRepository, lessons, cancellationToken);

        // Ç-06: (LessonId, tarih) → çalışıldı kümesi (Study modülünden okuma sözleşmesiyle).
        // P16 T5 / N-11: tarihler Türkiye yerel günüdür — iki taraf da aynı kuralla eşleşir (Salı 01:30 TR dersi Salı).
        var completions = await _completionReader.GetCompletionsAsync(
            query.StudentId,
            _calendar.ToLocalDate(query.StartAtUtc),
            _calendar.ToLocalDate(query.EndAtUtc),
            cancellationToken);
        var completedSet = completions.Select(c => (c.LessonId, c.Date)).ToHashSet();

        // M04-2/M08-1: Bireysel plan ile özel ders çakışması. Kendi planını oluştururken çakışma
        // reddedilir (409), ama öğretmen sonradan ders açabilir/erteleyebilir — o durumda kart uyarılır.
        var teacherOccurrences = new List<(DateTime StartAtUtc, DateTime EndAtUtc)>();

        foreach (var lesson in lessons)
        {
            var isSelf = lesson.TeacherUserId is null;
            var lessonExceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var ex)
                ? ex
                : Array.Empty<OccurrenceOverride>();

            // P16 T2 / N-04: seri kendi diliminin yerel takviminde genişletilir.
            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone),
                query.StartAtUtc, query.EndAtUtc, lessonExceptions))
            {
                // İptal edilen tek oluşum takvimde gösterilmez.
                if (occurrence.IsCancelled)
                {
                    continue;
                }

                if (!isSelf)
                {
                    teacherOccurrences.Add((occurrence.StartAtUtc, occurrence.EndAtUtc));
                }

                occurrences.Add(new StudentCalendarOccurrenceResponse(
                    lesson.Id,
                    isSelf ? "Self" : "Teacher",
                    lesson.Subject,
                    lesson.Topic,
                    occurrence.StartAtUtc,
                    occurrence.EndAtUtc,
                    lesson.LessonFormat?.ToString(),
                    lesson.LocationLabel,
                    lesson.ColorHex,
                    lesson.RecurrenceRule,
                    lesson.Notes,
                    IsEditable: isSelf,
                    Completed: completedSet.Contains((lesson.Id, _calendar.ToLocalDate(occurrence.StartAtUtc))),
                    ConflictsWithLesson: false));
            }
        }

        var payload = occurrences
            .Select(occurrence => occurrence.Source == "Self" && OverlapsAnyLesson(occurrence, teacherOccurrences)
                ? occurrence with { ConflictsWithLesson = true }
                : occurrence)
            .OrderBy(occurrence => occurrence.StartAtUtc)
            .ToArray();

        return Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>.Success(payload);
    }

    private static bool OverlapsAnyLesson(
        StudentCalendarOccurrenceResponse candidate,
        IReadOnlyCollection<(DateTime StartAtUtc, DateTime EndAtUtc)> teacherOccurrences)
        => teacherOccurrences.Any(lesson =>
            candidate.StartAtUtc < lesson.EndAtUtc && candidate.EndAtUtc > lesson.StartAtUtc);
}
