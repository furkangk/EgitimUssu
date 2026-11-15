using System.Text.Json;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

public sealed class LessonCancelledPaymentHandler(PaymentsDbContext dbContext, IClock clock,
    ILessonSessionAccessService sessions, IIdGenerator ids) : IdempotentIntegrationEventHandler(dbContext, clock)
{
    public override bool CanHandle(IIntegrationEvent integrationEvent) => integrationEvent.SourceModule == "Scheduling" &&
        integrationEvent.Name is "LessonCancellationAppliedDomainEvent" or "LessonScheduleDeletedDomainEvent" or "LessonOccurrenceCancelledIntegrationEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        if (envelope.Name == "LessonOccurrenceCancelledIntegrationEvent")
        {
            var charge = JsonSerializer.Deserialize<LessonOccurrenceCancelled>(envelope.Payload, IntegrationEventSerialization.Options);
            if (charge is null) throw new PoisonMessageException("İptal olay yükü null olamaz.");
            if (!charge.IsChargeable || charge.ChargeAmount is not > 0m || string.IsNullOrWhiteSpace(charge.Currency)) return true;
            if (await dbContext.PaymentRecords.AnyAsync(p => p.LessonScheduleId == charge.LessonScheduleId &&
                p.OccurrenceStartUtc == charge.OccurrenceStartUtc && p.ItemType == BillingItemType.LateCancellation, cancellationToken)) return true;
            var record = new PaymentRecord(ids.New(), charge.TeacherUserId, charge.StudentId, null,
                BillingItemType.LateCancellation, "Geç iptal ücreti", charge.Currency, charge.ChargeAmount.Value, 0m,
                Clock.UtcNow.AddDays(7), null, PaymentStatus.Pending, null, null, null, Clock.UtcNow);
            record.LinkCancellationOccurrence(charge.LessonScheduleId, charge.OccurrenceStartUtc);
            dbContext.PaymentRecords.Add(record);
            return true;
        }
        var payload = JsonSerializer.Deserialize<LessonCancellationApplied>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null) throw new PoisonMessageException("Olay yükü null olamaz.");
        if (envelope.Name == "LessonScheduleDeletedDomainEvent") payload = payload with { Scope = "All" };
        // Eski normal ücretler yalnız oturum kimliği taşır; kaynağın salt-okunur sözleşmesinden oluşum çözülür.
        var pending = await dbContext.PaymentRecords.Where(p => p.StudentId == payload.StudentId &&
            p.TeacherUserId == payload.TeacherUserId && p.ItemType == BillingItemType.LessonFee && p.RelatedLessonSessionId != null &&
            p.CollectedAmount == 0m && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Overdue)).ToArrayAsync(cancellationToken);
        foreach (var record in pending)
        {
            var session = await sessions.GetByIdAsync(record.RelatedLessonSessionId!.Value, cancellationToken);
            if (session?.LessonScheduleId == payload.LessonScheduleId && session.OccurrenceStartUtc is { } key && payload.Includes(key))
                record.CancelOutstanding(Clock.UtcNow);
        }
        return true;
    }
}
