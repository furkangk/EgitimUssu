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

public sealed record AcceptTeacherStudentLinkCommand(Guid LinkId, Guid AcceptingUserId) : ICommand<Result>;

public sealed record RejectTeacherStudentLinkCommand(Guid LinkId, Guid RejectingUserId) : ICommand<Result>;

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

public sealed class AcceptTeacherStudentLinkCommandHandler : ICommandHandler<AcceptTeacherStudentLinkCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IStudentProfileRepository _profileRepository;
    private readonly IClock _clock;

    public AcceptTeacherStudentLinkCommandHandler(
        ITeacherStudentLinkRepository repository,
        IStudentProfileRepository profileRepository,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _clock = clock;
    }

    public async Task<Result> Handle(AcceptTeacherStudentLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        link.Accept(_clock.UtcNow);

        var profile = await _profileRepository.GetByIdAsync(link.StudentId, cancellationToken);
        profile?.LinkUser(command.AcceptingUserId, _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class RejectTeacherStudentLinkCommandHandler : ICommandHandler<RejectTeacherStudentLinkCommand, Result>
{
    private static readonly Error NotFound = new("students.link_not_found", "Ogrenci baglantisi bulunamadi.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IClock _clock;

    public RejectTeacherStudentLinkCommandHandler(ITeacherStudentLinkRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(RejectTeacherStudentLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        link.Reject(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
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
/// </summary>
public sealed class ClaimStudentLinkCommandHandler : ICommandHandler<ClaimStudentLinkCommand, Result>
{
    private static readonly Error InviteNotFound = new("students.invite_not_found", "Davet kodu bulunamadi.");
    private static readonly Error InviteInvalid = new("students.invite_invalid", "Davet kodu artik gecerli degil.");
    private static readonly Error InviteExpired = new("students.invite_expired", "Davet kodunun süresi dolmuş. Öğretmeninden yeni bir kod iste.");
    private static readonly Error CannotClaimOwnInvite = new("students.cannot_claim_own_invite", "Kendi oluşturduğun daveti kullanamazsın.");
    private static readonly Error TooManyAttempts = new("shared.too_many_attempts", "Çok fazla hatalı kod denemesi. Lütfen daha sonra tekrar dene.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IStudentProfileRepository _profileRepository;
    private readonly IInviteClaimAttemptLimiter _attempts;
    private readonly IClock _clock;

    public ClaimStudentLinkCommandHandler(
        ITeacherStudentLinkRepository repository,
        IStudentProfileRepository profileRepository,
        IInviteClaimAttemptLimiter attempts,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _attempts = attempts;
        _clock = clock;
    }

    public async Task<Result> Handle(ClaimStudentLinkCommand command, CancellationToken cancellationToken)
    {
        if (await _attempts.IsBlockedAsync(command.ClaimingUserId, cancellationToken))
        {
            return Result.Failure(TooManyAttempts);
        }

        var link = await _repository.GetByInviteCodeAsync(SecureCode.Normalize(command.InviteCode), cancellationToken);
        if (link is null)
        {
            await _attempts.RecordFailureAsync(command.ClaimingUserId, cancellationToken);
            return Result.Failure(InviteNotFound);
        }

        if (link.Status != TeacherStudentLinkStatus.InviteSent)
        {
            return Result.Failure(InviteInvalid);
        }

        var now = _clock.UtcNow;
        if (link.IsInviteExpired(now))
        {
            await _attempts.RecordFailureAsync(command.ClaimingUserId, cancellationToken);
            return Result.Failure(InviteExpired);
        }

        if (link.TeacherUserId == command.ClaimingUserId)
        {
            return Result.Failure(CannotClaimOwnInvite);
        }

        link.Accept(now);

        var manualProfile = await _profileRepository.GetByIdAsync(link.StudentId, cancellationToken);
        var existingProfile = await _profileRepository.GetByUserIdAsync(command.ClaimingUserId, cancellationToken);

        if (manualProfile is not null && existingProfile is not null && existingProfile.Id != manualProfile.Id)
        {
            // Öğrencinin zaten bir self-register profili var → kanonik = self-profil. Manuel profili
            // birleştir (pasifleştir) ve modüller-arası StudentId yeniden atamasını tetikle (Outbox).
            manualProfile.MarkMerged(existingProfile.Id, _clock.UtcNow);
        }
        else
        {
            // Mevcut self-register profil yok → manuel profili öğrenci kullanıcısına bağla (basit devralma).
            manualProfile?.LinkUser(command.ClaimingUserId, _clock.UtcNow);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        await _attempts.ResetAsync(command.ClaimingUserId, cancellationToken);
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
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        return Task.FromResult(
            _currentUser.Roles.Contains("Student") || _currentUser.Roles.Contains("Admin")
                ? Result.Success()
                : Result.Failure(ClaimRequiresStudentRole));
    }

    private async Task<Result> AuthorizeResponse(Guid linkId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
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
