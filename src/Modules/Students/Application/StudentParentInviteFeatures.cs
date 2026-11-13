using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
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
    private readonly ISecureCodeGenerator _codes;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateParentInviteCommandHandler(
        IStudentParentInviteRepository repository,
        IStudentProfileRepository profileRepository,
        ISecureCodeGenerator codes,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _codes = codes;
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

        var inviteCode = _codes.NewCode();
        var now = _clock.UtcNow;
        var invite = new StudentParentInvite(
            _idGenerator.New(),
            command.StudentId,
            command.TeacherUserId,
            inviteCode,
            command.ChildDisplayName,
            now,
            now.AddDays(InvitePolicy.ExpiryDays));

        await _repository.AddAsync(invite, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ParentInviteResponse>.Success(new ParentInviteResponse(invite.Id, inviteCode));
    }
}

/// <summary>
/// P15 T2 / N-21: Veli daveti yalnız öğrenciyle yaşayan özel ders bağı olan öğretmen tarafından üretilir.
/// Eskiden öğrenci kimliğini bilen herhangi bir öğretmen hesabı davet üretip ikinci bir hesapla kendisi
/// kullanabiliyor ve çocuğun birincil velisi oluyordu.
/// </summary>
public sealed class CreateParentInviteCommandAuthorizer : ICommandAuthorizer<CreateParentInviteCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu öğrenci için veli daveti oluşturma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    private readonly ITeacherStudentAccess _access;

    public CreateParentInviteCommandAuthorizer(ICurrentUser currentUser, ITeacherStudentAccess access)
    {
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<Result> Authorize(CreateParentInviteCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        var isSelf = _currentUser.Roles.Contains("Teacher")
            && Guid.TryParse(_currentUser.UserId, out var id)
            && id == command.TeacherUserId;
        if (!isSelf)
        {
            return Result.Failure(Forbidden);
        }

        var scope = await _access.GetScopeAsync(command.TeacherUserId, command.StudentId, cancellationToken);
        return scope == TeacherAccessScope.Private ? Result.Success() : Result.Failure(Forbidden);
    }
}
