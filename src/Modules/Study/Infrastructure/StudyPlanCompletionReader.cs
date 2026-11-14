using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>
/// Ç-06: Tamamlanmış ve bir plana bağlı (LessonId != null) çalışma seanslarından (LessonId, tarih) kümesini
/// döner. Scheduling, takvim occurrence'ının "çalışıldı" rozetini bu okuma sözleşmesiyle doldurur.
/// Tarih, seansın başlangıç (StartedAtUtc) gününe göre alınır — plan occurrence'ı ile eşleşme bu gün üzerinden yapılır.
/// P16 T5 / N-11 (K-19): gün <b>Türkiye yerel günüdür</b> (Salı 01:30 TR seansı Salı) ve aralık yerel günlerin UTC
/// karşılığıyla süzülür; Scheduling occurrence'ı da yerel güne çevirerek eşler — iki taraf aynı kuralda kalmalı.
/// </summary>
internal sealed class StudyPlanCompletionReader : IStudyPlanCompletionReader
{
    private readonly StudyDbContext _dbContext;
    private readonly ILocalCalendar _calendar;

    public StudyPlanCompletionReader(StudyDbContext dbContext, ILocalCalendar calendar)
    {
        _dbContext = dbContext;
        _calendar = calendar;
    }

    public async Task<IReadOnlyCollection<PlanCompletion>> GetCompletionsAsync(
        Guid studentId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var from = _calendar.DayRangeUtc(fromDate).StartUtc;
        var to = _calendar.DayRangeUtc(toDate).EndUtc;

        var rows = await _dbContext.StudySessions
            .Where(s => s.StudentId == studentId
                && s.LessonId != null
                && s.Status == StudySessionStatus.Completed
                && s.StartedAtUtc >= from
                && s.StartedAtUtc < to)
            .Select(s => new { s.LessonId, s.StartedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new PlanCompletion(r.LessonId!.Value, _calendar.ToLocalDate(r.StartedAtUtc)))
            .Distinct()
            .ToList();
    }
}
