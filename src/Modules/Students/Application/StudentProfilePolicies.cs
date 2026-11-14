using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Application;

/// <summary>
/// Profil oluşturma isteğinin biçimi. P15 / N-23: profilin <b>kime ait olduğu</b> gövdeden gelmez —
/// öğretmenin açtığı profilde <c>userId</c>/<c>parentUserId</c>, öğrencinin açtığında
/// <c>createdByTeacherUserId</c>/<c>parentUserId</c> dolu gelirse istek <c>students.field_not_allowed</c> ile
/// reddedilir (kabul edilip sessizce yok sayılmaz). Hesap davetle, veli onaylı bağla gelir.
/// </summary>
public sealed class CreateStudentProfileCommandValidator : ICommandValidator<CreateStudentProfileCommand>
{
    private static readonly Error InvalidRequest = new("students.invalid_request", "Öğrenci profili bilgileri eksik veya hatalı.");
    private static readonly Error ParentNotAllowed = new(
        "students.field_not_allowed",
        "'parentUserId' profil oluşturulurken gönderilemez; veli, onaylı veli bağıyla eklenir.");
    private static readonly Error UserNotAllowed = new(
        "students.field_not_allowed",
        "'userId' öğretmenin açtığı profilde gönderilemez; öğrencinin hesabı davet koduyla bağlanır.");
    private static readonly Error TeacherNotAllowed = new(
        "students.field_not_allowed",
        "'createdByTeacherUserId' öğrencinin kendi açtığı profilde gönderilemez; öğretmen bağı davetle kurulur.");

    public Task<Result> Validate(CreateStudentProfileCommand command, CancellationToken cancellationToken)
    {
        var isValid = !string.IsNullOrWhiteSpace(command.FullName)
            && !string.IsNullOrWhiteSpace(command.GradeLevel);
        if (!isValid)
        {
            return Task.FromResult(Result.Failure(InvalidRequest));
        }

        if (command.ParentUserId is not null)
        {
            return Task.FromResult(Result.Failure(ParentNotAllowed));
        }

        return Task.FromResult(command.Origin switch
        {
            StudentOrigin.TeacherManaged when command.UserId is not null => Result.Failure(UserNotAllowed),
            StudentOrigin.SelfRegistered when command.CreatedByTeacherUserId is not null => Result.Failure(TeacherNotAllowed),
            _ => Result.Success()
        });
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

/// <summary>
/// M03-1: Veli bağlama yetkisi — Admin · profili oluşturan öğretmen · profilin sahibi öğrenci.
/// <para>
/// P15 T9 (#185 b): yeni veli bağı açmak, kardeşi <c>parent-invite</c> (N-21) gibi öğretmenden <b>yaşayan özel ders
/// bağı</b> da ister (<see cref="ITeacherStudentAccess"/> = <c>Private</c>). Profili açan ama bağı arşivlenmiş öğretmen
/// talep açamaz; profilin sahibi öğrenci öğretmen bağından bağımsızdır. Profil düzeltme ve branş uçları bilinçli olarak
/// sahiplikle kalır (yeni ilişki değil, var olan kaydın bakımı).
/// </para>
/// </summary>
public sealed class LinkStudentParentCommandAuthorizer : ICommandAuthorizer<LinkStudentParentCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudentProfileRepository _repository;
    private readonly ITeacherStudentAccess _access;

    public LinkStudentParentCommandAuthorizer(
        ICurrentUser currentUser,
        IStudentProfileRepository repository,
        ITeacherStudentAccess access)
    {
        _currentUser = currentUser;
        _repository = repository;
        _access = access;
    }

    public Task<Result> Authorize(LinkStudentParentCommand command, CancellationToken cancellationToken)
        => StudentProfileAccess.AuthorizeManage(
            _currentUser, _repository, command.StudentId, cancellationToken, requireLivingTeacherLink: _access);
}

/// <summary>Öğrenci profili alt kaynaklarının ortak yetki kuralı (tek yerde durur).</summary>
internal static class StudentProfileAccess
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotLinked = new("shared.forbidden", "Bu öğrenciyle aktif bir özel ders bağınız yok.");
    private static readonly Error NotFound = new("students.profile_not_found", "Öğrenci profili bulunamadı.");

    /// <summary>Admin · profili açan öğretmen · profilin sahibi öğrenci.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    /// <param name="repository">Profil deposu.</param>
    /// <param name="studentId">Profil kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <param name="requireLivingTeacherLink">
    /// Verilirse, profili açan öğretmen ayrıca öğrenciyle yaşayan özel ders bağı (<c>Private</c>) ister — yeni ilişki
    /// açan uçlar için (P15 T9, <c>link-parent</c>). Sahibi öğrenci ve Admin bu sorudan muaftır.
    /// </param>
    /// <returns>Yetki sonucu.</returns>
    public static async Task<Result> AuthorizeManage(
        ICurrentUser currentUser,
        IStudentProfileRepository repository,
        Guid studentId,
        CancellationToken cancellationToken,
        ITeacherStudentAccess? requireLivingTeacherLink = null)
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

