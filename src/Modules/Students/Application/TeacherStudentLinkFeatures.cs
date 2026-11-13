using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

public interface ITeacherStudentLinkRepository
{
    Task AddAsync(TeacherStudentLink link, CancellationToken cancellationToken);

    Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken cancellationToken);

    Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken);

    Task<TeacherStudentLink?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<int> CountByTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TeacherStudentLink>> ListByTeacherAsync(Guid teacherUserId, bool includeArchived, CancellationToken cancellationToken);

    /// <summary>P15 / N-28: Bir öğrenci profilinin bütün öğretmen bağları (arşivli/bitmiş dahil) — birleştirme için.</summary>
    Task<IReadOnlyCollection<TeacherStudentLink>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken);

    /// <summary>P15 / N-28: Birleşmede düşen bağı siler (SaveChanges ile birlikte uygulanır).</summary>
    void Remove(TeacherStudentLink link);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ArchiveTeacherStudentLinkCommand(Guid TeacherUserId, Guid StudentId, bool Archive) : ICommand<Result>;

public sealed record SetTeacherStudentRateCommand(Guid TeacherUserId, Guid StudentId, decimal AgreedRateAmount, string Currency) : ICommand<Result>;

public sealed class ArchiveTeacherStudentLinkCommandHandler : ICommandHandler<ArchiveTeacherStudentLinkCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IClock _clock;

    public ArchiveTeacherStudentLinkCommandHandler(ITeacherStudentLinkRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(ArchiveTeacherStudentLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByTeacherAndStudentAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        if (command.Archive)
        {
            link.Archive(_clock.UtcNow);
        }
        else
        {
            link.Unarchive(_clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetTeacherStudentRateCommandHandler : ICommandHandler<SetTeacherStudentRateCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IClock _clock;

    public SetTeacherStudentRateCommandHandler(ITeacherStudentLinkRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(SetTeacherStudentRateCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByTeacherAndStudentAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        link.SetRate(command.AgreedRateAmount, command.Currency, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record InviteStudentCommand(Guid TeacherUserId, Guid StudentId, Guid? TargetUserId) : ICommand<Result>;

/// <summary>P15 / N-22: Link kimliğiyle davet kabulü — davet kodu kanıttır (link kimliği tek başına yetmez).</summary>
public sealed record AcceptTeacherStudentLinkCommand(Guid LinkId, Guid AcceptingUserId, string InviteCode) : ICommand<Result>;

/// <summary>P15 / N-22: Link kimliğiyle davet reddi — kabulle aynı kanıtı ister.</summary>
public sealed record RejectTeacherStudentLinkCommand(Guid LinkId, Guid RejectingUserId, string InviteCode) : ICommand<Result>;

public sealed class InviteStudentCommandHandler : ICommandHandler<InviteStudentCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly ISecureCodeGenerator _codes;
    private readonly IClock _clock;

    public InviteStudentCommandHandler(ITeacherStudentLinkRepository repository, ISecureCodeGenerator codes, IClock clock)
    {
        _repository = repository;
        _codes = codes;
        _clock = clock;
    }

    public async Task<Result> Handle(InviteStudentCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByTeacherAndStudentAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        var now = _clock.UtcNow;
        var marked = link.MarkInviteSent(_codes.NewCode(), command.TargetUserId, now.AddDays(InvitePolicy.ExpiryDays), now);
        if (marked.IsFailure)
        {
            return marked;
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// P15 / N-22: Link kimliğiyle davet kabulü. Claim ile <b>aynı çekirdeği</b> kullanır
/// (<see cref="StudentInviteAcceptance"/>): kod, durum, hedef, süre ve kendi davetini kullanma kontrolü;
/// sonra devralma ya da birleştirme. Kodla ilgili her başarısız deneme kullanıcı başına sayılır.
/// </summary>
public sealed class AcceptTeacherStudentLinkCommandHandler : ICommandHandler<AcceptTeacherStudentLinkCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IStudentProfileRepository _profileRepository;
    private readonly IStudentParentInviteRepository _parentInviteRepository;
    private readonly IInviteClaimAttemptLimiter _attempts;
    private readonly IClock _clock;

    public AcceptTeacherStudentLinkCommandHandler(
        ITeacherStudentLinkRepository repository,
        IStudentProfileRepository profileRepository,
        IStudentParentInviteRepository parentInviteRepository,
        IInviteClaimAttemptLimiter attempts,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _parentInviteRepository = parentInviteRepository;
        _attempts = attempts;
        _clock = clock;
    }

    public async Task<Result> Handle(AcceptTeacherStudentLinkCommand command, CancellationToken cancellationToken)
    {
        if (await _attempts.IsBlockedAsync(command.AcceptingUserId, cancellationToken))
        {
            return Result.Failure(StudentInviteAcceptance.TooManyAttempts);
        }

        var link = await _repository.GetByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        var accepted = await StudentInviteAcceptance.AcceptAsync(
            link, command.AcceptingUserId, command.InviteCode, _clock.UtcNow,
            _repository, _profileRepository, _parentInviteRepository, cancellationToken);

        return await StudentInviteAcceptance.FinishAsync(
            accepted, command.AcceptingUserId, _repository, _attempts, cancellationToken);
    }
}

/// <summary>P15 / N-22: Link kimliğiyle davet reddi — yalnız <c>InviteSent</c> bağda ve kodla.</summary>
public sealed class RejectTeacherStudentLinkCommandHandler : ICommandHandler<RejectTeacherStudentLinkCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IInviteClaimAttemptLimiter _attempts;
    private readonly IClock _clock;

    public RejectTeacherStudentLinkCommandHandler(
        ITeacherStudentLinkRepository repository,
        IInviteClaimAttemptLimiter attempts,
        IClock clock)
    {
        _repository = repository;
        _attempts = attempts;
        _clock = clock;
    }

    public async Task<Result> Handle(RejectTeacherStudentLinkCommand command, CancellationToken cancellationToken)
    {
        if (await _attempts.IsBlockedAsync(command.RejectingUserId, cancellationToken))
        {
            return Result.Failure(StudentInviteAcceptance.TooManyAttempts);
        }

        var link = await _repository.GetByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        var rejected = link.Reject(command.RejectingUserId, command.InviteCode, _clock.UtcNow);
        return await StudentInviteAcceptance.FinishAsync(
            rejected, command.RejectingUserId, _repository, _attempts, cancellationToken);
    }
}

public sealed record ClaimStudentLinkCommand(string InviteCode, Guid ClaimingUserId) : ICommand<Result>;

/// <summary>
/// Öğrenci, öğretmenin verdiği davet kodunu girerek öğretmenin manuel oluşturduğu profili kendi hesabına
/// devralır (claim, Ö-C). Öğrencinin zaten bir self-register profili varsa birleştirme (merge) devreye girer.
/// <para>
/// P15 / N-20: kod normalize edilir; süresi dolmuş kod ve daveti üreten öğretmenin kendi kodunu kullanması
/// reddedilir; bulunamayan/süresi dolmuş her deneme kullanıcı başına sayılır, eşik aşılınca doğru kod da
/// reddedilir (<see cref="IInviteClaimAttemptLimiter"/>).
/// </para>
/// <para>
/// P15 / N-22, N-28: kabulün kendisi link kabulüyle aynı çekirdektedir (<see cref="StudentInviteAcceptance"/>):
/// hedefli davet yalnız hedefine açılır, başkasına ait profil devralınamaz, birleşmede öğretmen bağları ve
/// bekleyen veli davetleri aynı transaction'da kanonik profile taşınır.
/// </para>
/// </summary>
public sealed class ClaimStudentLinkCommandHandler : ICommandHandler<ClaimStudentLinkCommand, Result>
{
    private static readonly Error InviteNotFound = new("students.invite_not_found", "Davet kodu bulunamadi.");
    private static readonly Error InviteInvalid = new("students.invite_invalid", "Davet kodu artik gecerli degil.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IStudentProfileRepository _profileRepository;
    private readonly IStudentParentInviteRepository _parentInviteRepository;
    private readonly IInviteClaimAttemptLimiter _attempts;
    private readonly IClock _clock;

    public ClaimStudentLinkCommandHandler(
        ITeacherStudentLinkRepository repository,
        IStudentProfileRepository profileRepository,
        IStudentParentInviteRepository parentInviteRepository,
        IInviteClaimAttemptLimiter attempts,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _parentInviteRepository = parentInviteRepository;
        _attempts = attempts;
        _clock = clock;
    }

    public async Task<Result> Handle(ClaimStudentLinkCommand command, CancellationToken cancellationToken)
    {
        if (await _attempts.IsBlockedAsync(command.ClaimingUserId, cancellationToken))
        {
            return Result.Failure(StudentInviteAcceptance.TooManyAttempts);
        }

        var code = SecureCode.Normalize(command.InviteCode);
        var link = await _repository.GetByInviteCodeAsync(code, cancellationToken);
        if (link is null)
        {
            await _attempts.RecordFailureAsync(command.ClaimingUserId, cancellationToken);
            return Result.Failure(InviteNotFound);
        }

        // Kullanılmış (Linked) eski kod: "bulunamadı" değil "artık geçersiz" — mobil mesajı bunu ayırır.
        if (link.Status != TeacherStudentLinkStatus.InviteSent)
        {
            return Result.Failure(InviteInvalid);
        }

        var accepted = await StudentInviteAcceptance.AcceptAsync(
            link, command.ClaimingUserId, code, _clock.UtcNow,
            _repository, _profileRepository, _parentInviteRepository, cancellationToken);

        return await StudentInviteAcceptance.FinishAsync(
            accepted, command.ClaimingUserId, _repository, _attempts, cancellationToken);
    }
}

/// <summary>
/// P15 / N-22, N-23, N-28: Öğrenci davetini kabul etmenin tek çekirdeği — claim (kodla) ve link kabulü
/// (link kimliği + kod) aynı kuralı izler:
/// <list type="number">
/// <item>Kod, durum (<c>InviteSent</c>), hedef kullanıcı ve süre (<see cref="TeacherStudentLink.CanRespond"/>);
/// daveti üreten öğretmen kendi davetini kullanamaz.</item>
/// <item>Davet edilen profil başka bir hesaba aitse devralınamaz (N-23 ile açılmış satırlar).</item>
/// <item>Kullanıcının kendi profili yoksa (ya da zaten bu profilse) <b>devralma</b>: profil kullanıcıya bağlanır.</item>
/// <item>Kendi profili varsa <b>birleştirme</b>: öğretmen bağları (<see cref="StudentMergePolicy"/>), bekleyen veli
/// davetleri ve (boşsa) onaylı veli kanonik profile taşınır, manuel profil pasifleşir. Hepsi tek SaveChanges.</item>
/// </list>
/// Bağ, birleştirmeden <b>sonra</b> kabul edilir: <c>TeacherStudentLinkAccepted</c> olayı kanonik kimliği taşısın
/// (Reporting'in birleştirme tüketicisi yok). Hiçbir adım kaydetmez; çağıran <see cref="FinishAsync"/> ile kaydeder.
/// </summary>
internal static class StudentInviteAcceptance
{
    public static readonly Error TooManyAttempts = new("shared.too_many_attempts", "Çok fazla hatalı kod denemesi. Lütfen daha sonra tekrar dene.");
    private static readonly Error CannotClaimOwnInvite = new("students.cannot_claim_own_invite", "Kendi oluşturduğun daveti kullanamazsın.");
    private static readonly Error ProfileNotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");

    /// <summary>Kod tahminine işaret eden hatalar — kullanıcı başına deneme sayacına yazılır.</summary>
    private static readonly HashSet<string> GuessingErrors =
    [
        StudentErrors.InviteCodeMismatch.Code,
        StudentErrors.InviteExpired.Code,
        StudentErrors.InviteNotForUser.Code,
    ];

    public static async Task<Result> AcceptAsync(
        TeacherStudentLink link,
        Guid actingUserId,
        string? inviteCode,
        DateTime nowUtc,
        ITeacherStudentLinkRepository links,
        IStudentProfileRepository profiles,
        IStudentParentInviteRepository parentInvites,
        CancellationToken cancellationToken)
    {
        var respondable = link.CanRespond(actingUserId, inviteCode, nowUtc);
        if (respondable.IsFailure)
        {
            return respondable;
        }

        if (link.TeacherUserId == actingUserId)
        {
            return Result.Failure(CannotClaimOwnInvite);
        }

        var invitedProfile = await profiles.GetByIdAsync(link.StudentId, cancellationToken);
        if (invitedProfile is null)
        {
            return Result.Failure(ProfileNotFound);
        }

        var claimable = invitedProfile.EnsureClaimableBy(actingUserId);
        if (claimable.IsFailure)
        {
            return claimable;
        }

        var ownProfile = await profiles.GetByUserIdAsync(actingUserId, cancellationToken);
        if (ownProfile is null || ownProfile.Id == invitedProfile.Id)
        {
            // Devralma: öğrencinin başka profili yok → öğretmenin açtığı profil onun olur.
            var bound = invitedProfile.LinkUser(actingUserId, nowUtc);
            return bound.IsFailure ? bound : link.Accept(actingUserId, inviteCode, nowUtc);
        }

        // Birleştirme: kanonik = öğrencinin kendi profili.
        var fromLinks = await links.ListByStudentAsync(invitedProfile.Id, cancellationToken);
        var canonicalLinks = await links.ListByStudentAsync(ownProfile.Id, cancellationToken);
        var dropped = StudentMergePolicy.MergeTeacherLinks(fromLinks, canonicalLinks, ownProfile.Id, link.Id, nowUtc);
        foreach (var droppedLink in dropped)
        {
            links.Remove(droppedLink);
        }

        if (!dropped.Contains(link))
        {
            var accepted = link.Accept(actingUserId, inviteCode, nowUtc);
            if (accepted.IsFailure)
            {
                return accepted;
            }
        }

        foreach (var invite in await parentInvites.ListPendingByStudentAsync(invitedProfile.Id, cancellationToken))
        {
            invite.ReassignStudent(ownProfile.Id);
        }

        if (ownProfile.ParentUserId is null && invitedProfile.ParentUserId is { } approvedParent)
        {
            ownProfile.LinkParent(approvedParent, nowUtc);
        }

        invitedProfile.MarkMerged(ownProfile.Id, nowUtc);
        return Result.Success();
    }

    /// <summary>Başarıda kaydeder ve deneme sayacını sıfırlar; kod tahmini hatasında sayacı artırır.</summary>
    public static async Task<Result> FinishAsync(
        Result outcome,
        Guid actingUserId,
        ITeacherStudentLinkRepository links,
        IInviteClaimAttemptLimiter attempts,
        CancellationToken cancellationToken)
    {
        if (outcome.IsFailure)
        {
            if (GuessingErrors.Contains(outcome.Error.Code))
            {
                await attempts.RecordFailureAsync(actingUserId, cancellationToken);
            }

            return outcome;
        }

        await links.SaveChangesAsync(cancellationToken);
        await attempts.ResetAsync(actingUserId, cancellationToken);
        return Result.Success();
    }
}

public sealed class TeacherStudentLinkAuthorizer :
    ICommandAuthorizer<ArchiveTeacherStudentLinkCommand>,
    ICommandAuthorizer<SetTeacherStudentRateCommand>,
    ICommandAuthorizer<InviteStudentCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ogrenci uzerinde islem yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public TeacherStudentLinkAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(ArchiveTeacherStudentLinkCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CanManage(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    public Task<Result> Authorize(SetTeacherStudentRateCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CanManage(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    public Task<Result> Authorize(InviteStudentCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CanManage(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    private bool CanManage(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return true;
        }

        return _currentUser.Roles.Contains("Teacher")
            && Guid.TryParse(_currentUser.UserId, out var id)
            && id == teacherUserId;
    }
}

/// <summary>
/// Davet yanıtı (kabul/red) yetkilendirmesi: belirli hedef varsa yalnız o kullanıcı; admin serbest.
/// </summary>
public sealed class TeacherStudentLinkResponseAuthorizer :
    ICommandAuthorizer<AcceptTeacherStudentLinkCommand>,
    ICommandAuthorizer<RejectTeacherStudentLinkCommand>,
    ICommandAuthorizer<ClaimStudentLinkCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu daveti yanitlama yetkiniz yok.");
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private static readonly Error ClaimRequiresStudentRole = new("students.claim_requires_student_role", "Öğrenci davet kodunu yalnız öğrenci hesabı kullanabilir.");
    private readonly ICurrentUser _currentUser;
    private readonly ITeacherStudentLinkRepository _repository;

    public TeacherStudentLinkResponseAuthorizer(ICurrentUser currentUser, ITeacherStudentLinkRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(AcceptTeacherStudentLinkCommand command, CancellationToken cancellationToken)
        => AuthorizeResponse(command.LinkId, cancellationToken);

    public Task<Result> Authorize(RejectTeacherStudentLinkCommand command, CancellationToken cancellationToken)
        => AuthorizeResponse(command.LinkId, cancellationToken);

    // Açık claim: davet kodunu bilen, kimliği doğrulanmış bir öğrenci profili devralabilir; kod bilgisi
    // sahiplik kanıtı yerine geçer. P15 / N-20: yalnız Öğrenci (veya Admin) rolü — öğretmen ya da veli
    // hesabı öğrenci profilini devralamaz.
    public Task<Result> Authorize(ClaimStudentLinkCommand command, CancellationToken cancellationToken)
        => Task.FromResult(AuthorizeStudentRole());

    private Result AuthorizeStudentRole()
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        return _currentUser.Roles.Contains("Student") || _currentUser.Roles.Contains("Admin")
            ? Result.Success()
            : Result.Failure(ClaimRequiresStudentRole);
    }

    // P15 / N-22: link kabulü/reddi de yalnız öğrenci hesabına açıktır (kabul eden profilin sahibi olur);
    // kodun ve durumun doğrulaması domain'dedir (TeacherStudentLink.CanRespond).
    private async Task<Result> AuthorizeResponse(Guid linkId, CancellationToken cancellationToken)
    {
        var role = AuthorizeStudentRole();
        if (role.IsFailure)
        {
            return role;
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        var link = await _repository.GetByIdAsync(linkId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        if (!Guid.TryParse(_currentUser.UserId, out var currentUserId))
        {
            return Result.Failure(Forbidden);
        }

        // Belirli bir hedef kullanıcı varsa yalnız o yanıtlayabilir; hedef yoksa herhangi bir
        // kimliği doğrulanmış öğrenci daveti kabul edebilir (açık davet).
        return link.InviteTargetUserId is { } target && target != currentUserId
            ? Result.Failure(Forbidden)
            : Result.Success();
    }
}
