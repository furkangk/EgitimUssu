using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Messaging.Application;

/// <summary>Oturum rollerini <see cref="ParticipantRole"/>'e çevirir.</summary>
public static class MessagingRoles
{
    /// <summary>
    /// Kullanıcının mesajlaşmadaki rolünü çözer. Bir hesap birden çok rol taşıyabilir; sıralama
    /// bilinçlidir: <b>öğretmen</b> rolü varsa sohbet öğretmen tarafından açılır, çünkü izinli
    /// çiftlerin ikisinde de öğretmen tarafı zorunludur.
    /// </summary>
    /// <param name="roles">Oturumdaki roller.</param>
    /// <returns>Katılımcı rolü; hiçbiri tanınmıyorsa <c>null</c>.</returns>
    public static ParticipantRole? Resolve(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains("Teacher"))
        {
            return ParticipantRole.Teacher;
        }

        if (roles.Contains("Student"))
        {
            return ParticipantRole.Student;
        }

        return roles.Contains("Parent") ? ParticipantRole.Parent : null;
    }
}

/// <summary>Sohbet açmadan önce tarafların gerçekten bağlı olduğunu doğrular.</summary>
public interface IConversationRelationshipPolicy
{
    /// <summary>Normalize edilmiş sohbet için ilişki kapısını uygular.</summary>
    /// <param name="teacherUserId">Öğretmen tarafı.</param>
    /// <param name="counterpartUserId">Öğrenci/veli tarafı.</param>
    /// <param name="counterpartRole">Karşı tarafın rolü.</param>
    /// <param name="studentId">Öğrenci bağlamı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İzin varsa başarı, yoksa <c>messaging.relationship_required</c>.</returns>
    Task<Result> EnsureAllowedAsync(
        Guid teacherUserId,
        Guid counterpartUserId,
        ParticipantRole counterpartRole,
        Guid studentId,
        CancellationToken cancellationToken);
}

/// <summary>
/// İlişki kapısı. Bilgi Messaging'in tablolarında <b>durmaz</b>: öğretmen↔öğrenci bağı Students'ın,
/// veli↔öğrenci onaylı bağı Parents'ın gerçeğidir ve <c>Shared/Contracts</c> üzerinden sorulur.
/// Yerel bir kopya tutulsaydı bağ koptuğunda mesajlaşma açık kalırdı.
/// </summary>
public sealed class ConversationRelationshipPolicy : IConversationRelationshipPolicy
{
    private readonly IStudentDirectory _students;
    private readonly IParentAccessDirectory _parents;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="students">Öğrenci dizini.</param>
    /// <param name="parents">Veli erişim dizini.</param>
    public ConversationRelationshipPolicy(IStudentDirectory students, IParentAccessDirectory parents)
    {
        _students = students;
        _parents = parents;
    }

    /// <inheritdoc />
    public async Task<Result> EnsureAllowedAsync(
        Guid teacherUserId,
        Guid counterpartUserId,
        ParticipantRole counterpartRole,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        // Her iki çift de öğretmenin o öğrenciyle yaşayan bir bağı olmasını gerektirir.
        if (!await _students.IsLinkedTeacherOfStudentAsync(teacherUserId, studentId, cancellationToken))
        {
            return Result.Failure(MessagingErrors.RelationshipRequired);
        }

        var allowed = counterpartRole switch
        {
            // Karşı taraf öğrenciyse, o kullanıcı gerçekten bu öğrenci profilinin sahibi olmalı:
            // aksi hâlde geçerli bir studentId ile başka birinin kullanıcı kimliği eşleştirilebilirdi.
            ParticipantRole.Student =>
                await _students.GetOwnerUserIdAsync(studentId, cancellationToken) == counterpartUserId,

            ParticipantRole.Parent =>
                await _parents.IsApprovedParentOfStudentAsync(counterpartUserId, studentId, cancellationToken),

            _ => false
        };

        return allowed ? Result.Success() : Result.Failure(MessagingErrors.RelationshipRequired);
    }
}

/// <summary>
/// Mesajlaşma uçlarının tamamı oturum ister; kayıt bazlı yetki (katılımcı mısın, gönderen misin)
/// handler'ların içinde, veriyi görerek yapılır — authorizer o veriye sahip değildir.
/// </summary>
public sealed class AuthenticatedMessagingAuthorizer
    : ICommandAuthorizer<OpenConversationCommand>,
      ICommandAuthorizer<SendMessageCommand>,
      ICommandAuthorizer<MarkConversationReadCommand>,
      ICommandAuthorizer<SetConversationBlockCommand>,
      ICommandAuthorizer<DeleteMessageCommand>,
      IQueryAuthorizer<ListConversationsQuery>,
      IQueryAuthorizer<ListMessagesQuery>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    public AuthenticatedMessagingAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Sohbet açma yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(OpenConversationCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Mesaj gönderme yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(SendMessageCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Okundu işaretleme yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(MarkConversationReadCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Engelleme yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(SetConversationBlockCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Mesaj silme yetkisini denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(DeleteMessageCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Gelen kutusu yetkisini denetler.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(ListConversationsQuery query, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    /// <summary>Mesaj listeleme yetkisini denetler.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(ListMessagesQuery query, CancellationToken cancellationToken)
        => Task.FromResult(Authenticated());

    private Result Authenticated()
        => _currentUser.IsAuthenticated && Guid.TryParse(_currentUser.UserId, out _)
            ? Result.Success()
            : Result.Failure(MessagingApplicationErrors.Forbidden);
}
