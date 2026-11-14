using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Application;

// ---- İşaret arayüzleri (açık-generik yetkilendiriciler bunlara bakar) ----

/// <summary>Öğretmen-kapsamlı rapor isteği; yalnız öğretmenin kendisi (veya Admin) okur.</summary>
public interface ITeacherScopedReportRequest
{
    Guid TeacherUserId { get; }
}

/// <summary>Öğrenci-kapsamlı rapor isteği; öğrenci kendisi, bağlı öğretmeni, onaylı velisi (veya Admin) okur.</summary>
public interface IStudentScopedReportRequest
{
    Guid StudentId { get; }
}

// ---- Sorgular ----

/// <summary>M14-1: Öğretmenin bir aydaki ders/tahsilat özeti (M07-3 dönemsel gelir raporunun karşılığı).</summary>
public sealed record GetTeacherMonthlyReportQuery(Guid TeacherUserId, int Year, int Month)
    : IQuery<Result<TeacherMonthlyReportResponse>>, ITeacherScopedReportRequest;

/// <summary>M14-5: Öğretmenin verilen aralıktaki boş zaman dilimleri.</summary>
public sealed record GetTeacherFreeSlotsQuery(Guid TeacherUserId, DateTime FromUtc, DateTime ToUtc)
    : IQuery<Result<IReadOnlyCollection<FreeSlotResponse>>>, ITeacherScopedReportRequest;

/// <summary>M14-2: Öğrencinin haftalık çalışma analizi.</summary>
public sealed record GetStudentStudyAnalysisQuery(Guid StudentId, DateOnly? From, DateOnly? To)
    : IQuery<Result<IReadOnlyCollection<StudentStudyWeekResponse>>>, IStudentScopedReportRequest;

/// <summary>M14-3: Öğrencinin ders bazlı aylık performansı (delta sorgu anında türetilir).</summary>
public sealed record GetStudentPerformanceQuery(Guid StudentId, string? Subject, DateOnly? From, DateOnly? To)
    : IQuery<Result<IReadOnlyCollection<StudentPerformanceMonthResponse>>>, IStudentScopedReportRequest;

// ---- Yanıtlar ----

public sealed record TeacherMonthlyReportResponse(
    Guid TeacherUserId,
    int Year,
    int Month,
    int CompletedLessonCount,
    int CancelledLessonCount,
    int ActiveStudentCount,
    decimal ExpectedAmount,
    decimal CollectedAmount,
    DateTime? UpdatedOnUtc);

public sealed record FreeSlotResponse(DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes);

public sealed record StudentStudyWeekResponse(
    int Year,
    int WeekOfYear,
    DateOnly WeekStart,
    int StudyMinutes,
    int SessionCount,
    int TestCount);

public sealed record StudentPerformanceMonthResponse(
    string Subject,
    int Year,
    int Month,
    int MasteryScore,
    decimal AverageNet,
    int TestCount,
    int Delta);

// ---- Projeksiyon okuyucuları (Infrastructure uygular) ----

