namespace EgitimUssu.Shared.Contracts;

// Yeni olay yükleri; eski Scheduled/Cancelled outbox şemaları değiştirilmez.
public sealed record LessonCancellationApplied(
    Guid LessonScheduleId, Guid? TeacherUserId, Guid StudentId, string Scope,
    DateTime? OccurrenceStartUtc, DateTime? EffectiveStartUtc, DateTime CancelledOnUtc)
{
    public bool Includes(DateTime key) => Scope == "All" ||
        (Scope == "Single" ? key == OccurrenceStartUtc : key >= OccurrenceStartUtc);
}

public sealed record LessonOccurrenceCancelled(
    Guid LessonScheduleId, DateTime OccurrenceStartUtc, Guid TeacherUserId, Guid StudentId,
    bool IsChargeable, decimal? ChargeAmount, string? Currency);
