using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Application;

/// <summary>M01-4: Kullanıcının açık cihaz oturumlarını listeler.</summary>
public sealed record GetDeviceSessionsQuery : IQuery<Result<IReadOnlyCollection<DeviceSessionResponse>>>;

/// <summary>M01-4: Tek bir cihaz oturumunu kapatır (yalnız kendi oturumu).</summary>
/// <param name="SessionId">Kapatılacak oturum.</param>
public sealed record RevokeDeviceSessionCommand(Guid SessionId) : ICommand<Result>;

/// <summary>M01-4: Mevcut oturum <b>hariç</b> tüm oturumları kapatır.</summary>
public sealed record RevokeAllDeviceSessionsCommand : ICommand<Result<int>>;

/// <summary>Kullanıcıya gösterilen cihaz oturumu.</summary>
/// <param name="SessionId">Oturum kimliği.</param>
/// <param name="DeviceName">İstemcinin bildirdiği cihaz adı (yoksa <c>null</c>).</param>
/// <param name="CreatedOnUtc">Cihazın ilk bağlandığı an (jeton yenilense de değişmez).</param>
/// <param name="LastUsedOnUtc">Jetonun en son yenilendiği an.</param>
/// <param name="IsCurrent">Bu istek bu oturumdan mı geliyor.</param>
public sealed record DeviceSessionResponse(
    Guid SessionId,
    string? DeviceName,
    DateTime CreatedOnUtc,
    DateTime LastUsedOnUtc,
    bool IsCurrent);

/// <summary>
/// M01-4: Yalnız <b>aktif</b> oturumlar listelenir.
/// <para>
/// İptal edilmiş ve süresi geçmiş satırlar gizlenir; bunun sebebi kozmetik değildir: refresh akışı
/// jetonu döndürürken eski satırı iptal edip yenisini açıyor, yani 30 gün boyunca her yenileme bir
/// "ölü" satır bırakıyor. Hepsi gösterilseydi kullanıcı tek cihazını onlarca oturum olarak görürdü.
/// </para>
/// </summary>
public sealed class GetDeviceSessionsQueryHandler
    : IQueryHandler<GetDeviceSessionsQuery, Result<IReadOnlyCollection<DeviceSessionResponse>>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");

    private readonly IUserAccountRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public GetDeviceSessionsQueryHandler(
        IUserAccountRepository repository, ICurrentUser currentUser, IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyCollection<DeviceSessionResponse>>> Handle(
        GetDeviceSessionsQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<IReadOnlyCollection<DeviceSessionResponse>>.Failure(NotFound);
        }

        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<IReadOnlyCollection<DeviceSessionResponse>>.Failure(NotFound);
        }

        var now = _clock.UtcNow;
        var currentSessionId = _currentUser.SessionId;

        var sessions = user.RefreshSessions
            .Where(session => session.IsActive(now))
            .OrderByDescending(session => session.LastUsedOnUtc)
            .Select(session => new DeviceSessionResponse(
                session.Id,
                session.DeviceName,
                session.CreatedOnUtc,
                session.LastUsedOnUtc,
                session.Id == currentSessionId))
            .ToArray();

        return Result<IReadOnlyCollection<DeviceSessionResponse>>.Success(sessions);
    }
}

/// <summary>
/// M01-4: Tek oturum iptali. Başka kullanıcının oturumu <b>403</b> ile reddedilir — "bulunamadı"
/// değil: oturum kimlikleri tahmin edilebilir olmasa da, var/yok bilgisini sızdırmamak için
/// yetkisiz erişim ile olmayan kayıt aynı cevabı verir.
/// </summary>
public sealed class RevokeDeviceSessionCommandHandler : ICommandHandler<RevokeDeviceSessionCommand, Result>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");
    private static readonly Error Forbidden = new("shared.forbidden", "Bu oturumu kapatma yetkiniz yok.");

    private readonly IUserAccountRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public RevokeDeviceSessionCommandHandler(
        IUserAccountRepository repository, ICurrentUser currentUser, IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(RevokeDeviceSessionCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(NotFound);
        }

        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(NotFound);
        }

        // Oturum yalnız KENDİ oturumları arasında aranır; başkasınınki hiç görünmez.
        var session = user.RefreshSessions.FirstOrDefault(item => item.Id == command.SessionId);
        if (session is null)
        {
            return Result.Failure(Forbidden);
        }

        var now = _clock.UtcNow;
        if (session.IsActive(now))
        {
            session.Revoke(now);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

/// <summary>
/// M01-4: "Diğer tüm oturumları kapat".
/// <para>
/// Jetonda oturum kimliği yoksa <b>reddedilir</b>: hangi oturumun korunacağı bilinemez ve işlem
/// sessizce "hepsini kapat"a dönüşerek kullanıcıyı kendi cihazından da atardı.
/// </para>
/// </summary>
public sealed class RevokeAllDeviceSessionsCommandHandler
    : ICommandHandler<RevokeAllDeviceSessionsCommand, Result<int>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");
    private static readonly Error UnknownSession =
        new("identity.unknown_session", "Mevcut oturum belirlenemedi; once tekrar giris yapin.");

    private readonly IUserAccountRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public RevokeAllDeviceSessionsCommandHandler(
        IUserAccountRepository repository, ICurrentUser currentUser, IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(
        RevokeAllDeviceSessionsCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<int>.Failure(NotFound);
        }

        if (_currentUser.SessionId is not { } currentSessionId)
        {
            return Result<int>.Failure(UnknownSession);
        }

        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<int>.Failure(NotFound);
        }

        var now = _clock.UtcNow;
        var revoked = user.RefreshSessions
            .Count(session => session.Id != currentSessionId && session.IsActive(now));

        if (revoked > 0)
        {
            // M15-3'ten gelen mevcut davranış: korunacak oturum dışındakileri iptal eder.
            user.RevokeOtherSessions(currentSessionId, now);
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return Result<int>.Success(revoked);
    }
}
