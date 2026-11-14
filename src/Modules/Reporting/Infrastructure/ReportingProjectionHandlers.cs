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

    protected async Task<StudentStudyProjection> GetOrCreateStudyAsync(
        Guid studentId, DateTime occurredOnUtc, CancellationToken cancellationToken)
    {
        var (year, week) = ReportingPeriods.WeekOf(occurredOnUtc, Calendar);
        var projection = await ReportingDb.StudentStudyProjections
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.StudentId == studentId && item.Year == year && item.WeekOfYear == week,
                cancellationToken);

        if (projection is null)
        {
            projection = new StudentStudyProjection(studentId, year, week);
            ReportingDb.StudentStudyProjections.Add(projection);
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
            return false;
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
            return false;
        }

        var projection = await GetOrCreateMonthlyAsync(teacherUserId, payload.CancelledOnUtc, cancellationToken);
        projection.TrackCancelledLesson(payload.LessonScheduleId);
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
            return false;
        }

        var projection = await GetOrCreateMonthlyAsync(payload.TeacherUserId, payload.CreatedOnUtc, cancellationToken);
        projection.TrackExpectedPayment(payload.PaymentRecordId, payload.ExpectedAmount);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>
/// M07 → M14: Tahsilat değişti; <b>fark</b> güncelleme ayına eklenir. Mutlak tutar yazılsaydı Ocak'ta
/// açılıp Şubat'ta tahsil edilen bir kayıt her iki aya da tam tutarla düşerdi.
/// </summary>
public sealed class TeacherMonthlyPaymentCollectedProjectionHandler
    : ReportingProjectionHandler<MonthlyPaymentUpdatedPayload>
{
    public TeacherMonthlyPaymentCollectedProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments"
            && integrationEvent.Name == "PaymentRecordUpdatedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        MonthlyPaymentUpdatedPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var delta = payload.CurrentCollectedAmount - payload.PreviousCollectedAmount;
        if (payload.TeacherUserId == Guid.Empty || delta == 0m)
        {
            return false;
        }

        var projection = await GetOrCreateMonthlyAsync(payload.TeacherUserId, payload.UpdatedOnUtc, cancellationToken);
        projection.AddCollected(delta);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

// --- M14-2: öğrenci haftalık çalışma ------------------------------------------------------------

/// <summary>M08 → M14: Çalışma seansı tamamlandı; süresi bittiği ISO haftasına yazılır.</summary>
public sealed class StudentStudySessionProjectionHandler : ReportingProjectionHandler<StudySessionPayload>
{
    public StudentStudySessionProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Study"
            && integrationEvent.Name == "StudySessionCompletedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        StudySessionPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.StudentId == Guid.Empty)
        {
            return false;
        }

        var projection = await GetOrCreateStudyAsync(payload.StudentId, payload.EndedAtUtc, cancellationToken);
        projection.TrackSession(payload.SessionId, payload.EffectiveMinutes);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M08 → M14: Test çözüldü; çözüldüğü ISO haftasının test sayacına girer.</summary>
public sealed class StudentStudyTestProjectionHandler : ReportingProjectionHandler<TestResultPayload>
{
    public StudentStudyTestProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Study"
            && integrationEvent.Name == "TestResultRecordedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        TestResultPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.StudentId == Guid.Empty)
        {
            return false;
        }

        var projection = await GetOrCreateStudyAsync(payload.StudentId, payload.TakenOnUtc, cancellationToken);
        projection.TrackTest(payload.TestResultId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

// --- M14-3: öğrenci aylık performans ------------------------------------------------------------

/// <summary>M08 → M14: Test neti, dersin o ayki performans satırına net oranı olarak girer.</summary>
public sealed class StudentPerformanceTestProjectionHandler : ReportingProjectionHandler<TestResultPayload>
{
    public StudentPerformanceTestProjectionHandler(ReportingDbContext dbContext, IClock clock, ILocalCalendar calendar)
        : base(dbContext, clock, calendar)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Study"
            && integrationEvent.Name == "TestResultRecordedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        TestResultPayload payload, IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (payload.StudentId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Subject) || payload.TotalQuestions <= 0)
        {
            return false;
        }

        var projection = await GetOrCreatePerformanceAsync(
            payload.StudentId, payload.Subject, payload.TakenOnUtc, cancellationToken);
        projection.TrackTestNet(payload.TestResultId, payload.Net / payload.TotalQuestions);
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
            return false;
        }

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

public sealed record MonthlyPaymentUpdatedPayload(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    decimal PreviousCollectedAmount,
    decimal CurrentCollectedAmount,
    DateTime UpdatedOnUtc);

public sealed record StudySessionPayload(
    Guid SessionId,
    Guid StudentId,
    int EffectiveMinutes,
    DateTime EndedAtUtc);

public sealed record TestResultPayload(
    Guid TestResultId,
    Guid StudentId,
    string Subject,
    int TotalQuestions,
    decimal Net,
    DateTime TakenOnUtc);

public sealed record TopicMasteryPayload(
    Guid StudentId,
    string Subject,
    string Topic,
    decimal MasteryScore,
    DateTime EvaluatedOnUtc);
