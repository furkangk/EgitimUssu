using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

public sealed class CreateStudentProfileCommandValidator : ICommandValidator<CreateStudentProfileCommand>
{
    private static readonly Error InvalidRequest = new("students.invalid_request", "Öğrenci profili bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        var isValid = !string.IsNullOrWhiteSpace(command.FullName)
            && !string.IsNullOrWhiteSpace(command.GradeLevel);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class CreateStudentProfileCommandAuthorizer : ICommandAuthorizer<CreateStudentProfileCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public CreateStudentProfileCommandAuthorizer(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result> Authorize(CreateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        if (isAdmin)
        {
            return Task.FromResult(Result.Success());
        }

        if (command.Origin == StudentOrigin.TeacherManaged
            && _currentUser.Roles.Contains("Teacher")
            && Guid.TryParse(_currentUser.UserId, out var teacherUserId)
            && teacherUserId == command.CreatedByTeacherUserId)
        {
            return Task.FromResult(Result.Success());
        }

        if (command.Origin == StudentOrigin.SelfRegistered
            && _currentUser.Roles.Contains("Student")
            && Guid.TryParse(_currentUser.UserId, out var studentUserId)
            && studentUserId == command.UserId)
        {
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure(Forbidden));
    }
}

public sealed class UpdateStudentProfileCommandValidator : ICommandValidator<UpdateStudentProfileCommand>
{
    private static readonly Error InvalidRequest = new("students.invalid_request", "Öğrenci profili bilgileri eksik veya hatalı.");

    public Task<Result> Validate(UpdateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        var isValid = !string.IsNullOrWhiteSpace(command.FullName)
            && !string.IsNullOrWhiteSpace(command.GradeLevel);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class UpdateStudentProfileCommandAuthorizer : ICommandAuthorizer<UpdateStudentProfileCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentProfileRepository _repository;

    public UpdateStudentProfileCommandAuthorizer(ICurrentUser currentUser, IStudentProfileRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public async Task<Result> Authorize(UpdateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        var profile = await _repository.GetByIdAsync(command.StudentId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(NotFound);
        }

        if (_currentUser.Roles.Contains("Teacher")
            && Guid.TryParse(_currentUser.UserId, out var teacherUserId)
            && profile.CreatedByTeacherUserId == teacherUserId)
        {
            return Result.Success();
        }

        return Result.Failure(Forbidden);
    }
}

public sealed class StudentProfileQueryAuthorizer :
    IQueryAuthorizer<GetStudentProfileByIdQuery>,
    IQueryAuthorizer<GetStudentProfileByUserIdQuery>,
    IQueryAuthorizer<ListStudentsByTeacherQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu kaynağa erişim yetkiniz yok.");
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentProfileRepository _repository;

    public StudentProfileQueryAuthorizer(ICurrentUser currentUser, IStudentProfileRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public async Task<Result> Authorize(GetStudentProfileByIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByIdAsync(query.StudentId, cancellationToken);
        return profile is null ? Result.Failure(NotFound) : EvaluateProfileAccess(profile);
    }

    public async Task<Result> Authorize(GetStudentProfileByUserIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(query.UserId, cancellationToken);
        return profile is null ? Result.Failure(NotFound) : EvaluateProfileAccess(profile);
    }

    public Task<Result> Authorize(ListStudentsByTeacherQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        var ownsList = Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == query.TeacherUserId;
        return Task.FromResult(isAdmin || (isTeacher && ownsList) ? Result.Success() : Result.Failure(Forbidden));
    }

    private Result EvaluateProfileAccess(StudentProfile profile)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        if (isAdmin)
        {
            return Result.Success();
        }

        var currentUserId = Guid.TryParse(_currentUser.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var hasAccess = currentUserId != Guid.Empty
            && (profile.UserId == currentUserId
                || profile.CreatedByTeacherUserId == currentUserId
                || profile.ParentUserId == currentUserId);

        return hasAccess ? Result.Success() : Result.Failure(Forbidden);
    }
}