public interface IReportingProjectionReader
{
    Task<TeacherMonthlyProjection?> GetTeacherMonthlyAsync(
        Guid teacherUserId, int year, int month, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentStudyProjection>> ListStudentStudyAsync(
        Guid studentId, (int Year, int Week)? from, (int Year, int Week)? to, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentPerformanceProjection>> ListStudentPerformanceAsync(
        Guid studentId, string? subject, (int Year, int Month)? from, (int Year, int Month)? to, CancellationToken cancellationToken);

    /// <summary>
    /// M14-4: Öğrencinin verilen ay aralığında tamamlanan ders sayısı. Ders sayacı öğretmen aylık
    /// projeksiyonunun satırlarında durur (her tamamlanan ders bir <c>CompletedLesson</c> satırıdır);
    /// öğrenci burada <b>tüm öğretmenleri</b> üzerinden sayılır.
    /// </summary>
    /// <param name="studentId">Öğrenci profili kimliği.</param>
    /// <param name="from">Başlangıç ayı (dahil).</param>
    /// <param name="to">Bitiş ayı (dahil).</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Tamamlanan ders sayısı.</returns>
    Task<int> CountStudentCompletedLessonsAsync(
        Guid studentId, (int Year, int Month) from, (int Year, int Month) to, CancellationToken cancellationToken);
}

internal static class ReportingErrors
{
    public static readonly Error Forbidden = new("shared.forbidden", "Bu rapor verisini görme yetkiniz yok.");

    public static readonly Error InvalidRange = new(
        "reporting.invalid_range", "Rapor aralığı geçersiz (bitiş, başlangıçtan sonra olmalı).");

    /// <summary>M14-4: Premium kapısı. P09'da <c>IEntitlementDirectory</c> ile aynı kod korunarak değişir.</summary>
    public static readonly Error PremiumRequired = new(
        "reporting.premium_required", "PDF rapor Premium üyelik gerektirir.");

    public static readonly Error ReportNotShared = new(
        "reporting.report_not_shared", "Öğrenci çalışma verisini paylaşmadığı için rapor üretilemez.");

    public static readonly Error ReportNotFound = new(
        "reporting.report_not_found", "Rapor bulunamadı.");
}

// ---- M14-1: öğretmen aylık ----

public sealed class GetTeacherMonthlyReportQueryHandler
    : IQueryHandler<GetTeacherMonthlyReportQuery, Result<TeacherMonthlyReportResponse>>
{
    private readonly IReportingProjectionReader _reader;

    public GetTeacherMonthlyReportQueryHandler(IReportingProjectionReader reader) => _reader = reader;

    public async Task<Result<TeacherMonthlyReportResponse>> Handle(
        GetTeacherMonthlyReportQuery query, CancellationToken cancellationToken)
    {
        if (query.Month is < 1 or > 12 || query.Year < 2000)
        {
            return Result<TeacherMonthlyReportResponse>.Failure(ReportingErrors.InvalidRange);
        }

        var projection = await _reader.GetTeacherMonthlyAsync(
            query.TeacherUserId, query.Year, query.Month, cancellationToken);

        // Hiç olay gelmemiş bir ay "veri yok" değil, **sıfır aktivite**dir: öğretmen o ay ders yapmamış
        // olabilir. Panel sayaçlarından (dashboard_not_ready) farkı budur — orada satırın hiç olmaması
        // "projeksiyon henüz beslenmedi" anlamına geliyordu, burada boş ay meşru bir cevaptır.
        if (projection is null)
        {
            return Result<TeacherMonthlyReportResponse>.Success(new TeacherMonthlyReportResponse(
                query.TeacherUserId, query.Year, query.Month, 0, 0, 0, 0m, 0m, null));
        }

        return Result<TeacherMonthlyReportResponse>.Success(new TeacherMonthlyReportResponse(
            projection.TeacherUserId,
            projection.Year,
            projection.Month,
            projection.CompletedLessonCount,
            projection.CancelledLessonCount,
            projection.ActiveStudentCount,
            projection.ExpectedAmount,
            projection.CollectedAmount,
            projection.UpdatedOnUtc));
    }
}

// ---- M14-5: boş zaman analizi ----

public sealed class GetTeacherFreeSlotsQueryHandler
    : IQueryHandler<GetTeacherFreeSlotsQuery, Result<IReadOnlyCollection<FreeSlotResponse>>>
{
    /// <summary>Aralık üst sınırı: uygunluk şablonu her güne yayıldığı için sınırsız pencere kabul edilmez.</summary>
    private const int MaxRangeDays = 92;

    private readonly ITeacherAvailabilityDirectory _availability;
    private readonly ILessonScheduleDirectory _schedules;
    private readonly ILocalCalendar _calendar;

    public GetTeacherFreeSlotsQueryHandler(
        ITeacherAvailabilityDirectory availability, ILessonScheduleDirectory schedules, ILocalCalendar calendar)
    {
        _availability = availability;
        _schedules = schedules;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyCollection<FreeSlotResponse>>> Handle(
        GetTeacherFreeSlotsQuery query, CancellationToken cancellationToken)
    {
        if (query.ToUtc <= query.FromUtc || (query.ToUtc - query.FromUtc).TotalDays > MaxRangeDays)
        {
            return Result<IReadOnlyCollection<FreeSlotResponse>>.Failure(ReportingErrors.InvalidRange);
        }

        var slots = await _availability.ListWeeklySlotsAsync(query.TeacherUserId, cancellationToken);
        var busy = await _schedules.ListTeacherBusyIntervalsAsync(
            query.TeacherUserId, query.FromUtc, query.ToUtc, cancellationToken);

        var free = FreeSlotCalculator.Compute(slots, busy, query.FromUtc, query.ToUtc, _calendar)
            .Select(item => new FreeSlotResponse(
                item.StartAtUtc, item.EndAtUtc, (int)(item.EndAtUtc - item.StartAtUtc).TotalMinutes))
            .ToArray();

        return Result<IReadOnlyCollection<FreeSlotResponse>>.Success(free);
    }
}

// ---- M14-2: öğrenci çalışma analizi ----

public sealed class GetStudentStudyAnalysisQueryHandler
    : IQueryHandler<GetStudentStudyAnalysisQuery, Result<IReadOnlyCollection<StudentStudyWeekResponse>>>
{
    private readonly IReportingProjectionReader _reader;
    private readonly StudentReportPrivacyGate _privacy;

    public GetStudentStudyAnalysisQueryHandler(IReportingProjectionReader reader, StudentReportPrivacyGate privacy)
    {
        _reader = reader;
        _privacy = privacy;
    }

    public async Task<Result<IReadOnlyCollection<StudentStudyWeekResponse>>> Handle(
        GetStudentStudyAnalysisQuery query, CancellationToken cancellationToken)
    {
        var visibility = await _privacy.ResolveAsync(query.StudentId, cancellationToken);
        if (visibility == ReportVisibility.Hidden)
        {
            return Result<IReadOnlyCollection<StudentStudyWeekResponse>>.Success(Array.Empty<StudentStudyWeekResponse>());
        }

        var weeks = await _reader.ListStudentStudyAsync(
            query.StudentId, WeekBound(query.From), WeekBound(query.To), cancellationToken);

        var payload = weeks
            .OrderBy(item => item.Year)
            .ThenBy(item => item.WeekOfYear)
            .Select(item => new StudentStudyWeekResponse(
                item.Year,
                item.WeekOfYear,
                DateOnly.FromDateTime(System.Globalization.ISOWeek.ToDateTime(item.Year, item.WeekOfYear, DayOfWeek.Monday)),
                item.StudyMinutes,
                // Limited: yalnız haftalık toplam süre paylaşılır, seans/test kırılımı gizlenir (M15-4).
                visibility == ReportVisibility.Limited ? 0 : item.SessionCount,
                visibility == ReportVisibility.Limited ? 0 : item.TestCount))
            .ToArray();

        return Result<IReadOnlyCollection<StudentStudyWeekResponse>>.Success(payload);
    }

    private static (int Year, int Week)? WeekBound(DateOnly? date) => date is null
        ? null
        : ReportingPeriods.WeekOf(date.Value);   // istemcinin yerel tarihi: tarih tabanlı kova (P16 T5)
}

// ---- M14-3: öğrenci performansı ----

public sealed class GetStudentPerformanceQueryHandler
    : IQueryHandler<GetStudentPerformanceQuery, Result<IReadOnlyCollection<StudentPerformanceMonthResponse>>>
{
    private readonly IReportingProjectionReader _reader;
    private readonly StudentReportPrivacyGate _privacy;

    public GetStudentPerformanceQueryHandler(IReportingProjectionReader reader, StudentReportPrivacyGate privacy)
    {
        _reader = reader;
        _privacy = privacy;
    }

    public async Task<Result<IReadOnlyCollection<StudentPerformanceMonthResponse>>> Handle(
        GetStudentPerformanceQuery query, CancellationToken cancellationToken)
    {
        var visibility = await _privacy.ResolveAsync(query.StudentId, cancellationToken);

        // Performans satırı doğrudan test/hâkimiyet detayıdır; Limited seviyesi bunu paylaşmaz.
        if (visibility is ReportVisibility.Hidden or ReportVisibility.Limited)
        {
            return Result<IReadOnlyCollection<StudentPerformanceMonthResponse>>.Success(
                Array.Empty<StudentPerformanceMonthResponse>());
        }

        var rows = await _reader.ListStudentPerformanceAsync(
            query.StudentId, NormalizeSubject(query.Subject), MonthBound(query.From), MonthBound(query.To), cancellationToken);

        // Delta saklanmaz: her (ders) serisi kendi içinde sıralanır ve bir önceki ayla farktan üretilir
        // (Öğrenilenler #102). Kırpılmış serinin ilk ayı 0 delta alır — karşılaştıracağı ay yoktur.
        var payload = rows
            .GroupBy(item => item.Subject)
            .OrderBy(series => series.Key)
            .SelectMany(series =>
            {
                int? previousScore = null;
                return series
                    .OrderBy(item => item.Year)
                    .ThenBy(item => item.Month)
                    .Select(item =>
                    {
                        var delta = ReportingDelta.FromScores(previousScore, item.MasteryScore);
                        previousScore = item.MasteryScore;
                        return new StudentPerformanceMonthResponse(
                            item.Subject, item.Year, item.Month, item.MasteryScore, item.AverageNet, item.TestCount, delta);
                    })
                    .ToArray();
            })
            .ToArray();

        return Result<IReadOnlyCollection<StudentPerformanceMonthResponse>>.Success(payload);
    }

    private static string? NormalizeSubject(string? subject)
        => string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();

    private static (int Year, int Month)? MonthBound(DateOnly? date) => date is null
        ? null
        : (date.Value.Year, date.Value.Month);
}
