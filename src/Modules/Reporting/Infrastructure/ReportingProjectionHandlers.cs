using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Projections;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>
/// M14 raporlama projeksiyonlarını besleyen handler'ların ortak tabanı: dönem kovasının satırını
/// bulur/oluşturur. SaveChanges + inbox satırı tabana (tek transaction) aittir.
/// </summary>
public abstract class ReportingProjectionHandler<TEvent> : ProjectionHandler<TEvent>
    where TEvent : class
{
    protected ReportingProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock)
    {
        Calendar = calendar;
    }

    /// <summary>P16 T5 / N-50: dönem kovası olayın <b>yerel</b> gününe göre (K-19).</summary>
    protected ILocalCalendar Calendar { get; }

    protected ReportingDbContext ReportingDb => (ReportingDbContext)DbContext;

    protected async Task<TeacherMonthlyProjection> GetOrCreateMonthlyAsync(
        Guid teacherUserId, DateTime occurredOnUtc, CancellationToken cancellationToken)
    {
        await TeacherDashboardOccurrenceProcessor.AcquireLockAsync(ReportingDb, cancellationToken);
        var (year, month) = ReportingPeriods.MonthOf(occurredOnUtc, Calendar);
        var projection = await ReportingDb.TeacherMonthlyProjections
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.TeacherUserId == teacherUserId && item.Year == year && item.Month == month,
                cancellationToken);

        if (projection is null)
        {
            projection = new TeacherMonthlyProjection(teacherUserId, year, month);
            ReportingDb.TeacherMonthlyProjections.Add(projection);
        }

        return projection;
    }

    protected async Task<StudentPerformanceProjection> GetOrCreatePerformanceAsync(
        Guid studentId, string subject, DateTime occurredOnUtc, CancellationToken cancellationToken)
    {
        var (year, month) = ReportingPeriods.MonthOf(occurredOnUtc, Calendar);
        var normalized = subject.Trim();
        var projection = await ReportingDb.StudentPerformanceProjections
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.StudentId == studentId
                    && item.Subject == normalized
                    && item.Year == year
                    && item.Month == month,
                cancellationToken);

        if (projection is null)
        {
            projection = new StudentPerformanceProjection(studentId, normalized, year, month);
            ReportingDb.StudentPerformanceProjections.Add(projection);
        }

        return projection;
    }
}

// --- M14-1: öğretmen aylık ---------------------------------------------------------------------

