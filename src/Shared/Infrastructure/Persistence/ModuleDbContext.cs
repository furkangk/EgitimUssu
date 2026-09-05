using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Shared.Infrastructure.Persistence;

public abstract class ModuleDbContext : DbContext
{
    private readonly IDomainEventMapper _domainEventMapper;

    protected ModuleDbContext(DbContextOptions options, IDomainEventMapper domainEventMapper)
        : base(options)
    {
        _domainEventMapper = domainEventMapper;
    }

    protected abstract string Schema { get; }

    protected abstract string ModuleName { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ModuleState> ModuleStates => Set<ModuleState>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Module).HasMaxLength(128).IsRequired();
            builder.Property(item => item.Type).HasMaxLength(512).IsRequired();
            builder.Property(item => item.Payload).IsRequired();

            // A-09: Claim sorgusu "ProcessedOnUtc IS NULL AND DeadLetteredOnUtc IS NULL AND
            // (NextAttemptUtc IS NULL OR <= now) ORDER BY OccurredOnUtc" biçimindedir; tek başına
            // ProcessedOnUtc üzerindeki indeks bunu karşılamıyordu. Kısmi indeks yalnız bekleyen
            // satırları kapsar — tablo (işlenmişler biriktikçe) büyüse de indeks küçük kalır.
            builder
                .HasIndex(item => new { item.NextAttemptUtc, item.OccurredOnUtc })
                .HasDatabaseName("ix_outbox_messages_pending")
                .HasFilter("\"ProcessedOnUtc\" IS NULL AND \"DeadLetteredOnUtc\" IS NULL");

            // Retention taraması işlenmiş satırları tarih sırasına göre siler.
            builder.HasIndex(item => item.ProcessedOnUtc);
        });

        modelBuilder.Entity<ModuleState>(builder =>
        {
            builder.ToTable("module_states");
            builder.HasKey(item => item.Id);
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(item => new { item.EventId, item.Handler });
            builder.Property(item => item.Handler).HasMaxLength(256).IsRequired();
            builder.Property(item => item.EventName).HasMaxLength(256).IsRequired();

            // A-09: Retention taraması bu sütuna göre siler.
            builder.HasIndex(item => item.ProcessedOnUtc);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        CaptureOutboxMessages();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CaptureOutboxMessages();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void CaptureOutboxMessages()
    {
        var aggregates = ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IAggregateRoot>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToArray();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                foreach (var integrationEvent in _domainEventMapper.Map(ModuleName, domainEvent))
                {
                    OutboxMessages.Add(new OutboxMessage
                    {
                        Id = integrationEvent.EventId,
                        Module = integrationEvent.SourceModule,
                        Type = integrationEvent.Name,
                        Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), IntegrationEventSerialization.Options),
                        OccurredOnUtc = integrationEvent.OccurredOnUtc
                    });
                }
            }

            aggregate.ClearDomainEvents();
        }
    }
}
