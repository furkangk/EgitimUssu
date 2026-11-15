using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Messaging.Application;

/// <summary>Sayfalama sınırları — istemciden gelen <c>take</c> burada kırpılır.</summary>
public static class MessagingPaging
{
    /// <summary>İstemci <c>take</c> vermezse kullanılan sayfa boyutu.</summary>
    public const int DefaultTake = 30;

    /// <summary>Tek istekte dönebilecek en fazla satır — sunucuyu tek çağrıyla tüketmeyi engeller.</summary>
    public const int MaxTake = 100;

    /// <summary>Geçersiz/aşırı <c>take</c> değerini güvenli aralığa çeker.</summary>
    public static int Clamp(int take) => take switch
    {
        <= 0 => DefaultTake,
        > MaxTake => MaxTake,
        _ => take
    };
}

/// <summary>Uygulama katmanının ek hata kodları.</summary>
public static class MessagingApplicationErrors
{
    /// <summary>Oturum kimliği çözülemedi.</summary>
    public static readonly Error Forbidden =
        new("shared.forbidden", "Mesajlaşmak için oturum açmalısınız.");

    /// <summary>Karşı taraf rolü tanınmadı ya da öğretmen tarafı iki kez verildi.</summary>
    public static readonly Error InvalidRole =
        new("messaging.invalid_role", "Geçersiz katılımcı rolü.");
}

/// <summary>Gelen kutusu satırı: sohbet + o kullanıcı için okunmamış sayısı.</summary>
/// <param name="Conversation">Sohbet.</param>
/// <param name="UnreadCount">İsteği yapan kullanıcının okumadığı mesaj sayısı.</param>
public sealed record ConversationInboxRow(Conversation Conversation, int UnreadCount);

