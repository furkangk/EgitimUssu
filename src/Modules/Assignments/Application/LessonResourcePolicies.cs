using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Application;

/// <summary>
/// M06-1: Ders kaynagi yetkilendirmesi (varsayilan-deny).
/// - **Olusturma/arsivleme:** yalniz kaynagin ogretmeni (veya Admin).
/// - **Listeleme/indirme:** ogretmen kendi kaynaklarini; ogrenci yalniz kendisine acik olanlari
///   (`studentId` = kendi kimligi). Ogrenci baska bir ogrencinin ya da tum ogretmenin listesini isteyemez.
/// </summary>
public sealed class LessonResourceAuthorizer :
    ICommandAuthorizer<CreateLessonResourceCommand>,
    ICommandAuthorizer<ArchiveLessonResourceCommand>,
    IQueryAuthorizer<ListLessonResourcesQuery>,
    IQueryAuthorizer<GetLessonResourceFileQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu kaynaga erisim yetkiniz yok.");

    private readonly ICurrentUser _currentUser;
    private readonly ILessonResourceRepository _repository;

    public LessonResourceAuthorizer(ICurrentUser currentUser, ILessonResourceRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(CreateLessonResourceCommand command, CancellationToken cancellationToken)
        => Task.FromResult(IsSelfOrAdmin(command.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    public async Task<Result> Authorize(ArchiveLessonResourceCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (IsAdmin)
        {
            return Result.Success();
        }

        var resource = await _repository.GetByIdAsync(command.ResourceId, cancellationToken);
        if (resource is null)
        {
            // Var olmayan kaynak: yetki katmani sizinti yapmaz, handler NotFound doner.
            return Result.Success();
        }

        return IsSelfOrAdmin(resource.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden);
    }

    public Task<Result> Authorize(ListLessonResourcesQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        if (IsAdmin)
        {
            return Task.FromResult(Result.Success());
        }

        if (!Guid.TryParse(_currentUser.UserId, out var currentUserId))
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var asTeacher = query.TeacherUserId == currentUserId;
        var asStudent = query.StudentId == currentUserId;

        return Task.FromResult(asTeacher || asStudent ? Result.Success() : Result.Failure(Forbidden));
    }

    public async Task<Result> Authorize(GetLessonResourceFileQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (IsAdmin)
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var currentUserId))
        {
            return Result.Failure(Forbidden);
        }

        var resource = await _repository.GetByIdAsync(query.ResourceId, cancellationToken);
        if (resource is null)
        {
            return Result.Success();
        }

        if (resource.TeacherUserId == currentUserId)
        {
            return Result.Success();
        }

        // Ogrenciye acik kaynak: kendisine ozel ya da o ogretmenin tum ogrencilerine acik olan.
        var visibleToStudent = resource.StudentId is null || resource.StudentId == currentUserId;
        return visibleToStudent ? Result.Success() : Result.Failure(Forbidden);
    }

    private bool IsAdmin => _currentUser.Roles.Contains("Admin");

    private bool IsSelfOrAdmin(Guid userId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        return IsAdmin || (Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == userId);
    }
}
