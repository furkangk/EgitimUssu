using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P10 Task 2: Mesajlaşma uygulama katmanı testlerinin ortak sahteleri.
/// <para>
/// Depo sahtesi <b>gerçek sıralama ve sayfalama semantiğini</b> taşır (bellekte LINQ ile): keyset
/// testinin anlamlı olması için "yeniden eskiye, <c>beforeUtc</c>'den küçük" kuralı burada da geçerli
/// olmalı; yoksa test yalnız handler'ın çağrı yaptığını doğrular, sayfalamanın doğruluğunu değil.
/// </para>
/// </summary>
internal sealed class FakeMessagingRepository : IMessagingRepository
{
    public List<Conversation> Conversations { get; } = [];

    public List<Message> Messages { get; } = [];

    public int SaveCount { get; private set; }

    public Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
        => Task.FromResult(Conversations.FirstOrDefault(row => row.Id == conversationId));

    public Task<Conversation?> FindConversationAsync(
        Guid teacherUserId, Guid counterpartUserId, Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult(Conversations.FirstOrDefault(row =>
            row.TeacherUserId == teacherUserId
            && row.CounterpartUserId == counterpartUserId
            && row.StudentId == studentId));

    public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        Conversations.Add(conversation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationInboxRow>> ListInboxAsync(
        Guid userId, int skip, int take, CancellationToken cancellationToken)
    {
        var rows = Conversations
            .Where(row => row.HasParticipant(userId))
            .OrderByDescending(row => row.LastMessageOnUtc)
            .Skip(skip)
            .Take(take)
            .Select(row => new ConversationInboxRow(
                row,
                Messages.Count(message =>
                    message.ConversationId == row.Id
                    && message.SenderUserId != userId
                    && !message.IsDeleted
                    && message.ReadOnUtc is null)))
            .ToList();

        return Task.FromResult<IReadOnlyList<ConversationInboxRow>>(rows);
    }

    public Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid conversationId, DateTime? beforeUtc, int take, CancellationToken cancellationToken)
    {
        var rows = Messages
            .Where(message => message.ConversationId == conversationId)
            .Where(message => beforeUtc is null || message.SentOnUtc < beforeUtc.Value)
            .OrderByDescending(message => message.SentOnUtc)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<Message>>(rows);
    }

    public Task<IReadOnlyList<Message>> ListUnreadForRecipientAsync(
        Guid conversationId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var rows = Messages
            .Where(message => message.ConversationId == conversationId
                && message.SenderUserId != recipientUserId
                && !message.IsDeleted
                && message.ReadOnUtc is null)
            .ToList();

        return Task.FromResult<IReadOnlyList<Message>>(rows);
    }

    public Task<Message?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken)
        => Task.FromResult(Messages.FirstOrDefault(message => message.Id == messageId));

    public Task AddMessageAsync(Message message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Öğretmen↔öğrenci bağı ve öğrenci sahipliği için sahte dizin.</summary>
internal sealed class FakeMessagingStudentDirectory : IStudentDirectory
{

    public Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    public Dictionary<Guid, Guid?> Owners { get; } = [];

    public HashSet<(Guid TeacherUserId, Guid StudentId)> Links { get; } = [];

    public Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult(Owners.TryGetValue(studentId, out var owner) ? owner : null);

    public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(
        Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult<AgreedLessonRate?>(null);

    public Task<bool> IsLinkedTeacherOfStudentAsync(
        Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult(Links.Contains((teacherUserId, studentId)));

    public Task<string?> GetDisplayNameAsync(Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

/// <summary>Veli↔öğrenci onaylı bağı için sahte dizin.</summary>
internal sealed class FakeMessagingParentAccessDirectory : IParentAccessDirectory
{
    public HashSet<(Guid ParentUserId, Guid StudentId)> Approved { get; } = [];

    public Task<bool> IsApprovedParentOfStudentAsync(
        Guid parentUserId, Guid studentId, CancellationToken cancellationToken)
        => Task.FromResult(Approved.Contains((parentUserId, studentId)));

    public Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(
        Guid parentUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>>(
            Approved.Where(pair => pair.ParentUserId == parentUserId).Select(pair => pair.StudentId).ToArray());
}

internal sealed class FakeMessagingCurrentUser(Guid? userId, params string[] roles) : ICurrentUser
{
    public string? UserId { get; } = userId?.ToString();

    public string? Email => null;

    public IReadOnlyCollection<string> Roles { get; } = roles;

    public bool IsAuthenticated => UserId is not null;
}

internal sealed class MessagingClock(DateTime nowUtc) : IClock
{
    public DateTime UtcNow { get; set; } = nowUtc;
}

internal sealed class MessagingIdGenerator(params Guid[] ids) : IIdGenerator
{
    private readonly Queue<Guid> _ids = new(ids);

    public Guid New() => _ids.Count > 0 ? _ids.Dequeue() : Guid.NewGuid();
}
