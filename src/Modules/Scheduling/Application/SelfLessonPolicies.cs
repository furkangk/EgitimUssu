using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed class CreateSelfLessonCommandValidator : ICommandValidator<CreateSelfLessonCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Program girdisi bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateSelfLessonCommand command, CancellationToken cancellationToken)
    {
        var duration = command.EndAtUtc - command.StartAtUtc;
        var isValid = command.StudentId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Subject)
            && command.Subject.Trim().Length <= 120
            && !string.IsNullOrWhiteSpace(command.TimeZone)
            && command.ReminderOffsetMinutes >= 0
            && command.EndAtUtc > command.StartAtUtc
            && duration >= TimeSpan.FromMinutes(15)
            && duration <= TimeSpan.FromHours(8);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class UpdateSelfLessonCommandValidator : ICommandValidator<UpdateSelfLessonCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Program girdisi bilgileri eksik veya hatalı.");

    public Task<Result> Validate(UpdateSelfLessonCommand command, CancellationToken cancellationToken)
    {
        var duration = command.EndAtUtc - command.StartAtUtc;
        var isValid = command.LessonId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Subject)
            && command.Subject.Trim().Length <= 120
            && !string.IsNullOrWhiteSpace(command.TimeZone)
            && command.ReminderOffsetMinutes >= 0
            && command.EndAtUtc > command.StartAtUtc
            && duration >= TimeSpan.FromMinutes(15)
            && duration <= TimeSpan.FromHours(8);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

/// <summary>
/// Öğrencinin kendi derslerini (self LessonSchedule) ve takvim sorgusunu koruyan yetkilendirici. Admin her zaman;
/// aksi halde girdinin/sorgunun StudentId'si oturum açan kullanıcıya ait olmalı. Sahiplik Students'ın yayınladığı
/// <see cref="IStudentDirectory"/> ile okunur (modül izolasyonu + IDOR koruması). Güncelle/sil yalnızca self derse
/// (TeacherUserId null) uygulanır — öğretmen derslerine öğrenci dokunamaz.
/// </summary>
public sealed class SelfLessonAuthorizer :
    ICommandAuthorizer<CreateSelfLessonCommand>,
    ICommandAuthorizer<UpdateSelfLessonCommand>,
    ICommandAuthorizer<DeleteSelfLessonCommand>,
    IQueryAuthorizer<GetStudentCalendarQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu programa erişim yetkiniz yok.");
    private static readonly Error NotFound = new("scheduling.entry_not_found", "Program girdisi bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _studentDirectory;
    private readonly ILessonScheduleRepository _repository;

    public SelfLessonAuthorizer(
        ICurrentUser currentUser,
        IStudentDirectory studentDirectory,
        ILessonScheduleRepository repository)
    {
        _currentUser = currentUser;
        _studentDirectory = studentDirectory;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateSelfLessonCommand command, CancellationToken cancellationToken)
        => AuthorizeStudent(command.StudentId, cancellationToken);

    public Task<Result> Authorize(GetStudentCalendarQuery query, CancellationToken cancellationToken)
        => AuthorizeStudent(query.StudentId, cancellationToken);

    public async Task<Result> Authorize(UpdateSelfLessonCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        return lesson is null || !lesson.IsSelfPlanned
            ? Result.Failure(NotFound)
            : await AuthorizeStudent(lesson.StudentId, cancellationToken);
    }

    public async Task<Result> Authorize(DeleteSelfLessonCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        return lesson is null || !lesson.IsSelfPlanned
            ? Result.Failure(NotFound)
            : await AuthorizeStudent(lesson.StudentId, cancellationToken);
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
