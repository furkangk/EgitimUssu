using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Projections;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>
/// Öğretmen dashboard projeksiyonunu besleyen handler'ların ortak tabanı: projeksiyon satırını bulur/oluşturur,
/// değişikliği STAGE eder ve <see cref="ProjectionEntity.MarkApplied"/> ile zarfın kimliğini işler.
/// SaveChanges + inbox satırı taban sınıfa (tek transaction) aittir.
/// </summary>
public abstract class TeacherDashboardProjectionHandler<TEvent> : ProjectionHandler<TEvent>
    where TEvent : class
{
    protected TeacherDashboardProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    protected ReportingDbContext ReportingDb => (ReportingDbContext)DbContext;

    /// <summary>Projeksiyonu yükler; yoksa oluşturur. Öğretmensiz (öğrencinin kendi planı) event'lerde null.</summary>
    protected async Task<TeacherDashboardProjection?> GetOrCreateAsync(
        Guid? teacherUserId,
        CancellationToken cancellationToken)
    {
        if (teacherUserId is null || teacherUserId == Guid.Empty)
        {
            return null;
        }

        var projection = await ReportingDb.TeacherDashboardProjections
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.TeacherUserId == teacherUserId, cancellationToken);
        if (projection is null)
        {
            projection = new TeacherDashboardProjection(teacherUserId.Value);
            ReportingDb.TeacherDashboardProjections.Add(projection);
        }

        return projection;
    }

    /// <summary>Var olan projeksiyonu yükler; yoksa null (çıkarma işlemleri satır oluşturmaz).</summary>
    protected async Task<TeacherDashboardProjection?> FindAsync(
        Guid? teacherUserId,
        CancellationToken cancellationToken)
    {
        if (teacherUserId is null || teacherUserId == Guid.Empty)
        {
            return null;
        }

        return await ReportingDb.TeacherDashboardProjections
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.TeacherUserId == teacherUserId, cancellationToken);
    }
}

/// <summary>M04 → M14: Ders planlandı / yeniden planlandı. Aynı ders satırı upsert edilir.</summary>
public sealed class LessonScheduleUpsertProjectionHandler : TeacherDashboardProjectionHandler<LessonSchedulePayload>
{
    public LessonScheduleUpsertProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Scheduling"
            && integrationEvent.Name is "LessonScheduledDomainEvent" or "LessonScheduleRescheduledDomainEvent";

    protected override async Task<bool> ProjectAsync(
        LessonSchedulePayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        var projection = await GetOrCreateAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            // Öğrencinin öğretmensiz bireysel planı — öğretmen panelini ilgilendirmez.
            return false;
        }

        projection.TrackLesson(payload.LessonScheduleId, payload.StartAtUtc);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M04 → M14: Ders iptal edildi; satır düşer.</summary>
public sealed class LessonScheduleCancelledProjectionHandler : TeacherDashboardProjectionHandler<LessonScheduleCancelledPayload>
{
    public LessonScheduleCancelledProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Scheduling"
            && integrationEvent.Name == "LessonScheduleCancelledDomainEvent";

    protected override async Task<bool> ProjectAsync(
        LessonScheduleCancelledPayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        var projection = await FindAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return false;
        }

