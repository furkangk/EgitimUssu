using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Application;

/// <summary>
/// M10-2 yetkilendiricileri. Öğrencinin kendi uçları açık-generik
/// <see cref="ProgressOwnershipQueryAuthorizer{TQuery}"/> ile korunur; öğretmen ve veli görünümü
/// <b>ikinci</b> bir bağ doğrulaması ister (öğretmen–öğrenci bağı / onaylı veli–çocuk bağı).
/// <para>
/// Her iki yetkilendirici de önce rotadaki kimliğin oturumdaki kullanıcıyla aynı olduğunu doğrular:
/// bağ tek başına yetmez, aksi hâlde bir öğretmen başka bir öğretmenin rotasından okuyabilirdi (IDOR).
/// </para>
/// </summary>
internal static class ProgressAccessErrors
{
    public static readonly Error Forbidden = new("shared.forbidden", "Bu gelişim verisine erişim yetkiniz yok.");
}

/// <summary>M10-2: Öğretmen, yalnız kendi rotasından ve yalnız bağlı olduğu öğrencinin özetini okur.</summary>
public sealed class TeacherStudentProgressAuthorizer : IQueryAuthorizer<GetTeacherStudentProgressOverviewQuery>
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _students;

    public TeacherStudentProgressAuthorizer(ICurrentUser currentUser, IStudentDirectory students)
    {
        _currentUser = currentUser;
        _students = students;
    }

    public async Task<Result> Authorize(GetTeacherStudentProgressOverviewQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(ProgressAccessErrors.Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId != query.TeacherUserId)
        {
            return Result.Failure(ProgressAccessErrors.Forbidden);
        }

        var isLinked = await _students.IsLinkedTeacherOfStudentAsync(query.TeacherUserId, query.StudentId, cancellationToken);
        return isLinked ? Result.Success() : Result.Failure(ProgressAccessErrors.Forbidden);
    }
}

/// <summary>M10-2: Veli, yalnız kendi rotasından ve yalnız <b>onaylı</b> bağı olan çocuğunun özetini okur.</summary>
public sealed class ParentChildProgressAuthorizer : IQueryAuthorizer<GetParentChildProgressOverviewQuery>
{
    private readonly ICurrentUser _currentUser;
    private readonly IParentAccessDirectory _parents;

    public ParentChildProgressAuthorizer(ICurrentUser currentUser, IParentAccessDirectory parents)
    {
        _currentUser = currentUser;
        _parents = parents;
    }

    public async Task<Result> Authorize(GetParentChildProgressOverviewQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(ProgressAccessErrors.Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId != query.ParentUserId)
        {
            return Result.Failure(ProgressAccessErrors.Forbidden);
        }

        var isApproved = await _parents.IsApprovedParentOfStudentAsync(query.ParentUserId, query.StudentId, cancellationToken);
        return isApproved ? Result.Success() : Result.Failure(ProgressAccessErrors.Forbidden);
    }
}