/// <summary>Messaging modülünün deposu.</summary>
public interface IMessagingRepository
{
    /// <summary>Sohbeti kimliğiyle getirir (izlenen — üzerinde değişiklik yapılabilir).</summary>
    Task<Conversation?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Normalize edilmiş üçlüyle var olan sohbeti arar (get-or-create'in "get" tarafı).</summary>
    Task<Conversation?> FindConversationAsync(
        Guid teacherUserId, Guid counterpartUserId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Yeni sohbeti ekler.</summary>
    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);

    /// <summary>Kullanıcının gelen kutusunu son mesaja göre azalan döner.</summary>
    Task<IReadOnlyList<ConversationInboxRow>> ListInboxAsync(
        Guid userId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Keyset sayfalama: <paramref name="beforeUtc"/>'den eski mesajlar, yeniden eskiye.</summary>
    Task<IReadOnlyList<Message>> ListMessagesAsync(
        Guid conversationId, DateTime? beforeUtc, int take, CancellationToken cancellationToken);

    /// <summary>Alıcının henüz okumadığı mesajlar (okundu işaretleme için, izlenen).</summary>
    Task<IReadOnlyList<Message>> ListUnreadForRecipientAsync(
        Guid conversationId, Guid recipientUserId, CancellationToken cancellationToken, DateTime? upToUtc = null, Guid? upToId = null);

    /// <summary>Mesajı kimliğiyle getirir (izlenen).</summary>
    Task<Message?> GetMessageAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>Yeni mesajı ekler.</summary>
    Task AddMessageAsync(Message message, CancellationToken cancellationToken);

    /// <summary>Değişiklikleri kaydeder.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Sohbetin istemciye giden görünümü.</summary>
/// <param name="Id">Sohbet kimliği.</param>
/// <param name="TeacherUserId">Öğretmen tarafı.</param>
/// <param name="CounterpartUserId">Öğrenci/veli tarafı.</param>
/// <param name="CounterpartRole">Karşı tarafın rolü (<c>Student</c> | <c>Parent</c>).</param>
/// <param name="StudentId">Sohbetin bağlamı olan öğrenci profili.</param>
/// <param name="LastMessageOnUtc">Son hareket anı (sıralama).</param>
/// <param name="LastMessagePreview">Son mesajın önizlemesi.</param>
/// <param name="IsBlocked">Taraflardan biri engellemiş — gönderim kapalı.</param>
/// <param name="BlockedByMe">Engeli isteği yapan taraf koymuş (kaldırma düğmesi buna bakar).</param>
/// <param name="IsReadOnly">
/// P15 T5 / K-28: İlişki (özel ders bağı / veli bağı) bitti — gönderim kapalı, geçmiş okunur. Engelden ayrıdır:
/// katılımcılar kaldıramaz, ilişki yeniden kurulursa kendiliğinden <c>false</c> olur. Saklanmaz, istek anında hesaplanır.
/// </param>
public sealed record ConversationResponse(
    Guid Id,
    Guid TeacherUserId,
    Guid CounterpartUserId,
    string CounterpartRole,
    Guid StudentId,
    DateTime LastMessageOnUtc,
    string? LastMessagePreview,
    bool IsBlocked,
    bool BlockedByMe,
    bool IsReadOnly);

/// <summary>Gelen kutusu satırı — sohbet görünümü + okunmamış sayısı.</summary>
/// <param name="Id">Sohbet kimliği.</param>
/// <param name="CounterpartUserId">Karşı tarafın kullanıcı kimliği (isteği yapana göre).</param>
/// <param name="CounterpartRole">Karşı tarafın rolü.</param>
/// <param name="StudentId">Öğrenci bağlamı.</param>
/// <param name="LastMessageOnUtc">Son hareket anı.</param>
/// <param name="LastMessagePreview">Son mesajın önizlemesi.</param>
/// <param name="UnreadCount">Okunmamış mesaj sayısı.</param>
/// <param name="IsBlocked">Taraflardan biri engellemiş — gönderim kapalı.</param>
/// <param name="IsReadOnly">P15 T5 / K-28: İlişki bitti — gönderim kapalı, geçmiş okunur (bkz. <see cref="ConversationResponse.IsReadOnly"/>).</param>
public sealed record ConversationSummaryResponse(
    Guid Id,
    Guid CounterpartUserId,
    string CounterpartRole,
    Guid StudentId,
    DateTime LastMessageOnUtc,
    string? LastMessagePreview,
    int UnreadCount,
    bool IsBlocked,
    bool IsReadOnly);

/// <summary>Tek mesajın istemciye giden görünümü.</summary>
/// <param name="Id">Mesaj kimliği.</param>
/// <param name="ConversationId">Üst sohbet.</param>
/// <param name="SenderUserId">Gönderen.</param>
/// <param name="Body">İçerik; mesaj silinmişse <c>null</c>.</param>
/// <param name="SentOnUtc">Gönderim anı (keyset imleci).</param>
/// <param name="ReadOnUtc">Okunma anı; okunmadıysa <c>null</c>.</param>
/// <param name="IsDeleted">Yumuşak silindi mi.</param>
public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string? Body,
    DateTime SentOnUtc,
    DateTime? ReadOnUtc,
    bool IsDeleted);

/// <summary>Domain nesnelerini yanıt sözleşmelerine çevirir.</summary>
public static class MessagingResponseFactory
{
    /// <summary>Sohbet yanıtı üretir.</summary>
    /// <param name="conversation">Sohbet.</param>
    /// <param name="viewerUserId">İsteği yapan kullanıcı.</param>
    /// <param name="isReadOnly">İlişki bitti mi (<see cref="IConversationRelationshipPolicy"/>).</param>
    /// <returns>Yanıt.</returns>
    public static ConversationResponse From(Conversation conversation, Guid viewerUserId, bool isReadOnly)
    {
        var blockedByMe = viewerUserId == conversation.TeacherUserId
            ? conversation.IsBlockedByTeacher
            : conversation.IsBlockedByCounterpart;

        return new ConversationResponse(
            conversation.Id,
            conversation.TeacherUserId,
            conversation.CounterpartUserId,
            conversation.CounterpartRole.ToString(),
            conversation.StudentId,
            conversation.LastMessageOnUtc,
            conversation.LastMessagePreview,
            conversation.IsBlockedByTeacher || conversation.IsBlockedByCounterpart,
            blockedByMe,
            isReadOnly);
    }

    /// <summary>Gelen kutusu satırı üretir.</summary>
    /// <param name="row">Sohbet + okunmamış sayısı.</param>
    /// <param name="viewerUserId">İsteği yapan kullanıcı.</param>
    /// <param name="isReadOnly">İlişki bitti mi (<see cref="IConversationRelationshipPolicy"/>).</param>
    /// <returns>Yanıt.</returns>
    public static ConversationSummaryResponse From(ConversationInboxRow row, Guid viewerUserId, bool isReadOnly)
    {
        var conversation = row.Conversation;

        // "Karşı taraf" isteği yapana görelidir: öğretmene öğrenci/veli, öğrenciye öğretmen görünür.
        var counterpartUserId = viewerUserId == conversation.TeacherUserId
            ? conversation.CounterpartUserId
            : conversation.TeacherUserId;
        var counterpartRole = viewerUserId == conversation.TeacherUserId
            ? conversation.CounterpartRole
            : ParticipantRole.Teacher;

        return new ConversationSummaryResponse(
            conversation.Id,
            counterpartUserId,
            counterpartRole.ToString(),
            conversation.StudentId,
            conversation.LastMessageOnUtc,
            conversation.LastMessagePreview,
            row.UnreadCount,
            conversation.IsBlockedByTeacher || conversation.IsBlockedByCounterpart,
            isReadOnly);
    }

    /// <summary>Mesaj yanıtı üretir; silinmiş mesajda gövde <c>null</c> döner.</summary>
    /// <param name="message">Mesaj.</param>
    /// <returns>Yanıt.</returns>
    public static MessageResponse From(Message message)
        => new(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            message.VisibleBody,
            message.SentOnUtc,
            message.ReadOnUtc,
            message.IsDeleted);
}

/// <summary>M16-1: Sohbeti açar; aynı çift + öğrenci bağlamı için varsa mevcudu döner (get-or-create).</summary>
/// <param name="CounterpartUserId">
/// Karşı tarafın kullanıcı kimliği. Öğretmen bir <b>öğrenciye</b> yazıyorsa boş (<c>Guid.Empty</c>)
/// bırakılabilir: istemci öğrencinin kullanıcı kimliğini bilmez, sunucu onu
/// <paramref name="StudentId"/>'den çözer.
/// </param>
/// <param name="CounterpartRole">Karşı tarafın rolü (<c>Teacher</c> | <c>Student</c> | <c>Parent</c>).</param>
/// <param name="StudentId">Sohbetin bağlamı olan öğrenci profili.</param>
public sealed record OpenConversationCommand(Guid CounterpartUserId, string CounterpartRole, Guid StudentId)
    : ICommand<Result<ConversationResponse>>;

/// <summary>M16-1: Gelen kutusu.</summary>
/// <param name="Skip">Atlanacak satır sayısı.</param>
/// <param name="Take">Sayfa boyutu.</param>
public sealed record ListConversationsQuery(int Skip, int Take)
    : IQuery<Result<IReadOnlyList<ConversationSummaryResponse>>>;

/// <summary>M16-1: Sohbetin mesajları — keyset sayfalama.</summary>
/// <param name="ConversationId">Sohbet.</param>
/// <param name="BeforeUtc">İmleç: bu andan <b>eski</b> mesajlar döner; ilk sayfada <c>null</c>.</param>
/// <param name="Take">Sayfa boyutu.</param>
public sealed record ListMessagesQuery(Guid ConversationId, DateTime? BeforeUtc, int Take)
    : IQuery<Result<IReadOnlyList<MessageResponse>>>;

/// <summary>M16-1: Mesaj gönderir.</summary>
/// <param name="ConversationId">Sohbet.</param>
/// <param name="Body">İçerik.</param>
public sealed record SendMessageCommand(Guid ConversationId, string Body) : ICommand<Result<MessageResponse>>;

/// <summary>M16-1: Sohbetteki gelen mesajları okundu işaretler.</summary>
/// <param name="ConversationId">Sohbet.</param>
/// <param name="UpToMessageId">İstemcinin gördüğü son mesaj.</param>
public sealed record MarkConversationReadCommand(Guid ConversationId, Guid? UpToMessageId = null) : ICommand<Result>;

/// <summary>M16-1: Sohbeti engeller veya engeli kaldırır.</summary>
/// <param name="ConversationId">Sohbet.</param>
/// <param name="Blocked">true → engelle, false → engeli kaldır.</param>
public sealed record SetConversationBlockCommand(Guid ConversationId, bool Blocked)
    : ICommand<Result<ConversationResponse>>;

/// <summary>M16-1: Mesajı yumuşak siler (gönderen veya Admin).</summary>
/// <param name="MessageId">Mesaj.</param>
public sealed record DeleteMessageCommand(Guid MessageId) : ICommand<Result>;

/// <summary>
/// Sohbet açar. İzinli çift kuralı <see cref="Conversation.Create"/>'de, <b>ilişki zorunluluğu</b>
/// <see cref="IConversationRelationshipPolicy"/>'de denetlenir — ikisi ayrı sorulardır: "bu roller
/// konuşabilir mi" ve "bu iki kişi birbirini tanıyor mu".
/// </summary>
public sealed class OpenConversationCommandHandler
    : ICommandHandler<OpenConversationCommand, Result<ConversationResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;
    private readonly IConversationRelationshipPolicy _relationshipPolicy;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    /// <param name="relationshipPolicy">İlişki kapısı.</param>
    /// <param name="clock">Saat.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    public OpenConversationCommandHandler(
        ICurrentUser currentUser,
        IMessagingRepository repository,
        IConversationRelationshipPolicy relationshipPolicy,
        IClock clock,
        IIdGenerator idGenerator)
    {
        _currentUser = currentUser;
        _repository = repository;
        _relationshipPolicy = relationshipPolicy;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Var olan ya da yeni açılan sohbet.</returns>
    public async Task<Result<ConversationResponse>> Handle(
        OpenConversationCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var callerUserId))
        {
            return Result<ConversationResponse>.Failure(MessagingApplicationErrors.Forbidden);
        }

