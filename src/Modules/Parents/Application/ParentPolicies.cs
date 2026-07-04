using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Parents.Application;

// ---------------------------------------------------------------------------
// Validators (şekil kontrolü)
// ---------------------------------------------------------------------------

public sealed class CreateParentProfileCommandValidator : ICommandValidator<CreateParentProfileCommand>
{
    public Task<Result> Validate(CreateParentProfileCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == Guid.Empty || string.IsNullOrWhiteSpace(command.FullName))
        {
            return Task.FromResult(Result.Failure(ParentErrors.InvalidRequest));
        }

        return Task.FromResult(Result.Success());
    }
}

public sealed class RequestChildLinkCommandValidator : ICommandValidator<RequestChildLinkCommand>
{
    public Task<Result> Validate(RequestChildLinkCommand command, CancellationToken cancellationToken)
    {
        if (command.ParentUserId == Guid.Empty || command.StudentId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(ParentErrors.InvalidRequest));
        }

        return Task.FromResult(Result.Success());
    }
}

// ---------------------------------------------------------------------------
// Authorizer — tüm command/query yetkilendirmesi tek sınıfta (Scheduling deseni).
// ---------------------------------------------------------------------------

public sealed class ParentAuthorizer :
    ICommandAuthorizer<CreateParentProfileCommand>,
    ICommandAuthorizer<UpdateNotificationPreferencesCommand>,
    ICommandAuthorizer<RequestChildLinkCommand>,
    ICommandAuthorizer<ApproveChildLinkCommand>,
    ICommandAuthorizer<RejectChildLinkCommand>,
    ICommandAuthorizer<RevokeChildLinkCommand>,
    IQueryAuthorizer<GetParentProfileQuery>,
    IQueryAuthorizer<ListChildrenQuery>,
    IQueryAuthorizer<GetChildDashboardQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu kaynağa erişim yetkiniz yok.");

    private readonly ICurrentUser _currentUser;
    private readonly IParentRepository _repository;

    public ParentAuthorizer(ICurrentUser currentUser, IParentRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    private bool IsAdmin => _currentUser.Roles.Contains("Admin");

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(_currentUser.UserId, out userId);
    }

    private Result RequireSelfOrAdmin(Guid targetUserId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (IsAdmin)
        {
            return Result.Success();
        }

        return TryGetUserId(out var userId) && userId == targetUserId
            ? Result.Success()
            : Result.Failure(Forbidden);
    }

    public Task<Result> Authorize(CreateParentProfileCommand command, CancellationToken cancellationToken)
        => Task.FromResult(RequireSelfOrAdmin(command.UserId));

    public Task<Result> Authorize(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
        => Task.FromResult(RequireSelfOrAdmin(command.ParentUserId));

    public Task<Result> Authorize(RequestChildLinkCommand command, CancellationToken cancellationToken)
        => Task.FromResult(RequireSelfOrAdmin(command.ParentUserId));

    public Task<Result> Authorize(GetParentProfileQuery query, CancellationToken cancellationToken)
        => Task.FromResult(RequireSelfOrAdmin(query.UserId));

    public Task<Result> Authorize(ListChildrenQuery query, CancellationToken cancellationToken)
        => Task.FromResult(RequireSelfOrAdmin(query.ParentUserId));

    public Task<Result> Authorize(GetChildDashboardQuery query, CancellationToken cancellationToken)
        => Task.FromResult(RequireSelfOrAdmin(query.ParentUserId));

    // Onay/red: Admin ya da bağın ait olduğu öğrencinin kendisi (fail-closed: eşleme bilinmiyorsa yalnız Admin).
    // Veli KENDİ bağını onaylayamaz (self-approve engeli).
    public async Task<Result> Authorize(ApproveChildLinkCommand command, CancellationToken cancellationToken)
        => await AuthorizeStudentOrAdminAsync(command.LinkId, cancellationToken);

    public async Task<Result> Authorize(RejectChildLinkCommand command, CancellationToken cancellationToken)
        => await AuthorizeStudentOrAdminAsync(command.LinkId, cancellationToken);

    // İptal: Admin, bağın velisi (kendi bağını iptal edebilir) ya da öğrencinin kendisi.
    public async Task<Result> Authorize(RevokeChildLinkCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        var link = await _repository.GetLinkByIdAsync(command.LinkId, cancellationToken);
        if (link is null)
        {
            // Bağ yoksa handler 404 dönsün; veri sızıntısı yok.
            return Result.Success();
        }

        if (IsAdmin)
        {
            return Result.Success();
        }

        if (TryGetUserId(out var userId))
        {
            if (userId == link.ParentUserId)
            {
                return Result.Success();
            }

            var known = await _repository.GetKnownStudentAsync(link.StudentId, cancellationToken);
            if (known?.UserId is { } studentUserId && studentUserId == userId)
            {
                return Result.Success();
            }
        }

        return Result.Failure(Forbidden);
    }

    private async Task<Result> AuthorizeStudentOrAdminAsync(Guid linkId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        var link = await _repository.GetLinkByIdAsync(linkId, cancellationToken);
        if (link is null)
        {
            return Result.Success();
        }

        if (IsAdmin)
        {
            return Result.Success();
        }

        if (TryGetUserId(out var userId))
        {
            var known = await _repository.GetKnownStudentAsync(link.StudentId, cancellationToken);
            if (known?.UserId is { } studentUserId && studentUserId == userId)
            {
                return Result.Success();
            }
        }

        return Result.Failure(Forbidden);
    }
}
