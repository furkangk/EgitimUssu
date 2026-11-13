using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Messaging.Infrastructure;

/// <summary>
/// P15 T5 / #189: İki öğrenci profili birleştiğinde (Ö-C claim/merge) sohbetleri kanonik profile taşır.
/// <para>
/// Eskiden Messaging bu olayı tüketmiyordu. T3 öğretmen bağını, Parents veli bağını kanonik kimliğe taşıdığı için
/// birleşen (manuel) profildeki sohbetin ilişkisi "bitmiş" görünür ve K-28 kontrolü süren bir yazışmayı salt-okunur
/// yapardı; ayrıca aynı çift kanonik kimlikle ikinci bir sohbet açabilirdi.
/// </para>
/// <list type="bullet">
/// <item>Kanonikte aynı üçlü <b>yoksa</b>: sohbetin <c>StudentId</c>'si yeniden atanır — kimlik değişmez, bildirim
/// rotaları geçerli kalır.</item>
/// <item>Kanonikte aynı üçlü <b>varsa</b> (benzersiz <c>(TeacherUserId, CounterpartUserId, StudentId)</c> indeksi kör
/// taşımayı reddeder ve mesaj outbox'ta sonsuza dek yeniden denenirdi — #187): mesajlar kanonik sohbete taşınır,
/// kanonik sohbet kopyanın özetini emer (<c>Conversation.Absorb</c>), kopya silinir.</item>
/// </list>
/// Doğrudan cross-module DB erişimi yok — yalnız integration event. Replay koruması ortak inbox üzerinden; mesaj
/// taşıması ve sohbet değişiklikleri tabanın açtığı tek transaction'da commit olur.
/// </summary>
internal sealed class MessagingStudentMergedHandler : IdempotentIntegrationEventHandler
{
    public MessagingStudentMergedHandler(MessagingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private MessagingDbContext MessagingDb => (MessagingDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfilesMergedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return false;
        }

        var from = payload.FromStudentId;
        var to = payload.ToStudentId;

        var sources = await MessagingDb.Conversations
            .Where(conversation => conversation.StudentId == from)
            .ToListAsync(cancellationToken);

        foreach (var source in sources)
        {
            var canonical = await MessagingDb.Conversations.FirstOrDefaultAsync(
                conversation => conversation.TeacherUserId == source.TeacherUserId
                    && conversation.CounterpartUserId == source.CounterpartUserId
                    && conversation.StudentId == to,
                cancellationToken);

            if (canonical is null)
            {
                source.MoveToStudent(to);
                continue;
            }

            // Mesajlar önce taşınır (tabanın transaction'ında); sonra kanonik özet güncellenir ve kopya silinir.
            await MessagingDb.Messages
                .Where(message => message.ConversationId == source.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(message => message.ConversationId, canonical.Id),
                    cancellationToken);

            canonical.Absorb(source);
            MessagingDb.Conversations.Remove(source);
        }

        return true;
    }
}
