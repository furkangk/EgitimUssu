using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
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

public sealed class GetStudentCalendarQueryHandler : IQueryHandler<GetStudentCalendarQuery, Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>>
{
    private readonly ILessonScheduleRepository _lessonRepository;

    public GetStudentCalendarQueryHandler(ILessonScheduleRepository lessonRepository)
    {
        _lessonRepository = lessonRepository;
    }

    public async Task<Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>> Handle(GetStudentCalendarQuery query, CancellationToken cancellationToken)
    {
        var occurrences = new List<StudentCalendarOccurrenceResponse>();

        // Tek kaynak: öğretmen + kendi dersleri (TeacherUserId null = self).
        var lessons = await _lessonRepository.ListActiveForStudentUntilAsync(query.StudentId, query.EndAtUtc, cancellationToken);
        var exceptionsByLesson = await SelfLessonConflict.LoadExceptionsByLessonAsync(_lessonRepository, lessons, cancellationToken);

        foreach (var lesson in lessons)
        {
            var isSelf = lesson.TeacherUserId is null;
            var lessonExceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var ex)
                ? ex
                : Array.Empty<OccurrenceOverride>();

            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, query.StartAtUtc, query.EndAtUtc, lessonExceptions))
            {
                // İptal edilen tek oluşum takvimde gösterilmez.
                if (occurrence.IsCancelled)
                {
                    continue;
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
                    IsEditable: isSelf));
            }
        }

        var payload = occurrences
            .OrderBy(occurrence => occurrence.StartAtUtc)
            .ToArray();

        return Result<IReadOnlyCollection<StudentCalendarOccurrenceResponse>>.Success(payload);
    }
}