/// <summary>M05 → M14: Ders tamamlandı; tamamlanma <b>ayına</b> (Türkiye yerel ayı, P16 T5) yazılır.</summary>
public sealed class TeacherMonthlyLessonCompletedProjectionHandler
    : ReportingProjectionHandler<MonthlyLessonCompletedPayload>
{
    public TeacherMonthlyLessonCompletedProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "LessonSessions"
            && integrationEvent.Name == "LessonSessionCompletedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        MonthlyLessonCompletedPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.TeacherUserId == Guid.Empty)
        {
            throw new PoisonMessageException("Raporlama olay alanları geçersiz.");
        }

        var projection = await GetOrCreateMonthlyAsync(payload.TeacherUserId, payload.CompletedOnUtc, cancellationToken);
        projection.TrackCompletedLesson(payload.LessonSessionId, payload.StudentId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M04 → M14: Ders iptal edildi; iptal ayına yazılır (öğretmensiz self ders sayılmaz).</summary>
public sealed class TeacherMonthlyLessonCancelledProjectionHandler
    : ReportingProjectionHandler<MonthlyLessonCancelledPayload>
{
    public TeacherMonthlyLessonCancelledProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Scheduling"
            && integrationEvent.Name == "LessonScheduleCancelledDomainEvent";

    protected override async Task<bool> ProjectAsync(
        MonthlyLessonCancelledPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.TeacherUserId is not { } teacherUserId || teacherUserId == Guid.Empty)
        {
            return true; // öğretmensiz self ders
        }

        var projection = await GetOrCreateMonthlyAsync(teacherUserId, payload.CancelledOnUtc, cancellationToken);
        projection.TrackCancelledLesson(payload.LessonScheduleId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>Single iptal, serinin diğer haftalarından ayrı aylık sayaçtır.</summary>
public sealed class TeacherMonthlyOccurrenceCancelledProjectionHandler(
    ReportingDbContext db, IClock clock, ILocalCalendar calendar)
    : ReportingProjectionHandler<LessonCancellationApplied>(db, clock, calendar)
{
    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Scheduling" && integrationEvent.Name == "LessonCancellationAppliedDomainEvent";

    protected override async Task<bool> ProjectAsync(LessonCancellationApplied payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        // All eski seri olayıyla sayılır; iki ayrı olayı aynı iptal olarak sayma.
        if (payload.Scope != "Single" || payload.TeacherUserId is not { } teacher || teacher == Guid.Empty
            || payload.OccurrenceStartUtc is not { } key) return true; // yalnız Single öğretmen oluşumu sayılır
        var projection = await GetOrCreateMonthlyAsync(teacher, payload.CancelledOnUtc, cancellationToken);
        projection.TrackCancelledOccurrence(payload.LessonScheduleId, key);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M07 → M14: Ödeme kaydı açıldı; beklenen tutar açılış ayına yazılır.</summary>
public sealed class TeacherMonthlyPaymentExpectedProjectionHandler
    : ReportingProjectionHandler<MonthlyPaymentCreatedPayload>
{
    public TeacherMonthlyPaymentExpectedProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments"
            && integrationEvent.Name == "PaymentRecordCreatedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        MonthlyPaymentCreatedPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.TeacherUserId == Guid.Empty)
        {
            throw new PoisonMessageException("Raporlama olay alanları geçersiz.");
        }

        var projection = await GetOrCreateMonthlyAsync(payload.TeacherUserId, payload.CreatedOnUtc, cancellationToken);
        projection.TrackExpectedPayment(payload.PaymentRecordId, payload.ExpectedAmount);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M07 → M14: Hareketin yerel ayına tek TransactionId ile tahsilat/iade yazar.</summary>
public sealed class TeacherMonthlyPaymentCollectedProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
    : ReportingProjectionHandler<MonthlyPaymentTransactionPayload>(dbContext, clock, calendar)
{
    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments" && integrationEvent.Name == "PaymentTransactionRecordedIntegrationEvent";

    protected override async Task<bool> ProjectAsync(MonthlyPaymentTransactionPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.TeacherUserId == Guid.Empty || payload.TransactionId == Guid.Empty || payload.Amount <= 0m
            || payload.Kind is not (1 or 2)) throw new PoisonMessageException("Raporlama olay alanları geçersiz.");
        var projection = await GetOrCreateMonthlyAsync(payload.TeacherUserId, payload.OccurredOnUtc, cancellationToken);
        projection.TrackPaymentTransaction(payload.TransactionId, payload.Kind == 2, payload.Amount);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M10 → M14: Konu hâkimiyeti değişti; dersin o ayki ortalamasına katılır.</summary>
public sealed class StudentPerformanceMasteryProjectionHandler : ReportingProjectionHandler<TopicMasteryPayload>
{
    public StudentPerformanceMasteryProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "ProgressTracking"
            && integrationEvent.Name == "TopicMasteryChangedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        TopicMasteryPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.StudentId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Topic))
        {
            throw new PoisonMessageException("Raporlama olay alanları geçersiz.");
        }

        await StudyStatisticsProjectionHandler.AcquireStudentLockAsync(ReportingDb, payload.StudentId, cancellationToken);
        var projection = await GetOrCreatePerformanceAsync(
            payload.StudentId, payload.Subject, payload.EvaluatedOnUtc, cancellationToken);
        projection.TrackTopicMastery(payload.Topic, payload.MasteryScore);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

// --- Payload sözleşmeleri -----------------------------------------------------------------------
// Kaynak modülün domain event'inin YALNIZ ihtiyaç duyulan alanları (bkz. TeacherDashboardProjectionHandlers).

public sealed record MonthlyLessonCompletedPayload(
    Guid LessonSessionId,
    Guid TeacherUserId,
    Guid StudentId,
    DateTime CompletedOnUtc);

public sealed record MonthlyLessonCancelledPayload(
    Guid LessonScheduleId,
    Guid? TeacherUserId,
    DateTime CancelledOnUtc);

public sealed record MonthlyPaymentCreatedPayload(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    decimal ExpectedAmount,
    DateTime CreatedOnUtc);

public sealed record MonthlyPaymentTransactionPayload(Guid TransactionId, Guid TeacherUserId, int Kind, decimal Amount, DateTime OccurredOnUtc);

public sealed record TopicMasteryPayload(
    Guid StudentId,
    string Subject,
    string Topic,
    decimal MasteryScore,
    DateTime EvaluatedOnUtc);

/// <summary>Study'nin güncel seans/test kümesini eski ve yeni yerel dönemlerle uzlaştırır.</summary>
public sealed class StudyStatisticsProjectionHandler(ReportingDbContext db, IStudyStatisticsReader source,
    IClock clock, ILocalCalendar calendar) : ReportingProjectionHandler<StudyStatisticsStudentPayload>(db, clock, calendar)
{
    public override bool CanHandle(IIntegrationEvent message) => StudyStatisticsEvents.Matches(message);
    protected override async Task<bool> ProjectAsync(StudyStatisticsStudentPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.StudentId == Guid.Empty) throw new PoisonMessageException("Raporlama öğrenci kimliği geçersiz.");
        await AcquireStudentLockAsync(ReportingDb, payload.StudentId, cancellationToken);
        var data = await source.ReadAsync(payload.StudentId, cancellationToken);
        var weeks = await ReportingDb.StudentStudyProjections.Include(x => x.Items)
            .Where(x => x.StudentId == payload.StudentId).ToListAsync(cancellationToken);
        var months = await ReportingDb.StudentPerformanceProjections.Include(x => x.Items)
            .Where(x => x.StudentId == payload.StudentId).ToListAsync(cancellationToken);
        var sessions = data.Sessions.GroupBy(x => ReportingPeriods.WeekOf(x.EndedAtUtc, Calendar))
            .ToDictionary(g => g.Key, g => g.ToArray());
        var weeklyTests = data.Tests.GroupBy(x => ReportingPeriods.WeekOf(x.TakenOnUtc, Calendar))
            .ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var (year, week) in sessions.Keys.Concat(weeklyTests.Keys).Distinct())
        {
            if (weeks.Any(x => x.Year == year && x.WeekOfYear == week)) continue;
            var projection = new StudentStudyProjection(payload.StudentId, year, week);
            ReportingDb.StudentStudyProjections.Add(projection); weeks.Add(projection);
        }
        foreach (var projection in weeks)
        {
            var key = (projection.Year, projection.WeekOfYear);
            var weekSessions = sessions.GetValueOrDefault(key) ?? [];
            var weekTests = weeklyTests.GetValueOrDefault(key) ?? [];
            projection.RemoveMissing(weekSessions.Select(x => x.Id).ToHashSet(), weekTests.Select(x => x.Id).ToHashSet());
            foreach (var session in weekSessions) projection.TrackSession(session.Id, session.EffectiveMinutes);
            foreach (var test in weekTests) projection.TrackTest(test.Id);
            projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        }
        var monthlyTests = data.Tests.GroupBy(x => (Subject: x.Subject.Trim(), Period: ReportingPeriods.MonthOf(x.TakenOnUtc, Calendar)))
            .ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var key in monthlyTests.Keys)
        {
            if (months.Any(x => x.Subject == key.Subject && x.Year == key.Period.Year && x.Month == key.Period.Month)) continue;
            var projection = new StudentPerformanceProjection(payload.StudentId, key.Subject, key.Period.Year, key.Period.Month);
            ReportingDb.StudentPerformanceProjections.Add(projection); months.Add(projection);
        }
        foreach (var projection in months)
        {
            var tests = monthlyTests.GetValueOrDefault((projection.Subject, (projection.Year, projection.Month))) ?? [];
            projection.RemoveMissingTests(tests.Select(x => x.Id.ToString("N")).ToHashSet());
            foreach (var test in tests)
                projection.TrackTestNet(test.Id, test.TotalQuestions > 0 ? test.Net / test.TotalQuestions : 0m);
            projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        }
        return true;
    }
    internal static async Task AcquireStudentLockAsync(ReportingDbContext db, Guid studentId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        var key = $"reporting:study:{studentId:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }
}
public sealed record StudyStatisticsStudentPayload(Guid StudentId);
