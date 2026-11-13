using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Messaging.Domain;

/// <summary>Sohbetteki bir tarafın rolü. Öğretmen tarafı her sohbette zorunludur.</summary>
public enum ParticipantRole
{
    Teacher = 1,
    Student = 2,
    Parent = 3
}

/// <summary>Messaging modülünün hata kodları (`&lt;modul&gt;.&lt;hata&gt;`).</summary>
public static class MessagingErrors
{
    public static readonly Error PairNotAllowed =
        new("messaging.pair_not_allowed", "Mesajlaşma yalnız öğretmen ile öğrenci veya veli arasında kurulabilir.");

    public static readonly Error SelfConversation =
        new("messaging.self_conversation", "Kullanıcı kendisiyle sohbet başlatamaz.");

    public static readonly Error NotParticipant =
        new("messaging.not_participant", "Bu sohbetin katılımcısı değilsin.");

    public static readonly Error Blocked =
        new("messaging.blocked", "Bu sohbet engellendiği için mesaj gönderilemez.");

    public static readonly Error EmptyBody =
        new("messaging.empty_body", "Mesaj boş olamaz.");

    public static readonly Error BodyTooLong =
        new("messaging.body_too_long", $"Mesaj en fazla {Message.MaxBodyLength} karakter olabilir.");

    public static readonly Error RelationshipRequired =
        new("messaging.relationship_required", "Aranızda aktif bir bağ olmadan sohbet açılamaz.");

    public static readonly Error ConversationNotFound =
        new("messaging.conversation_not_found", "Sohbet bulunamadı.");

    public static readonly Error MessageNotFound =
        new("messaging.message_not_found", "Mesaj bulunamadı.");
}

/// <summary>
/// M16-1: Bir öğretmen ile bir öğrenci/veli arasındaki, <b>bir öğrenci bağlamına</b> bağlı sohbet.
/// <para>
/// Kayıt her zaman <b>normalize</b> edilir: öğretmen tarafı <see cref="TeacherUserId"/>,
/// diğer taraf <see cref="CounterpartUserId"/>'dir. Sohbeti kim başlatırsa başlatsın aynı çift
/// aynı satıra düşer — "aynı çift + aynı öğrenci için tek sohbet" benzersiz indeksi buna dayanır.
/// </para>
/// </summary>
public sealed class Conversation : AggregateRoot<Guid>
{
    /// <summary>Gelen kutusu tek satır gösterir; önizleme bu uzunlukta kırpılır.</summary>
    public const int PreviewMaxLength = 140;

    private Conversation()
    {
    }

    private Conversation(
        Guid id,
        Guid teacherUserId,
        Guid counterpartUserId,
        ParticipantRole counterpartRole,
        Guid studentId,
        DateTime createdOnUtc)
    {
        Id = id;
        TeacherUserId = teacherUserId;
        CounterpartUserId = counterpartUserId;
        CounterpartRole = counterpartRole;
        StudentId = studentId;
        CreatedOnUtc = createdOnUtc;
        LastMessageOnUtc = createdOnUtc;
    }

    /// <summary>Sohbetin öğretmen tarafı (Identity kullanıcısı).</summary>
    public Guid TeacherUserId { get; private set; }

    /// <summary>Karşı taraf — öğrenci <b>veya</b> veli kullanıcısı.</summary>
    public Guid CounterpartUserId { get; private set; }

    public ParticipantRole CounterpartRole { get; private set; }

