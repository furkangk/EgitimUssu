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

/// <summary>
/// M03-1/M03-2: Öğrenci profilinin alt kaynaklarını (branş, veli bağı) değiştirme yetkisi.
/// Admin · profili oluşturan öğretmen · profilin sahibi öğrenci. Sahibi öğrenciyi de kapsar,
/// çünkü self-register profillerin <c>CreatedByTeacherUserId</c>'si yoktur.
/// </summary>
public sealed class StudentSubjectCommandAuthorizer :
    ICommandAuthorizer<AddStudentSubjectCommand>,
    ICommandAuthorizer<RemoveStudentSubjectCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudentProfileRepository _repository;

    public StudentSubjectCommandAuthorizer(ICurrentUser currentUser, IStudentProfileRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(AddStudentSubjectCommand command, CancellationToken cancellationToken)
        => StudentProfileAccess.AuthorizeManage(_currentUser, _repository, command.StudentId, cancellationToken);

    public Task<Result> Authorize(RemoveStudentSubjectCommand command, CancellationToken cancellationToken)
        => StudentProfileAccess.AuthorizeManage(_currentUser, _repository, command.StudentId, cancellationToken);
}

/// <summary>M03-1: Veli bağlama yetkisi — Admin · profili oluşturan öğretmen · profilin sahibi öğrenci.</summary>
public sealed class LinkStudentParentCommandAuthorizer : ICommandAuthorizer<LinkStudentParentCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudentProfileRepository _repository;

    public LinkStudentParentCommandAuthorizer(ICurrentUser currentUser, IStudentProfileRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(LinkStudentParentCommand command, CancellationToken cancellationToken)
        => StudentProfileAccess.AuthorizeManage(_currentUser, _repository, command.StudentId, cancellationToken);
}

/// <summary>Öğrenci profili alt kaynaklarının ortak yetki kuralı (tek yerde durur).</summary>
internal static class StudentProfileAccess
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");

    public static async Task<Result> AuthorizeManage(
        ICurrentUser currentUser,
        IStudentProfileRepository repository,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        var profile = await repository.GetByIdAsync(studentId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(NotFound);
        }

        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            return Result.Failure(Forbidden);
        }

        var isOwningTeacher = currentUser.Roles.Contains("Teacher") && profile.CreatedByTeacherUserId == userId;
        var isOwningStudent = profile.UserId == userId;
        return isOwningTeacher || isOwningStudent ? Result.Success() : Result.Failure(Forbidden);
    }
}

public sealed class StudentProfileQueryAuthorizer :
    IQueryAuthorizer<GetStudentProfileByIdQuery>,
    IQueryAuthorizer<GetStudentProfileByUserIdQuery>,
    IQueryAuthorizer<ListStudentsByTeacherQuery>,
    IQueryAuthorizer<ListStudentsByParentQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu kaynağa erişim yetkiniz yok.");
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentProfileRepository _repository;
    private readonly IRequestEntityCache _entityCache;

    public StudentProfileQueryAuthorizer(
        ICurrentUser currentUser,
        IStudentProfileRepository repository,
        IRequestEntityCache entityCache)
    {
        _currentUser = currentUser;
        _repository = repository;
        _entityCache = entityCache;
    }

    public async Task<Result> Authorize(GetStudentProfileByIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _entityCache.GetOrLoadAsync(
            StudentProfileCacheKeys.ById(query.StudentId),
            ct => _repository.GetByIdAsync(query.StudentId, ct),
            cancellationToken);

        return profile is null ? Result.Failure(NotFound) : EvaluateProfileAccess(profile);
    }

    public async Task<Result> Authorize(GetStudentProfileByUserIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _entityCache.GetOrLoadAsync(
            StudentProfileCacheKeys.ByUserId(query.UserId),
            ct => _repository.GetByUserIdAsync(query.UserId, ct),
            cancellationToken);

        return profile is null ? Result.Failure(NotFound) : EvaluateProfileAccess(profile);
    }

    /// <summary>M03-3: Veli yalnız <b>kendi</b> çocuk listesini sorgulayabilir; Admin hepsini.</summary>
    public Task<Result> Authorize(ListStudentsByParentQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Task.FromResult(Result.Success());
        }

        var ownsList = Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == query.ParentUserId;
        return Task.FromResult(ownsList ? Result.Success() : Result.Failure(Forbidden));
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

/// <summary>
/// O2: Öğrenci profilinin istek kapsamı önbellek anahtarları. Aynı profil iki farklı yoldan
/// (id / kullanıcı id) aranabildiği için anahtarlar ayrıştırılır.
/// </summary>
internal static class StudentProfileCacheKeys
{
    public static object ById(Guid studentId) => ("students.profile.byId", studentId);

    public static object ByUserId(Guid userId) => ("students.profile.byUserId", userId);
}
