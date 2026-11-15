using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Messaging.Infrastructure;

/// <summary>
/// M16-1: Messaging modülünün deposu.
/// <para>
/// Yazma yolundaki okumalar bilinçli olarak <b>izlenendir</b>: handler aggregate üzerinde bir metot
/// çağırıp <c>SaveChangesAsync</c> diyor; <c>AsNoTracking</c> olsaydı değişiklik sessizce kaybolurdu.
/// Saf okumalar (gelen kutusu, mesaj sayfası) izlemesizdir.
/// </para>
/// </summary>
internal sealed class MessagingRepository : IMessagingRepository
{
    private readonly MessagingDbContext _dbContext;

    public MessagingRepository(MessagingDbContext dbContext) => _dbContext = dbContext;

    public Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
        => _dbContext.Conversations.FirstOrDefaultAsync(row => row.Id == conversationId, cancellationToken);

    public Task<Conversation?> FindConversationAsync(
        Guid teacherUserId, Guid counterpartUserId, Guid studentId, CancellationToken cancellationToken)
        => _dbContext.Conversations.FirstOrDefaultAsync(
            row => row.TeacherUserId == teacherUserId
                && row.CounterpartUserId == counterpartUserId
                && row.StudentId == studentId,
            cancellationToken);

    public async Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
        => await _dbContext.Conversations.AddAsync(conversation, cancellationToken);

    public async Task<IReadOnlyList<ConversationInboxRow>> ListInboxAsync(
        Guid userId, int skip, int take, CancellationToken cancellationToken)
        => await _dbContext.Conversations
            .AsNoTracking()
            .Where(row => row.TeacherUserId == userId || row.CounterpartUserId == userId)
            .OrderByDescending(row => row.LastMessageOnUtc)
            .Skip(skip)
            .Take(take)
            // Okunmamış sayısı alt sorguyla hesaplanır: sohbette denormalize bir sayaç tutmak,
            // iki tarafın sayaçlarını her gönderim/okuma turunda tutarlı tutma yükü getirirdi.
            .Select(row => new ConversationInboxRow(
                row,
                _dbContext.Messages.Count(message =>
                    message.ConversationId == row.Id
                    && message.SenderUserId != userId
                    && !message.IsDeleted
                    && message.ReadOnUtc == null)))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid conversationId, DateTime? beforeUtc, int take, CancellationToken cancellationToken)
        => await _dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .Where(message => beforeUtc == null || message.SentOnUtc < beforeUtc)
            .OrderByDescending(message => message.SentOnUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<Message>> ListUnreadForRecipientAsync(
        Guid conversationId, Guid recipientUserId, CancellationToken cancellationToken, DateTime? upToUtc = null, Guid? upToId = null)
        => await _dbContext.Messages
            .Where(message => message.ConversationId == conversationId
                && message.SenderUserId != recipientUserId
                && !message.IsDeleted
                && message.ReadOnUtc == null
                && (upToUtc == null || message.SentOnUtc < upToUtc || message.Id == upToId))
            .ToArrayAsync(cancellationToken);

    public Task<Message?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken)
        => _dbContext.Messages.FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);

    public async Task AddMessageAsync(Message message, CancellationToken cancellationToken)
        => await _dbContext.Messages.AddAsync(message, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
