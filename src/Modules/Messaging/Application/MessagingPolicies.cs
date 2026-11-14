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

    /// <summary>
    /// Öğrenci profiline bağlı kullanıcı kimliği. İstemci bunu <b>bilmez</b> — öğrenci listesi
    /// yalnız profil kimliği taşır — bu yüzden "öğretmen öğrenciye yazıyor" akışında karşı tarafı
    /// sunucu çözer. Bağlı kullanıcı yoksa (öğrenci hesabını henüz devralmadı) <c>null</c> döner.
    /// </summary>
    /// <param name="studentId">Öğrenci profili kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kullanıcı kimliği ya da <c>null</c>.</returns>
    Task<Guid?> ResolveStudentUserIdAsync(Guid studentId, CancellationToken cancellationToken);

    /// <summary>
    /// P15 T5 / K-28: Var olan sohbetlerin ilişkisi <b>hâlâ</b> yaşıyor mu? Sohbet açma kapısıyla aynı yüklem;
    /// ilişki bitmişse gönderim kapanır, sohbet salt-okunur görünür. Aynı (öğretmen, öğrenci), öğrenci sahipliği ve
    /// (veli, öğrenci) sorusu çağrı içinde bir kez sorulur — gelen kutusu sayfası N kez aynı şeyi sormasın.
    /// </summary>
    /// <param name="conversations">Değerlendirilecek sohbetler.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Sohbet kimliği → ilişki yaşıyor mu.</returns>
    Task<IReadOnlyDictionary<Guid, bool>> GetActiveStatesAsync(
        IReadOnlyCollection<Conversation> conversations, CancellationToken cancellationToken);

    /// <summary>Tek sohbet için <see cref="GetActiveStatesAsync"/>.</summary>
    /// <param name="conversation">Sohbet.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İlişki yaşıyorsa <c>true</c>.</returns>
    async Task<bool> IsActiveAsync(Conversation conversation, CancellationToken cancellationToken)
        => (await GetActiveStatesAsync([conversation], cancellationToken))[conversation.Id];
}

/// <summary>
/// İlişki kapısı. Bilgi Messaging'in tablolarında <b>durmaz</b>: öğretmen↔öğrenci kapsamı Students'ın,
/// veli↔öğrenci onaylı bağı Parents'ın gerçeğidir ve <c>Shared/Contracts</c> üzerinden sorulur.
/// Yerel bir kopya tutulsaydı bağ koptuğunda mesajlaşma açık kalırdı (#191) — bu yüzden ilişki hem sohbet
/// açılırken hem de her gönderimde/listelemede yeniden sorulur.
/// </summary>
public sealed class ConversationRelationshipPolicy : IConversationRelationshipPolicy
{
    private readonly ITeacherStudentAccess _access;
    private readonly IStudentDirectory _students;
    private readonly IParentAccessDirectory _parents;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="access">Öğretmen–öğrenci erişim kapsamı (K-13).</param>
    /// <param name="students">Öğrenci dizini (profil sahibi).</param>
    /// <param name="parents">Veli erişim dizini.</param>
    public ConversationRelationshipPolicy(
        ITeacherStudentAccess access, IStudentDirectory students, IParentAccessDirectory parents)
    {
        _access = access;
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
        => await new RelationshipLookup(this).IsAliveAsync(
                teacherUserId, counterpartUserId, counterpartRole, studentId, cancellationToken)
            ? Result.Success()
            : Result.Failure(MessagingErrors.RelationshipRequired);

    /// <inheritdoc />
    public Task<Guid?> ResolveStudentUserIdAsync(Guid studentId, CancellationToken cancellationToken)
        => _students.GetOwnerUserIdAsync(studentId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, bool>> GetActiveStatesAsync(
        IReadOnlyCollection<Conversation> conversations, CancellationToken cancellationToken)
    {
        var lookup = new RelationshipLookup(this);
        var states = new Dictionary<Guid, bool>(conversations.Count);

        // Sırayla: dizinler scoped DbContext kullanır, paralel sorgu (Task.WhenAll) aynı bağlamda patlar.
        foreach (var conversation in conversations)
        {
            states[conversation.Id] = await lookup.IsAliveAsync(
                conversation.TeacherUserId,
                conversation.CounterpartUserId,
                conversation.CounterpartRole,
                conversation.StudentId,
                cancellationToken);
        }

        return states;
    }

    /// <summary>
    /// Tek çağrılık önbellekli yüklem. Önbellek çağrının ömrüyle sınırlıdır: sonraki istek her şeyi kaynaktan
    /// yeniden sorar, böylece bağ koptuğu anda gönderim kapanır, yeniden kurulduğu anda açılır.
    /// </summary>
    private sealed class RelationshipLookup(ConversationRelationshipPolicy policy)
    {
        private readonly Dictionary<(Guid TeacherUserId, Guid StudentId), bool> _privateScopes = [];
        private readonly Dictionary<Guid, Guid?> _owners = [];
        private readonly Dictionary<(Guid ParentUserId, Guid StudentId), bool> _approvedParents = [];

        public async Task<bool> IsAliveAsync(
            Guid teacherUserId,
            Guid counterpartUserId,
            ParticipantRole counterpartRole,
            Guid studentId,
            CancellationToken cancellationToken)
        {
            // Her iki çift de öğretmenin o öğrenciyle yaşayan bir ÖZEL DERS bağı olmasını gerektirir.
            // `== Private`, `!= None` değil: sınıf bağı (P22) birebir mesaj yetkisi vermez (K-17).
            if (!_privateScopes.TryGetValue((teacherUserId, studentId), out var isPrivate))
            {
                isPrivate = await policy._access.GetScopeAsync(teacherUserId, studentId, cancellationToken)
                    == TeacherAccessScope.Private;
                _privateScopes[(teacherUserId, studentId)] = isPrivate;
            }

            if (!isPrivate)
            {
                return false;
            }

            switch (counterpartRole)
            {
                // Karşı taraf öğrenciyse, o kullanıcı gerçekten bu öğrenci profilinin sahibi olmalı:
                // aksi hâlde geçerli bir studentId ile başka birinin kullanıcı kimliği eşleştirilebilirdi.
                case ParticipantRole.Student:
                    if (!_owners.TryGetValue(studentId, out var owner))
                    {
                        owner = await policy._students.GetOwnerUserIdAsync(studentId, cancellationToken);
                        _owners[studentId] = owner;
                    }

                    return owner == counterpartUserId;

                case ParticipantRole.Parent:
                    if (!_approvedParents.TryGetValue((counterpartUserId, studentId), out var approved))
                    {
                        approved = await policy._parents.IsApprovedParentOfStudentAsync(
                            counterpartUserId, studentId, cancellationToken);
                        _approvedParents[(counterpartUserId, studentId)] = approved;
                    }

                    return approved;

                default:
                    return false;
            }
        }
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
