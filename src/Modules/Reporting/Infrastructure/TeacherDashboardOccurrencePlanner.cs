using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>Olay tüketimi ve günlük pencere yenilemesi aynı transaction kilidini kullanır.</summary>
public sealed class TeacherDashboardOccurrenceProcessor(
    ReportingDbContext db, ITeacherDashboardLessonSource source, ILocalCalendar calendar, IClock clock)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var attempt = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            await ReconcileAsync(null, ct);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        });
    }

    public static async Task AcquireLockAsync(ReportingDbContext db, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('reporting-dashboard'))", ct);
    }

    // Inbox transaction'ında çağrılır; SaveChanges taban tüketiciye aittir.
    public async Task ReconcileAsync(Guid? lessonScheduleId, CancellationToken ct, Guid? eventId = null)
    {
        await AcquireLockAsync(db, ct);
        var today = calendar.Today();
        var from = calendar.DayRangeUtc(today.AddDays(-1)).StartUtc;
        var to = calendar.DayRangeUtc(today.AddDays(15)).StartUtc;
        var occurrences = await source.ListDashboardOccurrencesAsync(from, to, lessonScheduleId, ct);
        var rows = occurrences.Where(o => o.StartAtUtc >= from && o.StartAtUtc < to && o.TeacherUserId != Guid.Empty).ToArray();
        var teachers = rows.Select(o => o.TeacherUserId).Distinct().ToArray();
        var projections = await db.TeacherDashboardProjections.Include(p => p.Items)
            .Where(p => !lessonScheduleId.HasValue || teachers.Contains(p.TeacherUserId) ||
                p.Items.Any(i => i.Kind == TeacherDashboardItemKind.Lesson && i.ItemId == lessonScheduleId))
            .ToListAsync(ct);
        foreach (var teacher in teachers.Where(t => projections.All(p => p.TeacherUserId != t)))
        {
            var projection = new TeacherDashboardProjection(teacher);
            db.TeacherDashboardProjections.Add(projection);
            projections.Add(projection);
        }
        var byTeacher = rows.ToLookup(o => o.TeacherUserId);
        foreach (var projection in projections)
        {
            var desired = byTeacher[projection.TeacherUserId].Select(o => (o.LessonScheduleId, o.OccurrenceStartUtc)).ToHashSet();
            projection.CancelMissingLessons(lessonScheduleId, desired);
            foreach (var occurrence in byTeacher[projection.TeacherUserId])
                projection.TrackLessonOccurrence(occurrence.LessonScheduleId, occurrence.OccurrenceStartUtc,
                    occurrence.StartAtUtc, occurrence.IsCancelled, fromCurrentSource: true);
            projection.PruneLessons(from, to);
            projection.MarkApplied(eventId ?? projection.LastAppliedEventId, clock.UtcNow);
        }
    }
}

internal sealed class TeacherDashboardOccurrencePlanner(IServiceScopeFactory scopeFactory, ILogger<TeacherDashboardOccurrencePlanner> logger)
    : LeasedBackgroundService(scopeFactory, logger)
{
    private DateOnly? _lastRefreshedDay;
    protected override string JobName => "teacher-dashboard-occurrences";
    protected override TimeSpan PollInterval => TimeSpan.FromMinutes(1);
    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
        => services.GetRequiredService<ReportingDbContext>();
    protected override async Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var today = services.GetRequiredService<ILocalCalendar>().Today();
        if (_lastRefreshedDay == today) return;
        await services.GetRequiredService<TeacherDashboardOccurrenceProcessor>().RunAsync(cancellationToken);
        _lastRefreshedDay = today;
    }
}