        if (profile.UserId == userId)
        {
            return Result.Success();
        }

        var isOwningTeacher = currentUser.Roles.Contains("Teacher") && profile.CreatedByTeacherUserId == userId;
        if (!isOwningTeacher)
        {
            return Result.Failure(Forbidden);
        }

        if (requireLivingTeacherLink is null)
        {
            return Result.Success();
        }

        var scope = await requireLivingTeacherLink.GetScopeAsync(userId, studentId, cancellationToken);
        return scope == TeacherAccessScope.Private ? Result.Success() : Result.Failure(NotLinked);
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
    private readonly ITeacherStudentAccess _teacherAccess;
    private readonly IParentAccessDirectory _parentAccess;

    public StudentProfileQueryAuthorizer(
        ICurrentUser currentUser,
        IStudentProfileRepository repository,
        IRequestEntityCache entityCache,
        ITeacherStudentAccess teacherAccess,
        IParentAccessDirectory parentAccess)
    {
        _currentUser = currentUser;
        _repository = repository;
        _entityCache = entityCache;
        _teacherAccess = teacherAccess;
        _parentAccess = parentAccess;
    }

    public async Task<Result> Authorize(GetStudentProfileByIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _entityCache.GetOrLoadAsync(
            StudentProfileCacheKeys.ById(query.StudentId),
            ct => _repository.GetByIdAsync(query.StudentId, ct),
            cancellationToken);

        return profile is null ? Result.Failure(NotFound) : await EvaluateProfileAccessAsync(profile, cancellationToken);
    }

    public async Task<Result> Authorize(GetStudentProfileByUserIdQuery query, CancellationToken cancellationToken)
    {
        var profile = await _entityCache.GetOrLoadAsync(
            StudentProfileCacheKeys.ByUserId(query.UserId),
            ct => _repository.GetByUserIdAsync(query.UserId, ct),
            cancellationToken);

        return profile is null ? Result.Failure(NotFound) : await EvaluateProfileAccessAsync(profile, cancellationToken);
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

    /// <summary>
    /// Profil okuma: sahibi öğrenci, profili açan öğretmen, <b>onaylı bağı süren</b> veli — ve P15 / N-28'den beri
    /// profille <b>yaşayan özel ders bağı</b> olan öğretmen (<see cref="ITeacherStudentAccess"/> = <c>Private</c>).
    /// Birleşme sonrası kanonik profil öğrencinin kendi açtığıdır (<c>CreatedByTeacherUserId</c> boş); bağı taşınan
    /// öğretmen öğrenciyi listesinde görür, profilini de açabilmeli. Profil <b>yazma</b> yetkisi bilinçli olarak genişletilmedi.
    /// <para>
    /// P15 / N-26: veli erişimi profildeki <c>ParentUserId</c>'den değil Parents'taki onaylı bağdan
    /// (<see cref="IParentAccessDirectory"/>) okunur. Alan yalnız onay olayında yazılıyordu; bağı iptal/ret edilen veli
    /// okumaya devam ediyordu. Artık iptal anında kesilir (olayın işlenmesini beklemez) ve birincil olmayan onaylı veli de
    /// okuyabilir — <c>ListStudentsByParent</c> ile aynı kural. Kapsam/bağ sorguları yalnız ucuz kontroller başarısızsa ve
    /// yalnız ilgili rolde çalışır (O2: ek yükleme yok).
    /// </para>
    /// </summary>
    private async Task<Result> EvaluateProfileAccessAsync(StudentProfile profile, CancellationToken cancellationToken)
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

        if (!Guid.TryParse(_currentUser.UserId, out var currentUserId) || currentUserId == Guid.Empty)
        {
            return Result.Failure(Forbidden);
        }

        if (profile.UserId == currentUserId || profile.CreatedByTeacherUserId == currentUserId)
        {
            return Result.Success();
        }

        if (_currentUser.Roles.Contains("Teacher")
            && await _teacherAccess.GetScopeAsync(currentUserId, profile.Id, cancellationToken) == TeacherAccessScope.Private)
        {
            return Result.Success();
        }

        var isApprovedParent = _currentUser.Roles.Contains("Parent")
            && await _parentAccess.IsApprovedParentOfStudentAsync(currentUserId, profile.Id, cancellationToken);
        return isApprovedParent ? Result.Success() : Result.Failure(Forbidden);
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
