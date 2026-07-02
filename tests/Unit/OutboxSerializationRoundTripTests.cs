using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// K3 regresyon kapısı: Outbox yazım tarafı (ModuleDbContext + JsonDomainEventMapper) ile
/// okuma tarafı (OutboxProcessor) aynı serileştirme sözleşmesini kullanmalıdır.
/// Ayrışırsa (ör. okumada varsayılan/PascalCase) event alanları null deserialize olur,
/// hiçbir handler eşleşmez ama mesaj yine "processed" işaretlenir → sessiz veri kaybı.
/// </summary>
public sealed class OutboxSerializationRoundTripTests
{
    private sealed record SampleDomainEvent(Guid AggregateId, string Title, int Attempt) : DomainEvent;

    [Fact]
    public void IntegrationEvent_ShouldSurvive_WriteThenReadRoundTrip()
    {
        // ARRANGE — yazım tarafını birebir taklit et: domain event → mapper → IntegrationEvent
        var mapper = new JsonDomainEventMapper();
        var domainEvent = new SampleDomainEvent(Guid.NewGuid(), "12 Mayis dersi", 3);

        var integrationEvent = (IntegrationEvent)mapper.Map("Scheduling", domainEvent).Single();

        // ModuleDbContext'in outbox satırına yazdığı payload (dış zarf)
        var storedPayload = JsonSerializer.Serialize(
            integrationEvent, integrationEvent.GetType(), IntegrationEventSerialization.Options);

        // ACT — OutboxProcessor'ın okuma tarafı
        var read = JsonSerializer.Deserialize<IntegrationEvent>(
            storedPayload, IntegrationEventSerialization.Options);

        // ASSERT — alanlar korunmalı (eski opsiyonsuz deserialize'da hepsi null olurdu)
        Assert.NotNull(read);
        Assert.Equal(integrationEvent.EventId, read!.EventId);
        Assert.Equal(nameof(SampleDomainEvent), read.Name);
        Assert.Equal("Scheduling", read.SourceModule);
        Assert.False(string.IsNullOrEmpty(read.Payload));
    }

    [Fact]
    public void InnerDomainEventPayload_ShouldRoundTrip_WithSharedOptions()
    {
        var mapper = new JsonDomainEventMapper();
        var original = new SampleDomainEvent(Guid.NewGuid(), "Ödev takibi", 1);

        var integrationEvent = (IntegrationEvent)mapper.Map("Assignments", original).Single();

        var innerRoundTripped = JsonSerializer.Deserialize<SampleDomainEvent>(
            integrationEvent.Payload, IntegrationEventSerialization.Options);

        Assert.NotNull(innerRoundTripped);
        Assert.Equal(original.AggregateId, innerRoundTripped!.AggregateId);
        Assert.Equal(original.Title, innerRoundTripped.Title);
        Assert.Equal(original.Attempt, innerRoundTripped.Attempt);
    }
}
