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

public sealed class TeacherStudentLinkAuthorizer :
    ICommandAuthorizer<ArchiveTeacherStudentLinkCommand>,
    ICommandAuthorizer<SetTeacherStudentRateCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ogrenci uzerinde islem yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public TeacherStudentLinkAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(ArchiveTeacherStudentLinkCommand command, CancellationToken cancellationToken)
        => Task.FromResult(CanManage(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    public Task<Result> Authorize(SetTeacherStudentRateCommand command, CancellationToken cancellationToken)
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
