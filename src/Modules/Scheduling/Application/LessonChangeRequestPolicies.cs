using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

/// <summary>
/// Öğrencinin kendi adına erteleme talebi açmasını koruyan yetkilendirici. Admin her zaman; aksi halde
/// komuttaki StudentId oturum açan kullanıcıya ait olmalı. Sahiplik Students'ın yayınladığı
/// <see cref="IStudentDirectory"/> sözleşmesinden okunur (modül izolasyonu + IDOR koruması).
/// </summary>
public sealed class LessonChangeRequestStudentAuthorizer : ICommandAuthorizer<CreateLessonChangeRequestCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _studentDirectory;

    public LessonChangeRequestStudentAuthorizer(ICurrentUser currentUser, IStudentDirectory studentDirectory)
    {
        _currentUser = currentUser;
        _studentDirectory = studentDirectory;
    }

    public async Task<Result> Authorize(CreateLessonChangeRequestCommand command, CancellationToken cancellationToken)
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

        var ownerUserId = await _studentDirectory.GetOwnerUserIdAsync(command.StudentId, cancellationToken);
        return ownerUserId == userId ? Result.Success() : Result.Failure(Forbidden);
    }
}

/// <summary>
/// Öğretmenin yalnızca kendi derslerine gelen erteleme taleplerini görmesini/sonuçlandırmasını koruyan
/// yetkilendirici. Admin her zaman; aksi halde talebin/sorgunun TeacherUserId'si oturum açan öğretmene ait olmalı.
/// </summary>
public sealed class LessonChangeRequestTeacherAuthorizer :
    ICommandAuthorizer<AcceptLessonChangeRequestCommand>,
    ICommandAuthorizer<RejectLessonChangeRequestCommand>,
    IQueryAuthorizer<ListLessonChangeRequestsForTeacherQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private static readonly Error NotFound = new("scheduling.request_not_found", "Erteleme talebi bulunamadı.");
    private readonly ICurrentUser _currentUser;
    private readonly ILessonChangeRequestRepository _repository;

    public LessonChangeRequestTeacherAuthorizer(ICurrentUser currentUser, ILessonChangeRequestRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public Task<Result> Authorize(AcceptLessonChangeRequestCommand command, CancellationToken cancellationToken)
        => AuthorizeRequest(command.RequestId, cancellationToken);

    public Task<Result> Authorize(RejectLessonChangeRequestCommand command, CancellationToken cancellationToken)
        => AuthorizeRequest(command.RequestId, cancellationToken);

    public Task<Result> Authorize(ListLessonChangeRequestsForTeacherQuery query, CancellationToken cancellationToken)
        => Task.FromResult(CanManageTeacher(query.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));

    private async Task<Result> AuthorizeRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(requestId, cancellationToken);
        return request is null
            ? Result.Failure(NotFound)
            : (CanManageTeacher(request.TeacherUserId) ? Result.Success() : Result.Failure(Forbidden));
    }

    private bool CanManageTeacher(Guid teacherUserId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return false;
        }

        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isTeacher = _currentUser.Roles.Contains("Teacher");
        return isAdmin || (isTeacher && Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == teacherUserId);
    }
}