        projection.UntrackLesson(payload.LessonScheduleId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M06 → M14: Ödev oluşturuldu; açık ödev kümesine girer.</summary>
public sealed class AssignmentOpenedProjectionHandler : TeacherDashboardProjectionHandler<AssignmentPayload>
{
    public AssignmentOpenedProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Assignments"
            && integrationEvent.Name == "AssignmentCreatedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        AssignmentPayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        var projection = await GetOrCreateAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return false;
        }

        projection.TrackOpenAssignment(payload.AssignmentId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>
/// M06 → M14: Ödev tamamlandı ya da onaylandı; açık ödev kümesinden çıkar. İki event de aynı kümeyi
/// kapattığı için tek handler yeter — "varsa sil" olduğundan ikisi peş peşe gelse de sayaç bozulmaz.
/// </summary>
public sealed class AssignmentClosedProjectionHandler : TeacherDashboardProjectionHandler<AssignmentPayload>
{
    public AssignmentClosedProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Assignments"
            && integrationEvent.Name is "AssignmentCompletedDomainEvent" or "AssignmentApprovedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        AssignmentPayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        var projection = await FindAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return false;
        }

        projection.UntrackAssignment(payload.AssignmentId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M07 → M14: Ödeme vadesi geçti; gecikmiş ödeme kümesine girer (tutar + para birimiyle).</summary>
public sealed class PaymentOverdueProjectionHandler : TeacherDashboardProjectionHandler<PaymentBecameOverduePayload>
{
    public PaymentOverdueProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments"
            && integrationEvent.Name == "PaymentBecameOverdueDomainEvent";

    protected override async Task<bool> ProjectAsync(
        PaymentBecameOverduePayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        var projection = await GetOrCreateAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return false;
        }

        projection.TrackOverduePayment(payload.PaymentRecordId, payload.OutstandingAmount, payload.Currency);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M07 → M14: Ödeme kaydı güncellendi; artık Overdue değilse gecikmiş kümesinden çıkar.</summary>
public sealed class PaymentSettledProjectionHandler : TeacherDashboardProjectionHandler<PaymentRecordUpdatedPayload>
{
    /// <summary>Payments.Domain.PaymentStatus.Overdue sayısal karşılığı (modül sınırı nedeniyle enum paylaşılamaz).</summary>
    private const int OverdueStatus = 4;

    public PaymentSettledProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Payments"
            && integrationEvent.Name == "PaymentRecordUpdatedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        PaymentRecordUpdatedPayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        if (payload.CurrentStatus == OverdueStatus)
        {
            // Hâlâ gecikmiş — küme değişmez.
            return false;
        }

        var projection = await FindAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return false;
        }

        projection.UntrackOverduePayment(payload.PaymentRecordId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

/// <summary>M03 → M14: Öğretmen-öğrenci bağı onaylandı; aktif öğrenci kümesine girer.</summary>
public sealed class TeacherStudentLinkProjectionHandler : TeacherDashboardProjectionHandler<TeacherStudentLinkPayload>
{
    public TeacherStudentLinkProjectionHandler(ReportingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "TeacherStudentLinkAcceptedDomainEvent";

    protected override async Task<bool> ProjectAsync(
        TeacherStudentLinkPayload payload,
        IntegrationEvent envelope,
        CancellationToken cancellationToken)
    {
        var projection = await GetOrCreateAsync(payload.TeacherUserId, cancellationToken);
        if (projection is null)
        {
            return false;
        }

        projection.TrackStudent(payload.StudentId);
        projection.MarkApplied(envelope.EventId, Clock.UtcNow);
        return true;
    }
}

// --- Payload sözleşmeleri ---------------------------------------------------------------------
// Kaynak modülün domain event'inin YALNIZ ihtiyaç duyulan alanları. Fazla alanlar JSON'da yok sayılır,
// böylece kaynak event büyüdüğünde bu tarafta değişiklik gerekmez.

public sealed record LessonSchedulePayload(Guid LessonScheduleId, Guid? TeacherUserId, DateTime StartAtUtc);

public sealed record LessonScheduleCancelledPayload(Guid LessonScheduleId, Guid? TeacherUserId);

public sealed record AssignmentPayload(Guid AssignmentId, Guid TeacherUserId);

public sealed record PaymentBecameOverduePayload(
    Guid PaymentRecordId,
    Guid TeacherUserId,
    decimal OutstandingAmount,
    string Currency);

public sealed record PaymentRecordUpdatedPayload(Guid PaymentRecordId, Guid TeacherUserId, int CurrentStatus);

public sealed record TeacherStudentLinkPayload(Guid TeacherUserId, Guid StudentId);
