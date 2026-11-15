using System.Text.Json;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Scheduling.Infrastructure;

/// <summary>K-21: Tamamlanmanın tek kaynağı LessonSessions; Scheduling yalnız durumunu yansıtır.</summary>
public sealed class LessonSessionCompletedSchedulingHandler : IdempotentIntegrationEventHandler
{
    private readonly ILessonSessionAccessService _sessions;
    private readonly ITeacherScheduleLock _scheduleLock;
    private readonly IIdGenerator _ids;

    public LessonSessionCompletedSchedulingHandler(SchedulingDbContext dbContext, IClock clock,
        ILessonSessionAccessService sessions, ITeacherScheduleLock scheduleLock, IIdGenerator ids)
        : base(dbContext, clock)
    {
        _sessions = sessions;
        _scheduleLock = scheduleLock;
        _ids = ids;
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "LessonSessions" && integrationEvent.Name == "LessonSessionCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        // Var olan olay şemasına alan eklenmez. Orijinal anahtar/saatler oturumun okuma sözleşmesinden gelir.
        var payload = JsonSerializer.Deserialize<CompletionPayload>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null) return false;
        var session = await _sessions.GetByIdAsync(payload.LessonSessionId, cancellationToken);
        if (session is not { IsCompleted: true, LessonScheduleId: { } scheduleId }) return false;
        var db = (SchedulingDbContext)DbContext;

        var result = await _scheduleLock.RunExclusiveAsync(session.TeacherUserId, async token =>
        {
            var lesson = await db.LessonSchedules.SingleOrDefaultAsync(item => item.Id == scheduleId, token);
            // Silinmiş planın gecikmiş olayı yeni bir plan yaratmaz.
            if (lesson is null) return Result.Success();
            if (string.IsNullOrWhiteSpace(lesson.RecurrenceRule))
            {
                lesson.MarkCompleted(session.CompletedOnUtc ?? envelope.OccurredOnUtc);
                return Result.Success();
            }
            // T3 öncesi oturumlar migration ile anahtar aldı; yalnız ad-hoc oturumda anahtar yoktur.
            if (session.OccurrenceStartUtc is not { } key) return Result.Failure(new Error(
                "scheduling.session_occurrence_missing", "Tekrarlı ders oturumunun oluşum anahtarı bulunamadı."));
            var exception = await db.LessonOccurrenceExceptions.SingleOrDefaultAsync(
                item => item.SeriesLessonScheduleId == scheduleId && item.OriginalStartAtUtc == key, token);
            if (exception?.Action == OccurrenceExceptionAction.Completed) return Result.Success();
            var start = exception?.OverrideStartAtUtc ?? session.PlannedStartAtUtc ?? key;
            var end = exception?.OverrideEndAtUtc ?? start.Add(lesson.EndAtUtc - lesson.StartAtUtc);
            if (exception is null)
                db.LessonOccurrenceExceptions.Add(new(_ids.New(), scheduleId, key, OccurrenceExceptionAction.Completed,
                    start, end, null, session.CompletedOnUtc ?? envelope.OccurredOnUtc));
            else
                exception.Overwrite(OccurrenceExceptionAction.Completed, start, end, exception.Note);
            // SaveChanges ve inbox aynı dış transaction'da taban sınıfa aittir.
            return Result.Success();
        }, cancellationToken);
        if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
        return true;
    }

    private sealed record CompletionPayload(Guid LessonSessionId);
}
