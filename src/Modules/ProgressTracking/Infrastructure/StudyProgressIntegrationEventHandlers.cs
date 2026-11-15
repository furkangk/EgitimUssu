using System.Text.Json;
using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

/// <summary>Altı Study olayı aynı güncel kaynağı uzlaştırır; teslim sırası sayaç/trend üretmez.</summary>
public sealed class StudyStatisticsProgressHandler(ProgressTrackingDbContext db, MasteryService mastery,
    IStudyStatisticsReader source, IClock clock) : IdempotentIntegrationEventHandler(db, clock)
{
    public override bool CanHandle(IIntegrationEvent message) => StudyStatisticsEvents.Matches(message);
    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentPayload>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null || payload.StudentId == Guid.Empty) throw new PoisonMessageException("Çalışma öğrenci kimliği geçersiz.");
        await AcquireStudentLockAsync(db, payload.StudentId, cancellationToken);
        await mastery.RebuildStudyAsync(payload.StudentId, await source.ReadAsync(payload.StudentId, cancellationToken), cancellationToken);
        return true;
    }
    internal static async Task AcquireStudentLockAsync(ProgressTrackingDbContext db, Guid studentId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        var key = $"progress:study:{studentId:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }
    private sealed record StudentPayload(Guid StudentId);
}
