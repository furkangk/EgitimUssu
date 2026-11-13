using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Messaging.Infrastructure;

/// <summary>
/// E-07: Silinen kullanıcının gönderdiği mesajların <b>içeriğini</b> siler ve sohbet önizlemelerini
/// temizler.
/// <para>
/// Sohbet satırı ve mesaj iskeleti kalır: karşı taraf da bu sohbetin tarafıdır ve konuşmanın var
/// olduğu bilgisi onun verisidir. Silinen olan <b>içeriktir</b>; karşı tarafta sohbet "silinmiş
/// kullanıcı" olarak görünür.
/// </para>
/// </summary>
public sealed class MessagingPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly MessagingDbContext _dbContext;
    private readonly IClock _clock;

    public MessagingPersonalDataAnonymizer(MessagingDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public string ModuleName => "Messaging";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var messages = await _dbContext.Messages
            .Where(message => message.SenderUserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var conversations = await _dbContext.Conversations
            .Where(conversation => conversation.TeacherUserId == subject.UserId
                || conversation.CounterpartUserId == subject.UserId
                || studentIds.Contains(conversation.StudentId))
            .ToArrayAsync(cancellationToken);

        var now = _clock.UtcNow;
        var affected = messages.Count(message => message.AnonymizePersonalData(now))
            + conversations.Count(conversation => conversation.AnonymizePersonalData());

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
