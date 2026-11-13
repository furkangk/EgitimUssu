using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Application;

/// <summary>E-07: "Hesabımı sil" — 30 günlük bekleme süresini başlatır.</summary>
public sealed record RequestAccountDeletionCommand : ICommand<Result<AccountDeletionResponse>>;

/// <summary>E-07: Bekleyen silme talebini iptal eder (fikir değiştirme hakkı).</summary>
public sealed record CancelAccountDeletionCommand : ICommand<Result>;

/// <summary>E-07: Kullanıcının bekleyen silme talebini okur; yoksa <c>Pending=false</c> döner.</summary>
public sealed record GetAccountDeletionQuery : IQuery<Result<AccountDeletionResponse>>;

/// <summary>Silme talebinin kullanıcıya gösterilen hâli.</summary>
/// <param name="Pending">Bekleyen bir talep var mı.</param>
/// <param name="RequestedOnUtc">Talebin açıldığı an (yoksa <c>null</c>).</param>
/// <param name="ScheduledForUtc">Anonimleştirmenin uygulanacağı an (yoksa <c>null</c>).</param>
public sealed record AccountDeletionResponse(bool Pending, DateTime? RequestedOnUtc, DateTime? ScheduledForUtc);

/// <summary>E-07: Hesap silme taleplerinin kalıcılığı.</summary>
public interface IAccountDeletionRequestRepository
{
    /// <summary>Kullanıcının bekleyen talebi (yoksa <c>null</c>).</summary>
    Task<AccountDeletionRequest?> GetPendingAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(AccountDeletionRequest request, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class RequestAccountDeletionCommandHandler
    : ICommandHandler<RequestAccountDeletionCommand, Result<AccountDeletionResponse>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");

    private readonly IAccountDeletionRequestRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public RequestAccountDeletionCommandHandler(
        IAccountDeletionRequestRepository repository,
        ICurrentUser currentUser,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<AccountDeletionResponse>> Handle(
        RequestAccountDeletionCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<AccountDeletionResponse>.Failure(NotFound);
        }

        // İkinci kez talep açmak hata değildir: kullanıcı butona iki kez basmış olabilir. Var olan
        // talep döndürülür — yeni bir talep açmak bekleme süresini sıfırlar ve silmeyi geciktirirdi.
        var existing = await _repository.GetPendingAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return Result<AccountDeletionResponse>.Success(Describe(existing));
        }

        var request = new AccountDeletionRequest(_idGenerator.New(), userId, _clock.UtcNow);
        await _repository.AddAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<AccountDeletionResponse>.Success(Describe(request));
    }

    internal static AccountDeletionResponse Describe(AccountDeletionRequest request)
        => new(true, request.RequestedOnUtc, request.ScheduledForUtc);
}

public sealed class CancelAccountDeletionCommandHandler : ICommandHandler<CancelAccountDeletionCommand, Result>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");
    private static readonly Error NoPendingRequest =
        new("identity.deletion_request_not_found", "Bekleyen bir hesap silme talebiniz yok.");

    private readonly IAccountDeletionRequestRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public CancelAccountDeletionCommandHandler(
        IAccountDeletionRequestRepository repository, ICurrentUser currentUser, IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(CancelAccountDeletionCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(NotFound);
        }

        var request = await _repository.GetPendingAsync(userId, cancellationToken);
        if (request is null)
        {
            return Result.Failure(NoPendingRequest);
        }

        request.Cancel(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetAccountDeletionQueryHandler : IQueryHandler<GetAccountDeletionQuery, Result<AccountDeletionResponse>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");

    private readonly IAccountDeletionRequestRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetAccountDeletionQueryHandler(IAccountDeletionRequestRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<AccountDeletionResponse>> Handle(
        GetAccountDeletionQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<AccountDeletionResponse>.Failure(NotFound);
        }

        var request = await _repository.GetPendingAsync(userId, cancellationToken);
        return Result<AccountDeletionResponse>.Success(
            request is null
                ? new AccountDeletionResponse(false, null, null)
                : RequestAccountDeletionCommandHandler.Describe(request));
    }
}
