using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>
/// Ç-06: Tamamlanmış ve bir plana bağlı (LessonId != null) çalışma seanslarından (LessonId, tarih) kümesini
/// döner. Scheduling, takvim occurrence'ının "çalışıldı" rozetini bu okuma sözleşmesiyle doldurur.
/// Tarih, seansın başlangıç (StartedAtUtc) gününe göre alınır — plan occurrence'ı ile eşleşme bu gün üzerinden yapılır.
/// </summary>
internal sealed class StudyPlanCompletionReader : IStudyPlanCompletionReader
{
    private readonly StudyDbContext _dbContext;

    public StudyPlanCompletionReader(StudyDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<PlanCompletion>> GetCompletionsAsync(
        Guid studentId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await _dbContext.StudySessions
            .Where(s => s.StudentId == studentId
                && s.LessonId != null
                && s.Status == StudySessionStatus.Completed
                && s.StartedAtUtc >= from
                && s.StartedAtUtc < to)
            .Select(s => new { s.LessonId, s.StartedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new PlanCompletion(r.LessonId!.Value, DateOnly.FromDateTime(r.StartedAtUtc)))
            .Distinct()
            .ToList();
    }
}
