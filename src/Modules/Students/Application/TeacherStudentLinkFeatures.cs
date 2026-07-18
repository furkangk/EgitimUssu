using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

public interface ITeacherStudentLinkRepository
{
    Task AddAsync(TeacherStudentLink link, CancellationToken cancellationToken);

    Task<TeacherStudentLink?> GetByIdAsync(Guid linkId, CancellationToken cancellationToken);

    Task<TeacherStudentLink?> GetByTeacherAndStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken);

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
    ICommandAuthorizer<RejectTeacherStudentLinkCommand>
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