        if (!Enum.TryParse<ParticipantRole>(command.CounterpartRole, ignoreCase: true, out var counterpartRole))
        {
            return Result<ConversationResponse>.Failure(MessagingApplicationErrors.InvalidRole);
        }

        var callerRole = MessagingRoles.Resolve(_currentUser.Roles);
        if (callerRole is null)
        {
            return Result<ConversationResponse>.Failure(MessagingApplicationErrors.InvalidRole);
        }

        var counterpartUserId = command.CounterpartUserId;
        if (counterpartUserId == Guid.Empty && counterpartRole == ParticipantRole.Student)
        {
            // Öğrenci listesi yalnız profil kimliği taşır; karşı tarafı burada çözeriz.
            // Çözülemezse (öğrenci hesabını henüz devralmadı) sohbet açılamaz.
            var resolved = await _relationshipPolicy.ResolveStudentUserIdAsync(
                command.StudentId, cancellationToken);
            if (resolved is null)
            {
                return Result<ConversationResponse>.Failure(MessagingErrors.RelationshipRequired);
            }

            counterpartUserId = resolved.Value;
        }

        // Domain önce çalışır: çift yasaksa ilişkiyi sormaya gerek yok ve normalize edilmiş
        // (öğretmen, karşı taraf) ikilisini yalnız domain üretebilir.
        var created = Conversation.Create(
            _idGenerator.New(),
            callerRole.Value,
            callerUserId,
            counterpartRole,
            counterpartUserId,
            command.StudentId,
            _clock.UtcNow);