    /// <summary>
    /// Sohbetin bağlamı olan öğrenci profili. Veli sohbetinde de doludur: "hangi çocuk hakkında
    /// konuşuyoruz" bilgisi olmadan aynı veli-öğretmen çifti iki çocuk için ayrışamazdı.
    /// </summary>
    public Guid StudentId { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    /// <summary>Gelen kutusu sıralaması. Mesaj yokken sohbetin açılış anıdır.</summary>
    public DateTime LastMessageOnUtc { get; private set; }

    public string? LastMessagePreview { get; private set; }

    /// <summary>E-07: Sohbet önizlemesini siler; sohbetin kendisi karşı taraf için kalır.</summary>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData()
    {
        if (LastMessagePreview is null)
        {
            return false;
        }

        LastMessagePreview = null;
        return true;
    }

    public bool IsBlockedByTeacher { get; private set; }

    public bool IsBlockedByCounterpart { get; private set; }

    /// <summary>Son engelleme anı; engel kalkınca <c>null</c>'a döner (destek/moderasyon izi).</summary>
    public DateTime? BlockedOnUtc { get; private set; }

    /// <summary>
    /// İzinli çift kuralını (PRD) <b>domainde</b> zorlayarak sohbeti kurar. Rol çifti
    /// <c>Teacher↔Student</c> veya <c>Teacher↔Parent</c> değilse reddedilir; taraflar
    /// normalize edilerek öğretmen her zaman <see cref="TeacherUserId"/>'ye yazılır.
    /// </summary>
    public static Result<Conversation> Create(
        Guid id,
        ParticipantRole initiatorRole,
        Guid initiatorUserId,
        ParticipantRole counterpartRole,
        Guid counterpartUserId,
        Guid studentId,
        DateTime nowUtc)
    {
        if (initiatorUserId == counterpartUserId)
        {
            return Result<Conversation>.Failure(MessagingErrors.SelfConversation);
        }

        var teacherSideCount = (initiatorRole == ParticipantRole.Teacher ? 1 : 0)
            + (counterpartRole == ParticipantRole.Teacher ? 1 : 0);

        // Tam olarak bir taraf öğretmen olmalı: iki öğretmen de, sıfır öğretmen de yasaktır.
        if (teacherSideCount != 1)
        {
            return Result<Conversation>.Failure(MessagingErrors.PairNotAllowed);
        }

        var teacherUserId = initiatorRole == ParticipantRole.Teacher ? initiatorUserId : counterpartUserId;
        var otherRole = initiatorRole == ParticipantRole.Teacher ? counterpartRole : initiatorRole;
        var otherUserId = initiatorRole == ParticipantRole.Teacher ? counterpartUserId : initiatorUserId;

        return Result<Conversation>.Success(
            new Conversation(id, teacherUserId, otherUserId, otherRole, studentId, nowUtc));
    }

    public bool HasParticipant(Guid userId) => userId == TeacherUserId || userId == CounterpartUserId;

    /// <summary>Verilen gönderene göre karşı taraf; katılımcı değilse <c>null</c>.</summary>
    public Guid? RecipientOf(Guid senderUserId)
    {
        if (senderUserId == TeacherUserId)
        {
            return CounterpartUserId;
        }

        return senderUserId == CounterpartUserId ? TeacherUserId : null;
    }

    /// <summary>
    /// Engel <b>çift yönlüdür</b>: taraflardan biri engellediyse hiçbir taraf gönderemez.
    /// Tek yönlü engel, engelleyen kişinin karşı tarafa yazmayı sürdürmesi anlamına gelirdi.
    /// </summary>
    public bool CanSend(Guid senderUserId)
        => HasParticipant(senderUserId) && !IsBlockedByTeacher && !IsBlockedByCounterpart;

    /// <summary>
    /// Silinen mesajın gelen kutusunda kalan önizlemesini temizler. Daha yeni/farklı bir
    /// mesajın önizlemesi korunur; aynı anda aynı önizlemeyi üreten mesajlarda gizlilik önceliklidir.
    /// </summary>
    public void HideDeletedMessagePreview(Message message)
    {
        if (message.ConversationId == Id && message.IsDeleted
            && message.SentOnUtc == LastMessageOnUtc
            && LastMessagePreview == BuildPreview(message.Body))
        {
            LastMessagePreview = null;
        }
    }

    /// <summary>
    /// Mesajı üretir, gelen kutusu önizlemesini tazeler ve alıcıya <see cref="MessageSentDomainEvent"/>
    /// yayar. Mesaj oluşturma sohbetin üzerindedir: alıcıyı yalnız sohbet bilir.
    /// </summary>
    public Result<Message> SendMessage(Guid messageId, Guid senderUserId, string body, DateTime nowUtc)
    {
        var recipientUserId = RecipientOf(senderUserId);
        if (recipientUserId is null)
        {
            return Result<Message>.Failure(MessagingErrors.NotParticipant);
        }

        if (!CanSend(senderUserId))
        {
            return Result<Message>.Failure(MessagingErrors.Blocked);
        }

        var trimmed = (body ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Result<Message>.Failure(MessagingErrors.EmptyBody);
        }

        if (trimmed.Length > Message.MaxBodyLength)
        {
            return Result<Message>.Failure(MessagingErrors.BodyTooLong);
        }

        var message = new Message(messageId, Id, senderUserId, trimmed, nowUtc);
        var preview = BuildPreview(trimmed);
        LastMessagePreview = preview;
        LastMessageOnUtc = nowUtc;

        Raise(new MessageSentDomainEvent(
            Id, messageId, senderUserId, recipientUserId.Value, preview, nowUtc));

        return Result<Message>.Success(message);
    }

    public Result Block(Guid byUserId, DateTime nowUtc)
    {
        if (!HasParticipant(byUserId))
        {
            return Result.Failure(MessagingErrors.NotParticipant);
        }

        if (byUserId == TeacherUserId)
        {
            IsBlockedByTeacher = true;
        }
        else
        {
            IsBlockedByCounterpart = true;
        }

        BlockedOnUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>Engeli yalnız <b>koyan taraf</b> kaldırabilir; karşı taraf kendi engelini kaldıramaz.</summary>
    public Result Unblock(Guid byUserId, DateTime nowUtc)
    {
        if (!HasParticipant(byUserId))
        {
            return Result.Failure(MessagingErrors.NotParticipant);
        }

        if (byUserId == TeacherUserId)
        {
            IsBlockedByTeacher = false;
        }
        else
        {
            IsBlockedByCounterpart = false;
        }

        if (!IsBlockedByTeacher && !IsBlockedByCounterpart)
        {
            BlockedOnUtc = null;
        }

        return Result.Success();
    }

    private static string BuildPreview(string body)
    {
        var singleLine = body.Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= PreviewMaxLength
            ? singleLine
            : singleLine[..PreviewMaxLength];
    }
}

/// <summary>
/// M16-1: Sohbetteki tek mesaj.
/// <para>
/// <b>Yumuşak silme</b>: <see cref="SoftDelete"/> satırı ve <see cref="Body"/>'yi korur, yalnız
/// <see cref="VisibleBody"/>'yi <c>null</c>'a çevirir. İçerik moderasyon (M18) için saklanır.
/// </para>
/// </summary>
public sealed class Message : AggregateRoot<Guid>
{
    public const int MaxBodyLength = 4000;

    private Message()
    {
    }

    internal Message(Guid id, Guid conversationId, Guid senderUserId, string body, DateTime sentOnUtc)
    {
        Id = id;
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        Body = body;
        SentOnUtc = sentOnUtc;
    }

    public Guid ConversationId { get; private set; }

    public Guid SenderUserId { get; private set; }

    /// <summary>Ham içerik — silinmiş mesajda da durur. İstemciye <see cref="VisibleBody"/> gider.</summary>
    public string Body { get; private set; } = string.Empty;

    public DateTime SentOnUtc { get; private set; }

    public DateTime? ReadOnUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime? DeletedOnUtc { get; private set; }

    /// <summary>API'ye giden içerik: mesaj silinmişse <c>null</c>.</summary>
    public string? VisibleBody => IsDeleted ? null : Body;

    /// <summary>
    /// E-07: Mesaj içeriğini <b>gerçekten</b> siler.
    /// <para>
    /// <c>SoftDelete</c>'ten farkı budur: yumuşak silme <c>Body</c>'yi veritabanında bırakır
    /// (kullanıcı "sildim" der ama veri durur) — silme talebi için bu yeterli değildir.
    /// </para>
    /// </summary>
    /// <param name="nowUtc">Anonimleştirme anı (UTC).</param>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool AnonymizePersonalData(DateTime nowUtc)
    {
        if (Body.Length == 0 && IsDeleted)
        {
            return false;
        }

        Body = string.Empty;
        IsDeleted = true;
        DeletedOnUtc ??= nowUtc;
        return true;
    }

    /// <summary>
    /// Okundu damgası <b>ilk</b> okumada sabitlenir; sonraki çağrılar bir şey değiştirmez.
    /// Silinmiş mesaj okunmuş sayılmaz — gelen kutusu sayacı onu zaten görmez.
    /// </summary>
    public void MarkRead(DateTime nowUtc)
    {
        if (IsDeleted || ReadOnUtc is not null)
        {
            return;
        }

        ReadOnUtc = nowUtc;
    }

    public void SoftDelete(DateTime nowUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedOnUtc = nowUtc;
    }
}

/// <summary>Yeni mesaj yazıldı — Notifications bunu tüketip alıcıya bildirim/push üretir (M09-2).</summary>
public sealed record MessageSentDomainEvent(
    Guid ConversationId,
    Guid MessageId,
    Guid SenderUserId,
    Guid RecipientUserId,
    string Preview,
    DateTime SentOnUtc) : DomainEvent;
