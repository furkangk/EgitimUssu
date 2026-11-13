using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgitimUssu.Modules.Messaging.Infrastructure;

public sealed class MessagingDbContext : ModuleDbContext
{
    public const string SchemaName = "messaging";

    public MessagingDbContext(
        DbContextOptions<MessagingDbContext> options,
        IDomainEventMapper domainEventMapper)
        : base(options, domainEventMapper)
    {
    }

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    protected override string Schema => SchemaName;

    protected override string ModuleName => "Messaging";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingDbContext).Assembly);
    }
}

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CounterpartRole).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.LastMessagePreview).HasMaxLength(Conversation.PreviewMaxLength);

        // "Aynı çift + aynı öğrenci bağlamı için tek sohbet" (get-or-create) kuralını veritabanı da
        // zorlar: uygulama katmanındaki arama, eşzamanlı iki "sohbet aç" isteğinde yarışabilir.
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.CounterpartUserId, entity.StudentId })
            .IsUnique();

        // Gelen kutusu her iki taraftan da "son mesaja göre azalan" sıralanır.
        builder.HasIndex(entity => new { entity.TeacherUserId, entity.LastMessageOnUtc });
        builder.HasIndex(entity => new { entity.CounterpartUserId, entity.LastMessageOnUtc });
    }
}

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Body).HasMaxLength(Message.MaxBodyLength).IsRequired();

        // Keyset sayfalama (yeniden eskiye, `beforeUtc`) bu indekse dayanır.
        builder.HasIndex(entity => new { entity.ConversationId, entity.SentOnUtc });

        // Okunmamış sayacı: sohbet + alıcı-değil-gönderen + ReadOnUtc null taraması.
        builder.HasIndex(entity => new { entity.ConversationId, entity.ReadOnUtc });
    }
}