        if (created.IsFailure)
        {
            return Result<ConversationResponse>.Failure(created.Error);
        }

        var candidate = created.Value!;

        var existing = await _repository.FindConversationAsync(
            candidate.TeacherUserId, candidate.CounterpartUserId, candidate.StudentId, cancellationToken);
        if (existing is not null)
        {
            // K-28: ilişkisi biten sohbet 409 değil, salt-okunur döner — kullanıcı kendi geçmişini görebilmeli.
            // Arayan normalize edilmiş üçlünün bir tarafıdır; başkasının sohbetine buradan ulaşılamaz.
            var isActive = await _relationshipPolicy.IsActiveAsync(existing, cancellationToken);
            return Result<ConversationResponse>.Success(
                MessagingResponseFactory.From(existing, callerUserId, isReadOnly: !isActive));
        }

        var allowed = await _relationshipPolicy.EnsureAllowedAsync(
            candidate.TeacherUserId,
            candidate.CounterpartUserId,
            candidate.CounterpartRole,
            candidate.StudentId,
            cancellationToken);
        if (allowed.IsFailure)
        {
            return Result<ConversationResponse>.Failure(allowed.Error);
        }

        await _repository.AddConversationAsync(candidate, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ConversationResponse>.Success(
            MessagingResponseFactory.From(candidate, callerUserId, isReadOnly: false));
    }
}

