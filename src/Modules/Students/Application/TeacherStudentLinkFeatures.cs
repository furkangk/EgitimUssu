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
    private readonly IClock _clock;

    public InviteStudentCommandHandler(ITeacherStudentLinkRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(InviteStudentCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByTeacherAndStudentAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        if (link is null)
        {
            return Result.Failure(NotFound);
        }

        link.MarkInviteSent(TeacherStudentLink.GenerateInviteCode(), command.TargetUserId, _clock.UtcNow);
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
/// Öğrenci, öğretmenin verdiği 6 haneli davet kodunu girerek öğretmenin manuel oluşturduğu profili
/// kendi hesabına devralır (claim, Ö-C). Öğrencinin zaten bir self-register profili varsa birleştirme
/// (merge) devreye girer; bu dal Task 3'te eklenir.
/// </summary>
public sealed class ClaimStudentLinkCommandHandler : ICommandHandler<ClaimStudentLinkCommand, Result>
{
    private static readonly Error InviteNotFound = new("students.invite_not_found", "Davet kodu bulunamadi.");
    private static readonly Error InviteInvalid = new("students.invite_invalid", "Davet kodu artik gecerli degil.");
    private readonly ITeacherStudentLinkRepository _repository;
    private readonly IStudentProfileRepository _profileRepository;
    private readonly IClock _clock;

    public ClaimStudentLinkCommandHandler(
        ITeacherStudentLinkRepository repository,
        IStudentProfileRepository profileRepository,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _clock = clock;
    }

    public async Task<Result> Handle(ClaimStudentLinkCommand command, CancellationToken cancellationToken)
    {
        var link = await _repository.GetByInviteCodeAsync(command.InviteCode, cancellationToken);
        if (link is null)
        {
            return Result.Failure(InviteNotFound);
        }

        if (link.Status != TeacherStudentLinkStatus.InviteSent)
        {
            return Result.Failure(InviteInvalid);
        }

        link.Accept(_clock.UtcNow);

        var manualProfile = await _profileRepository.GetByIdAsync(link.StudentId, cancellationToken);

        // Mevcut self-register profil yoksa: manuel profili öğrenci kullanıcısına bağla (basit devralma).
        // Varsa: tam profil birleştirme (merge) — Task 3.
        manualProfile?.LinkUser(command.ClaimingUserId, _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
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

    // Açık claim: davet kodunu bilen, kimliği doğrulanmış herhangi bir öğrenci profili devralabilir;
    // kod bilgisi sahiplik kanıtı yerine geçer. Belirli bir hedef kullanıcı doğrulaması yapılmaz.
    public Task<Result> Authorize(ClaimStudentLinkCommand command, CancellationToken cancellationToken)
        => Task.FromResult(_currentUser.IsAuthenticated ? Result.Success() : Result.Failure(Forbidden));

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
