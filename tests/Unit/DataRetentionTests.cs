using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 5 / E-07: Saklama süresi temizliği.
/// <para>
/// İki kural: (1) süresi <b>dolmuş</b> teknik kayıtlar silinir — 90 gün sonra hata ayıklama amacı
/// biter (<c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §2.4); (2) <b>işlenmemiş</b> outbox
/// satırına dokunulmaz — o henüz yayınlanmamış iştir, eskimiş olması onu çöp yapmaz, tam tersine
/// bir arıza işaretidir ve silinirse olay <b>sessizce kaybolur</b>.
/// </para>
/// </summary>
public sealed class DataRetentionTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_Delete_Expired_Rows_And_Keep_Unprocessed_Outbox()
    {
        await using var provider = BuildProvider(out var descriptor);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var expired = Now - DataRetentionProcessor.MessageLogRetention - TimeSpan.FromDays(1);
        var fresh = Now - TimeSpan.FromDays(10);

        context.OutboxMessages.AddRange(
            NewOutbox("islenmis-eski", processedOnUtc: expired),
            NewOutbox("islenmis-yeni", processedOnUtc: fresh),
            NewOutbox("islenmemis-eski", processedOnUtc: null, occurredOnUtc: expired));
        context.InboxMessages.AddRange(
            new InboxMessage(Guid.NewGuid(), "HandlerA", "EskiOlay", expired),
            new InboxMessage(Guid.NewGuid(), "HandlerB", "YeniOlay", fresh));
        await context.SaveChangesAsync();

        var summary = await NewProcessor(scope.ServiceProvider, descriptor).RunAsync();

        Assert.Equal(1, summary.OutboxDeleted);
        Assert.Equal(1, summary.InboxDeleted);
        Assert.Equal(2, summary.Total);

        var remainingOutbox = await context.OutboxMessages.Select(message => message.Type).ToArrayAsync();
        Assert.Contains("islenmis-yeni", remainingOutbox);
        // İşlenmemiş satır eski olsa da durmalı: silinirse olay hiç yayınlanmadan kaybolur.
        Assert.Contains("islenmemis-eski", remainingOutbox);
        Assert.DoesNotContain("islenmis-eski", remainingOutbox);
        Assert.Single(await context.InboxMessages.ToArrayAsync());
    }

    /// <summary>Silinecek bir şey yoksa tur sessizce 0 döner (gürültü üretmez).</summary>
    [Fact]
    public async Task Should_Report_Zero_When_Nothing_Expired()
    {
        await using var provider = BuildProvider(out var descriptor);
        using var scope = provider.CreateScope();

        var summary = await NewProcessor(scope.ServiceProvider, descriptor).RunAsync();

        Assert.Equal(0, summary.Total);
    }

    /// <summary>Saklama süresi politikadaki 90 gündür.</summary>
    [Fact]
    public void Retention_Should_Be_Ninety_Days()
        => Assert.Equal(TimeSpan.FromDays(90), DataRetentionProcessor.MessageLogRetention);

    private static ServiceProvider BuildProvider(out ModuleDbContextDescriptor descriptor)
    {
        var databaseName = Guid.NewGuid().ToString();
        descriptor = new ModuleDbContextDescriptor("Identity", IdentityDbContext.SchemaName, typeof(IdentityDbContext));

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventMapper, JsonDomainEventMapper>();
        services.AddDbContext<IdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private static DataRetentionProcessor NewProcessor(
        IServiceProvider services, ModuleDbContextDescriptor descriptor)
        => new([descriptor], services, new FixedClock(Now), NullLogger<DataRetentionProcessor>.Instance);

    private static OutboxMessage NewOutbox(string type, DateTime? processedOnUtc, DateTime? occurredOnUtc = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = "{}",
            OccurredOnUtc = occurredOnUtc ?? processedOnUtc ?? Now,
            ProcessedOnUtc = processedOnUtc,
        };

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