/// <summary>Kullanıcının gelen kutusunu döner.</summary>
public sealed class ListConversationsQueryHandler
    : IQueryHandler<ListConversationsQuery, Result<IReadOnlyList<ConversationSummaryResponse>>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;
    private readonly IConversationRelationshipPolicy _relationshipPolicy;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    /// <param name="relationshipPolicy">İlişki kapısı (salt-okunur işareti için).</param>
    public ListConversationsQueryHandler(
        ICurrentUser currentUser, IMessagingRepository repository, IConversationRelationshipPolicy relationshipPolicy)
    {
        _currentUser = currentUser;
        _repository = repository;
        _relationshipPolicy = relationshipPolicy;
    }

    /// <summary>Sorguyu çalıştırır.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Gelen kutusu satırları.</returns>
    public async Task<Result<IReadOnlyList<ConversationSummaryResponse>>> Handle(
        ListConversationsQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<IReadOnlyList<ConversationSummaryResponse>>.Failure(
                MessagingApplicationErrors.Forbidden);
        }

        var rows = await _repository.ListInboxAsync(
            userId, Math.Max(0, query.Skip), MessagingPaging.Clamp(query.Take), cancellationToken);

        // Her satırın ilişkisi kaynaktan sorulur (saklanmaz, #191); aynı soru sayfa içinde bir kez sorulur.
        var active = await _relationshipPolicy.GetActiveStatesAsync(
            rows.Select(row => row.Conversation).ToArray(), cancellationToken);

        return Result<IReadOnlyList<ConversationSummaryResponse>>.Success(
            rows.Select(row => MessagingResponseFactory.From(row, userId, isReadOnly: !active[row.Conversation.Id]))
                .ToArray());
    }
}

/// <summary>Sohbetin mesajlarını keyset sayfalamayla döner.</summary>
public sealed class ListMessagesQueryHandler
    : IQueryHandler<ListMessagesQuery, Result<IReadOnlyList<MessageResponse>>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    public ListMessagesQueryHandler(ICurrentUser currentUser, IMessagingRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    /// <summary>Sorguyu çalıştırır.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Mesaj sayfası (yeniden eskiye).</returns>
    public async Task<Result<IReadOnlyList<MessageResponse>>> Handle(
        ListMessagesQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<IReadOnlyList<MessageResponse>>.Failure(MessagingApplicationErrors.Forbidden);
        }

        var conversation = await _repository.GetConversationAsync(query.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<IReadOnlyList<MessageResponse>>.Failure(MessagingErrors.ConversationNotFound);
        }

        if (!conversation.HasParticipant(userId))
        {
            return Result<IReadOnlyList<MessageResponse>>.Failure(MessagingErrors.NotParticipant);
        }

        var messages = await _repository.ListMessagesAsync(
            query.ConversationId, query.BeforeUtc, MessagingPaging.Clamp(query.Take), cancellationToken);

        return Result<IReadOnlyList<MessageResponse>>.Success(
            messages.Select(MessagingResponseFactory.From).ToArray());
    }
}

/// <summary>
/// Mesaj gönderir. Katılımcılık ve engel kontrolü domainde; <b>ilişkinin hâlâ yaşadığı</b> (P15 T5 / K-28)
/// <see cref="IConversationRelationshipPolicy"/>'de denetlenir — ilişki bitince <c>messaging.relationship_ended</c>.
/// </summary>
public sealed class SendMessageCommandHandler
    : ICommandHandler<SendMessageCommand, Result<MessageResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;
    private readonly IConversationRelationshipPolicy _relationshipPolicy;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    /// <param name="relationshipPolicy">İlişki kapısı (K-28: ilişki bitince gönderim kapanır).</param>
    /// <param name="clock">Saat.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    public SendMessageCommandHandler(
        ICurrentUser currentUser,
        IMessagingRepository repository,
        IConversationRelationshipPolicy relationshipPolicy,
        IClock clock,
        IIdGenerator idGenerator)
    {
        _currentUser = currentUser;
        _repository = repository;
        _relationshipPolicy = relationshipPolicy;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Gönderilen mesaj.</returns>
    public async Task<Result<MessageResponse>> Handle(
        SendMessageCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<MessageResponse>.Failure(MessagingApplicationErrors.Forbidden);
        }

        var conversation = await _repository.GetConversationAsync(command.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<MessageResponse>.Failure(MessagingErrors.ConversationNotFound);
        }

        // Sıra bilinçli: önce katılımcılık (yabancı ilişkinin durumunu öğrenmez, diğer modüllere soru da gitmez),
        // sonra ilişki (K-28 — açılışta sorulan kapı her gönderimde kaynaktan yeniden sorulur), en son domain
        // (engel, gövde kuralları). İlişki dış durum olduğu için domainde değil, açılıştaki gibi burada denetlenir.
        if (!conversation.HasParticipant(userId))
        {
            return Result<MessageResponse>.Failure(MessagingErrors.NotParticipant);
        }

        if (!await _relationshipPolicy.IsActiveAsync(conversation, cancellationToken))
        {
            return Result<MessageResponse>.Failure(MessagingErrors.RelationshipEnded);
        }

        var sent = conversation.SendMessage(_idGenerator.New(), userId, command.Body, _clock.UtcNow);
        if (sent.IsFailure)
        {
            return Result<MessageResponse>.Failure(sent.Error);
        }

        await _repository.AddMessageAsync(sent.Value!, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<MessageResponse>.Success(MessagingResponseFactory.From(sent.Value!));
    }
}

