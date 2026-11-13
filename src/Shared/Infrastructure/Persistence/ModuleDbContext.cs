using System.Diagnostics;
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

    /// <summary>A-07: Arka plan işlerinin çoklu-instance kiraları.</summary>
    public DbSet<JobLease> JobLeases => Set<JobLease>();

    /// <summary>A-04: Gönderilmeyi bekleyen e-postalar (iş yazımıyla aynı transaction'da kuyruğa girer).</summary>
    public DbSet<OutgoingEmail> OutgoingEmails => Set<OutgoingEmail>();

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
            // W3C traceparent sabit 55 karakterdir; alan sınırsız bırakılırsa şema, taşımayacağı
            // bir esnekliği ima eder ve yanlışlıkla başka bir şey yazılmasını kolaylaştırır.
            builder.Property(item => item.TraceParent).HasMaxLength(55);

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

        modelBuilder.Entity<JobLease>(builder =>
        {
            builder.ToTable("job_leases");
            builder.HasKey(item => item.JobName);
            builder.Property(item => item.JobName).HasMaxLength(128);
        });

        modelBuilder.Entity<OutgoingEmail>(builder =>
        {
            builder.ToTable("outgoing_emails");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.ToEmail).HasMaxLength(320).IsRequired();
            builder.Property(item => item.Subject).HasMaxLength(512).IsRequired();
            builder.Property(item => item.HtmlBody).IsRequired();
            builder.Property(item => item.TextBody).IsRequired();

            // Outbox ile aynı gerekçe: kısmi indeks yalnız bekleyen satırları kapsar.
            builder
                .HasIndex(item => new { item.NextAttemptUtc, item.CreatedOnUtc })
                .HasDatabaseName("ix_outgoing_emails_pending")
                .HasFilter("\"SentOnUtc\" IS NULL AND \"DeadLetteredOnUtc\" IS NULL");

            builder.HasIndex(item => item.SentOnUtc);
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
                        OccurredOnUtc = integrationEvent.OccurredOnUtc,
                        // C-04: Bağlamı **burada** yakalamak zorunludur — dispatch dakikalar sonra,
                        // başka bir thread'de koşar ve o an `Activity.Current` çoktan boştur.
                        TraceParent = Activity.Current?.Id
                    });
                }
            }

            aggregate.ClearDomainEvents();
        }
    }
}
