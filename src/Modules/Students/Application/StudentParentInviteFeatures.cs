using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

public interface IStudentParentInviteRepository
{
    Task AddAsync(StudentParentInvite invite, CancellationToken cancellationToken);

    Task<StudentParentInvite?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<StudentParentInvite?> GetByIdAsync(Guid inviteId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record CreateParentInviteCommand(Guid StudentId, Guid TeacherUserId, string? ChildDisplayName)
    : ICommand<Result<ParentInviteResponse>>;

public sealed record ParentInviteResponse(Guid Id, string InviteCode);

public sealed class CreateParentInviteCommandValidator : ICommandValidator<CreateParentInviteCommand>
{
    private static readonly Error Invalid = new("students.invalid_request", "Davet bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateParentInviteCommand command, CancellationToken cancellationToken)
        => Task.FromResult(
            command.StudentId == Guid.Empty || command.TeacherUserId == Guid.Empty
                ? Result.Failure(Invalid)
                : Result.Success());
}

public sealed class CreateParentInviteCommandHandler : ICommandHandler<CreateParentInviteCommand, Result<ParentInviteResponse>>
{
    private static readonly Error StudentNotFound = new("students.student_not_found", "Öğrenci bulunamadı.");
    private readonly IStudentParentInviteRepository _repository;
    private readonly IStudentProfileRepository _profileRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateParentInviteCommandHandler(
        IStudentParentInviteRepository repository,
        IStudentProfileRepository profileRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<ParentInviteResponse>> Handle(CreateParentInviteCommand command, CancellationToken cancellationToken)
    {
        var profile = await _profileRepository.GetByIdAsync(command.StudentId, cancellationToken);
        if (profile is null)
        {
            return Result<ParentInviteResponse>.Failure(StudentNotFound);
        }

        var inviteCode = TeacherStudentLink.GenerateInviteCode();
        var invite = new StudentParentInvite(
            _idGenerator.New(),
            command.StudentId,
            command.TeacherUserId,
            inviteCode,
            command.ChildDisplayName,
            _clock.UtcNow);

        await _repository.AddAsync(invite, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ParentInviteResponse>.Success(new ParentInviteResponse(invite.Id, inviteCode));
    }
}

public sealed class CreateParentInviteCommandAuthorizer : ICommandAuthorizer<CreateParentInviteCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu öğrenci için veli daveti oluşturma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public CreateParentInviteCommandAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(CreateParentInviteCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Task.FromResult(Result.Success());
        }

        var canManage = _currentUser.Roles.Contains("Teacher")
            && Guid.TryParse(_currentUser.UserId, out var id)
            && id == command.TeacherUserId;
        return Task.FromResult(canManage ? Result.Success() : Result.Failure(Forbidden));
    }
}