/// <summary>Sohbetteki gelen mesajları okundu işaretler (idempotent).</summary>
public sealed class MarkConversationReadCommandHandler
    : ICommandHandler<MarkConversationReadCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    /// <param name="clock">Saat.</param>
    public MarkConversationReadCommandHandler(
        ICurrentUser currentUser, IMessagingRepository repository, IClock clock)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Sonuç.</returns>
    public async Task<Result> Handle(MarkConversationReadCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(MessagingApplicationErrors.Forbidden);
        }

        var conversation = await _repository.GetConversationAsync(command.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result.Failure(MessagingErrors.ConversationNotFound);
        }

        if (!conversation.HasParticipant(userId))
        {
            return Result.Failure(MessagingErrors.NotParticipant);
        }

        if (command.UpToMessageId is null) return Result.Success();
        var anchor = await _repository.GetMessageAsync(command.UpToMessageId.Value, cancellationToken);
        if (anchor is null || anchor.ConversationId != conversation.Id)
            return Result.Failure(MessagingErrors.MessageNotFound);
        var unread = await _repository.ListUnreadForRecipientAsync(
            command.ConversationId, userId, cancellationToken, anchor.SentOnUtc, anchor.Id);

        foreach (var message in unread)
        {
            message.MarkRead(_clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Sohbeti engeller / engeli kaldırır.</summary>
public sealed class SetConversationBlockCommandHandler
    : ICommandHandler<SetConversationBlockCommand, Result<ConversationResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;
    private readonly IConversationRelationshipPolicy _relationshipPolicy;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    /// <param name="relationshipPolicy">İlişki kapısı (yanıttaki salt-okunur işareti için).</param>
    /// <param name="clock">Saat.</param>
    public SetConversationBlockCommandHandler(
        ICurrentUser currentUser,
        IMessagingRepository repository,
        IConversationRelationshipPolicy relationshipPolicy,
        IClock clock)
    {
        _currentUser = currentUser;
        _repository = repository;
        _relationshipPolicy = relationshipPolicy;
        _clock = clock;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncel sohbet.</returns>
    public async Task<Result<ConversationResponse>> Handle(
        SetConversationBlockCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<ConversationResponse>.Failure(MessagingApplicationErrors.Forbidden);
        }

        var conversation = await _repository.GetConversationAsync(command.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<ConversationResponse>.Failure(MessagingErrors.ConversationNotFound);
        }

        var changed = command.Blocked
            ? conversation.Block(userId, _clock.UtcNow)
            : conversation.Unblock(userId, _clock.UtcNow);

        if (changed.IsFailure)
        {
            return Result<ConversationResponse>.Failure(changed.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        var isActive = await _relationshipPolicy.IsActiveAsync(conversation, cancellationToken);
        return Result<ConversationResponse>.Success(
            MessagingResponseFactory.From(conversation, userId, isReadOnly: !isActive));
    }
}

/// <summary>
/// Mesajı yumuşak siler. Yalnız <b>gönderen</b> ya da Admin silebilir: karşı tarafın senin mesajını
/// silebilmesi, sohbet geçmişini karşı tarafın kontrolüne verirdi.
/// </summary>
public sealed class DeleteMessageCommandHandler : ICommandHandler<DeleteMessageCommand, Result>
{
    private readonly ICurrentUser _currentUser;
    private readonly IMessagingRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Depo.</param>
    /// <param name="clock">Saat.</param>
    public DeleteMessageCommandHandler(
        ICurrentUser currentUser, IMessagingRepository repository, IClock clock)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu çalıştırır.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Sonuç.</returns>
    public async Task<Result> Handle(DeleteMessageCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(MessagingApplicationErrors.Forbidden);
        }

        var message = await _repository.GetMessageAsync(command.MessageId, cancellationToken);
        if (message is null)
        {
            return Result.Failure(MessagingErrors.MessageNotFound);
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        if (!isAdmin && message.SenderUserId != userId)
        {
            return Result.Failure(MessagingErrors.NotParticipant);
        }

        message.SoftDelete(_clock.UtcNow);
        var conversation = await _repository.GetConversationAsync(message.ConversationId, cancellationToken);
        conversation?.HideDeletedMessagePreview(message);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
