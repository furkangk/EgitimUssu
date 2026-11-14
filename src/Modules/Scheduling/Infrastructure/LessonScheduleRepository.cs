using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

internal sealed class LessonScheduleRepository : ILessonScheduleRepository
{
    private readonly SchedulingDbContext _dbContext;

    public LessonScheduleRepository(SchedulingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.FindAsync(new object?[] { lessonId }, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
    {
        // Ç-06: bu liste öğretmen derslerini döndürür (öğrencinin kendi dersleri /calendar'da gelir).
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.StudentId == studentId
                && lesson.TeacherUserId != null
                && lesson.StartAtUtc >= startAtUtc
                && lesson.StartAtUtc <= endAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.StudentId == studentId
                && lesson.Status != LessonScheduleStatus.Cancelled
                && (lesson.StartAtUtc <= untilUtc
                    // P16 T3 / N-14: seri pencereden sonra başlasa da bir oluşumu pencereye öne alınmış olabilir
                    // (ilk ders bir önceki haftaya) — seri yüklenir, pencereye düşüp düşmediğine genişletici karar verir.
                    || _dbContext.LessonOccurrenceExceptions.Any(x => x.SeriesLessonScheduleId == lesson.Id
                        && x.Action == OccurrenceExceptionAction.Rescheduled
                        && x.OverrideStartAtUtc <= untilUtc)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
    {
        // M04-1: Alt sınır yok — tekrar kuralı uygulama katmanında genişletildiği için pencereden
        // önce başlamış seriler de gerekir. İptal edilen seriler takvimde gösterilmez.
        return await _dbContext.LessonSchedules
            .Where(lesson => lesson.TeacherUserId == teacherUserId
                && lesson.Status != LessonScheduleStatus.Cancelled
                && (lesson.StartAtUtc <= untilUtc
                    // P16 T3 / N-14: pencereye öne alınmış oluşumu olan seri (öğrenci sorgusundaki kural).
                    || _dbContext.LessonOccurrenceExceptions.Any(x => x.SeriesLessonScheduleId == lesson.Id
                        && x.Action == OccurrenceExceptionAction.Rescheduled
                        && x.OverrideStartAtUtc <= untilUtc)))
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken)
    {
        return _dbContext.LessonSchedules.AddAsync(lessonSchedule, cancellationToken).AsTask();
    }

    public void Remove(LessonSchedule lessonSchedule)
    {
        _dbContext.LessonSchedules.Remove(lessonSchedule);
    }

    /// <summary>
    /// P16 T3 / N-05: Aynı (seri, orijinal oluşum) anahtarında satır varsa onu günceller, yoksa ekler. Aynı iş
    /// biriminde henüz kaydedilmemiş ekleme de bulunur (<c>Local</c>). Benzersiz indeks son savunma hattıdır:
    /// eşzamanlı iki ilk yazımın kaybedeni tek seferlik hata alır, kalıcı tekrar satırı oluşmaz.
    /// </summary>
    public async Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
    {
        var seriesId = occurrenceException.SeriesLessonScheduleId;
        var originalStartAtUtc = occurrenceException.OriginalStartAtUtc;
        var existing = _dbContext.LessonOccurrenceExceptions.Local
                .FirstOrDefault(x => x.SeriesLessonScheduleId == seriesId && x.OriginalStartAtUtc == originalStartAtUtc)
            ?? await _dbContext.LessonOccurrenceExceptions
                .FirstOrDefaultAsync(x => x.SeriesLessonScheduleId == seriesId && x.OriginalStartAtUtc == originalStartAtUtc, cancellationToken);

        if (existing is null)
        {
            await _dbContext.LessonOccurrenceExceptions.AddAsync(occurrenceException, cancellationToken);
            return;
        }

        existing.Overwrite(
            occurrenceException.Action,
            occurrenceException.OverrideStartAtUtc,
            occurrenceException.OverrideEndAtUtc,
            occurrenceException.Note);
    }

    public async Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
        => await _dbContext.LessonOccurrenceExceptions
            .Where(x => x.SeriesLessonScheduleId == seriesLessonScheduleId)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
        => await (from x in _dbContext.LessonOccurrenceExceptions
                  join l in _dbContext.LessonSchedules on x.SeriesLessonScheduleId equals l.Id
                  where l.TeacherUserId == teacherUserId
                  select x).ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
