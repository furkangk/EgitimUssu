using System.Text.Json;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>
/// Profil birleşmesinde çocuk satır kümelerini kanonik dönemlere birleştirir. Aynı çocuk anahtarında
/// kanonik değer korunur; benzersiz katkılar kaybolmaz. Anahtarlar izlenen nesnelerde değiştirilmez.
/// SQL güncellemeleri, kaynak silme ve inbox aynı transaction'dadır; PDF dosyası tarihî haliyle kalır.
/// </summary>
public sealed class ReportingStudentMergedHandler(ReportingDbContext db, IClock clock)
    : IdempotentIntegrationEventHandler(db, clock)
{
    public override bool CanHandle(IIntegrationEvent message)
        => message.SourceModule == "Students" && message.Name == "StudentProfilesMergedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null || payload.FromStudentId == Guid.Empty || payload.ToStudentId == Guid.Empty || payload.FromStudentId == payload.ToStudentId)
            throw new PoisonMessageException("Birleşme öğrenci kimlikleri geçersiz.");
        var from = payload.FromStudentId; var to = payload.ToStudentId;
        await TeacherDashboardOccurrenceProcessor.AcquireLockAsync(db, cancellationToken);
        // T5 ile aynı kilit; eşzamanlı birleşmelerde daima aynı sırayla iki profil kilitlenir.
        foreach (var id in new[] { from, to }.Order())
            await StudyStatisticsProjectionHandler.AcquireStudentLockAsync(db, id, cancellationToken);

        var dashboards = await db.TeacherDashboardProjections.Include(x => x.Items)
            .Where(x => x.Items.Any(i => i.Kind == TeacherDashboardItemKind.ActiveStudent && i.ItemId == from))
            .ToArrayAsync(cancellationToken);
        foreach (var dashboard in dashboards)
        {
            dashboard.MergeStudent(from, to);
            dashboard.MarkApplied(envelope.EventId, Clock.UtcNow);
        }
        await db.Set<TeacherMonthlyItem>().Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);
        await db.StudentReports.Where(x => x.StudentId == from)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, to), cancellationToken);

        var weeks = await db.StudentStudyProjections.Include(x => x.Items)
            .Where(x => x.StudentId == from || x.StudentId == to).ToArrayAsync(cancellationToken);
        var targets = weeks.Where(x => x.StudentId == to).ToDictionary(x => (x.Year, x.WeekOfYear));
        foreach (var source in weeks.Where(x => x.StudentId == from))
        {
            if (!targets.TryGetValue((source.Year, source.WeekOfYear), out var target))
            {
                target = new StudentStudyProjection(to, source.Year, source.WeekOfYear);
                targets.Add((source.Year, source.WeekOfYear), target); db.Add(target);
            }
            var keys = target.Items.Select(x => (x.Kind, x.ItemId)).ToHashSet();
            foreach (var item in source.Items.Where(x => !keys.Contains((x.Kind, x.ItemId))))
            {
                if (item.Kind == StudentStudyItemKind.Session) target.TrackSession(item.ItemId, item.Minutes);
                else target.TrackTest(item.ItemId);
            }
            target.MarkApplied(envelope.EventId, Clock.UtcNow);
            db.Remove(source);
        }

        var months = await db.StudentPerformanceProjections.Include(x => x.Items)
            .Where(x => x.StudentId == from || x.StudentId == to).ToArrayAsync(cancellationToken);
        var monthTargets = months.Where(x => x.StudentId == to).ToDictionary(x => (x.Subject, x.Year, x.Month));
        foreach (var source in months.Where(x => x.StudentId == from))
        {
            if (!monthTargets.TryGetValue((source.Subject, source.Year, source.Month), out var target))
            {
                target = new StudentPerformanceProjection(to, source.Subject, source.Year, source.Month);
                monthTargets.Add((source.Subject, source.Year, source.Month), target); db.Add(target);
            }
            var keys = target.Items.Select(x => (x.Kind, x.ItemKey)).ToHashSet();
            foreach (var item in source.Items.Where(x => !keys.Contains((x.Kind, x.ItemKey))))
            {
                if (item.Kind == StudentPerformanceItemKind.Topic) target.TrackTopicMastery(item.ItemKey, item.Score);
                else target.TrackTestNet(Guid.Parse(item.ItemKey), item.Score);
            }
            target.MarkApplied(envelope.EventId, Clock.UtcNow);
            db.Remove(source);
        }
        return true;
    }
}
