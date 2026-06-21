using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Teachers.Application;

public sealed class CreateTeacherProfileCommandValidator : ICommandValidator<CreateTeacherProfileCommand>
{
    private static readonly Error InvalidRequest = new("teachers.invalid_request", "Öğretmen profili bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.UserId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.FullName)
            && !string.IsNullOrWhiteSpace(command.Subject)
            && !string.IsNullOrWhiteSpace(command.City)
            && !string.IsNullOrWhiteSpace(command.District)
            && !string.IsNullOrWhiteSpace(command.EducationLevel)
            && !string.IsNullOrWhiteSpace(command.Currency)
            && command.ExperienceYears >= 0
            && command.HourlyRateAmount >= 0;

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class UpdateTeacherProfileCommandValidator : ICommandValidator<UpdateTeacherProfileCommand>
{
    private readonly CreateTeacherProfileCommandValidator _inner = new();

    public Task<Result> Validate(UpdateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        return _inner.Validate(
            new CreateTeacherProfileCommand(
                command.UserId,
                command.FullName,
                command.Subject,
                command.City,
                command.District,
                command.Biography,
                command.Headline,
                command.LessonFormat,
                command.ExperienceYears,
                command.EducationLevel,
                command.HourlyRateAmount,
                command.Currency,
                command.ProfilePhotoUrl,
                command.AvailabilitySlots),
            cancellationToken);
    }
}

public sealed class TeacherProfileCommandAuthorizer :
    ICommandAuthorizer<CreateTeacherProfileCommand>,
    ICommandAuthorizer<UpdateTeacherProfileCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public TeacherProfileCommandAuthorizer(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result> Authorize(CreateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacherProfile(command.UserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    public Task<Result> Authorize(UpdateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(CanManageTeacherProfile(command.UserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    private bool CanManageTeacherProfile(Guid userId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        return isAdmin || (isTeacher && Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == userId);
    }
}
