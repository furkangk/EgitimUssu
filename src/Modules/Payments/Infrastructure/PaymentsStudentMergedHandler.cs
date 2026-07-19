using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Payments.Infrastructure;

/// <summary>
/// İki öğrenci profili birleştirildiğinde (Ö-C claim/merge), Payments modülünün kaynak öğrenciye ait
/// ödeme kayıtlarını kanonik öğrenciye yeniden atar. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// </summary>
internal sealed class PaymentsStudentMergedHandler : IIntegrationEventHandler
{
    private readonly PaymentsDbContext _db;

    public PaymentsStudentMergedHandler(PaymentsDbContext db) => _db = db;

    public bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfilesMergedDomainEvent";

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return;
        }

        await _db.PaymentRecords.Where(x => x.StudentId == payload.FromStudentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StudentId, payload.ToStudentId), cancellationToken);
    }
}
