using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed class CreateStudyScheduleEntryCommandValidator : ICommandValidator<CreateStudyScheduleEntryCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Program girdisi bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.StudentId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Subject)
            && !string.IsNullOrWhiteSpace(command.TimeZone)
            && command.ReminderOffsetMinutes >= 0;

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class UpdateStudyScheduleEntryCommandValidator : ICommandValidator<UpdateStudyScheduleEntryCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Program girdisi bilgileri eksik veya hatalı.");

    public Task<Result> Validate(UpdateStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.EntryId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Subject)
            && !string.IsNullOrWhiteSpace(command.TimeZone)
            && command.ReminderOffsetMinutes >= 0;

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

/// <summary>
/// Öğrenci-sahipli program girdilerini ve takvim sorgusunu koruyan yetkilendirici. Admin her zaman;
/// aksi halde girdinin/sorgunun StudentId'si oturum açan kullanıcıya ait olmalı. Sahiplik Students'ın
/// yayınladığı <see cref="IStudentDirectory"/> sözleşmesinden okunur (modül izolasyonu + IDOR koruması).
/// </summary>
public sealed class StudyScheduleEntryAuthorizer :
    ICommandAuthorizer<CreateStudyScheduleEntryCommand>,
    ICommandAuthorizer<UpdateStudyScheduleEntryCommand>,
    ICommandAuthorizer<DeleteStudyScheduleEntryCommand>,
    IQueryAuthorizer<GetStudentCalendarQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu programa erişim yetkiniz yok.");
    private static readonly Error NotFound = new("scheduling.entry_not_found", "Program girdisi bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _studentDirectory;
    private readonly IStudyScheduleEntryRepository _repository;

    public StudyScheduleEntryAuthorizer(
        ICurrentUser currentUser,
        IStudentDirectory studentDirectory,
        IStudyScheduleEntryRepository repository)
    {
        _currentUser = currentUser;
        _studentDirectory = studentDirectory;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateStudyScheduleEntryCommand command, CancellationToken cancellationToken)
        => AuthorizeStudent(command.StudentId, cancellationToken);

    public Task<Result> Authorize(GetStudentCalendarQuery query, CancellationToken cancellationToken)
        => AuthorizeStudent(query.StudentId, cancellationToken);

    public async Task<Result> Authorize(UpdateStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken);
        return entry is null
            ? Result.Failure(NotFound)
            : await AuthorizeStudent(entry.StudentId, cancellationToken);
    }

    public async Task<Result> Authorize(DeleteStudyScheduleEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(command.EntryId, cancellationToken);
        return entry is null
            ? Result.Failure(NotFound)
            : await AuthorizeStudent(entry.StudentId, cancellationToken);
    }

    private async Task<Result> AuthorizeStudent(Guid studentId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(Forbidden);
        }

        var ownerUserId = await _studentDirectory.GetOwnerUserIdAsync(studentId, cancellationToken);
        return ownerUserId == userId ? Result.Success() : Result.Failure(Forbidden);
    }
}
