using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Application;

public sealed record RegisterUserCommand(string Email, string Password, string FirstName, string LastName, string? PhoneNumber, IReadOnlyCollection<UserRole> Roles) : ICommand<Result<AuthResponse>>, IAllowAnonymous;
public sealed record LoginUserCommand(string Email, string Password, string? DeviceName) : ICommand<Result<AuthResponse>>, IAllowAnonymous;
public sealed record RefreshTokenCommand(string RefreshToken, string? DeviceName) : ICommand<Result<AuthResponse>>, IAllowAnonymous;
public sealed record LogoutCommand(string RefreshToken) : ICommand<Result>, IAllowAnonymous;
public sealed record RequestPasswordResetCommand(string Email) : ICommand<Result>, IAllowAnonymous;
public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand<Result>, IAllowAnonymous;
public sealed record RequestEmailVerificationCommand(string Email) : ICommand<Result>, IAllowAnonymous;
public sealed record ConfirmEmailVerificationCommand(string Email, string Token) : ICommand<Result>, IAllowAnonymous;
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<Result<UserAccountResponse>>;
public sealed record AssignRolesCommand(Guid UserId, IReadOnlyCollection<UserRole> Roles) : ICommand<Result<UserAccountResponse>>;

public sealed record AuthResponse(Guid UserId, string Email, string FullName, IReadOnlyCollection<string> Roles, string AccessToken, DateTime ExpiresAtUtc, string RefreshToken);
public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserResponse>>;

public sealed record CurrentUserResponse(Guid UserId, string Email, string FullName, IReadOnlyCollection<string> Roles, bool IsEmailConfirmed, string Status);

public sealed record UserAccountResponse(Guid UserId, string Email, string FirstName, string LastName, string? PhoneNumber, string Status, bool IsEmailConfirmed, bool IsProfileVerified, IReadOnlyCollection<string> Roles, DateTime CreatedOnUtc, DateTime UpdatedOnUtc);

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken);
    Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>A-08: Kullanıcının işlevsiz kalmış oturum/güvenlik token'ı satırlarını siler.</summary>
    Task PurgeStaleCredentialsAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// A-06: Bir kullanıcının dolaşımdaki <b>erişim token'larını</b> topluca geçersiz kılar.
/// <para>
/// Refresh oturumlarını iptal etmek yetmiyordu: parola sıfırlandıktan sonra bile saldırganın
/// elindeki erişim token'ı ömrünü (60 dk) tamamlayana kadar geçerli kalıyordu. Aynı boşluk rol
/// değişiminde de vardı — yetkisi alınan bir yönetici, roller token claim'inden okunduğu için
/// bir saat daha yönetici kalıyordu.
/// </para>
/// <para>Port Application'da, uygulaması Infrastructure'da (katman kuralı: Application → Infrastructure yasak).</para>
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>Bu andan önce verilmiş tüm erişim token'larını reddedilir hâle getirir.</summary>
    /// <param name="userId">Kullanıcı kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>İşlem görevi.</returns>
    Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IPasswordHasher { string Hash(string password); bool Verify(string hashedPassword, string providedPassword); }
// Y4: Ardışık başarısız giriş denemelerine karşı hesap-bazlı kilit (brute-force koruması). Redis destekli, fail-open.
public interface ILoginAttemptThrottle
{
    Task<bool> IsLockedAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task RecordFailureAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task ResetAsync(string normalizedEmail, CancellationToken cancellationToken);
}
public interface ITokenIssuer { (string AccessToken, DateTime ExpiresAtUtc) Issue(UserAccount userAccount); }
public interface ITokenProtector { string Hash(string token); string GenerateToken(); }
public interface IIdentityNotificationService
{
    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken);
    Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken);
}

public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<AuthResponse>>
{
    private static readonly Error DuplicateEmail = new("identity.duplicate_email", "Bu e-posta ile kayitli bir kullanici zaten var.");
    private static readonly Error InvalidPassword = new("identity.invalid_password", "Sifre en az 8 karakter olmalidir.");
    private static readonly Error MissingRole = new("identity.missing_role", "En az bir kullanici rolu secilmelidir.");
    private static readonly Error RoleNotSelfAssignable = new("identity.role_not_self_assignable", "Bu rol kayit sirasinda secilemez.");

    // K1: Self-register'da yalnizca bu roller istemciden secilebilir. Admin gibi yukseltilmis roller
    // yalnizca yetkili bir yoneticinin cagirdigi AssignRolesCommand ile atanir (varsayilan-deny).
    private static readonly IReadOnlySet<UserRole> SelfAssignableRoles =
        new HashSet<UserRole> { UserRole.Teacher, UserRole.Student, UserRole.Parent };
    private readonly IUserAccountRepository _repository; private readonly IPasswordHasher _passwordHasher; private readonly ITokenIssuer _tokenIssuer; private readonly ITokenProtector _tokenProtector; private readonly IIdentityNotificationService _notificationService; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public RegisterUserCommandHandler(IUserAccountRepository repository, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer, ITokenProtector tokenProtector, IIdentityNotificationService notificationService, IIdGenerator idGenerator, IClock clock) { _repository = repository; _passwordHasher = passwordHasher; _tokenIssuer = tokenIssuer; _tokenProtector = tokenProtector; _notificationService = notificationService; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8) return Result<AuthResponse>.Failure(InvalidPassword);
        if (command.Roles.Count == 0) return Result<AuthResponse>.Failure(MissingRole);
        if (command.Roles.Any(role => !SelfAssignableRoles.Contains(role))) return Result<AuthResponse>.Failure(RoleNotSelfAssignable);
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        if (await _repository.GetByEmailAsync(normalizedEmail, cancellationToken) is not null) return Result<AuthResponse>.Failure(DuplicateEmail);
        var now = _clock.UtcNow;
        var user = new UserAccount(_idGenerator.New(), command.Email.Trim(), normalizedEmail, _passwordHasher.Hash(command.Password), command.FirstName.Trim(), command.LastName.Trim(), string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber.Trim(), UserAccountStatus.Active, false, false, now);
        foreach (var role in command.Roles.Distinct()) user.RoleMemberships.Add(new UserRoleMembership(_idGenerator.New(), user.Id, role, now));
        var refreshToken = _tokenProtector.GenerateToken();
        user.RefreshSessions.Add(new RefreshTokenSession(_idGenerator.New(), user.Id, _tokenProtector.Hash(refreshToken), null, now, now.AddDays(30)));
        var verificationToken = _tokenProtector.GenerateToken();
        user.SecurityTokens.Add(new UserSecurityToken(_idGenerator.New(), user.Id, SecurityTokenPurpose.EmailVerification, _tokenProtector.Hash(verificationToken), now, now.AddHours(24)));
        await _repository.AddAsync(user, cancellationToken);
        // A-04: E-posta, kullanıcı yazımıyla AYNI transaction'da kuyruğa girer — bu yüzden
        // SaveChanges'ten ÖNCE. Eskiden kayıt commit edildikten sonra SMTP'ye gidiliyordu:
        // sağlayıcı düşükse kullanıcı oluşmuş ama istek 500 dönüyor, tekrar deneme
        // duplicate_email veriyordu. Artık ya ikisi de yazılır ya hiçbiri.
        await _notificationService.SendEmailVerificationAsync(user.Email, verificationToken, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        var access = _tokenIssuer.Issue(user);
        return Result<AuthResponse>.Success(IdentityMappings.ToAuthResponse(user, access.AccessToken, access.ExpiresAtUtc, refreshToken));
    }
}

public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, Result<AuthResponse>>
{
    private static readonly Error InvalidCredentials = new("identity.invalid_credentials", "E-posta veya sifre hatali.");
    private static readonly Error UserInactive = new("identity.user_inactive", "Kullanici hesabi aktif degil.");
    private static readonly Error TooManyAttempts = new("identity.too_many_attempts", "Cok fazla basarisiz giris denemesi. Lutfen bir sure sonra tekrar deneyin.");
    private readonly IUserAccountRepository _repository; private readonly IPasswordHasher _passwordHasher; private readonly ITokenIssuer _tokenIssuer; private readonly ITokenProtector _tokenProtector; private readonly ILoginAttemptThrottle _throttle; private readonly ISessionPolicyDirectory _sessionPolicy; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public LoginUserCommandHandler(IUserAccountRepository repository, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer, ITokenProtector tokenProtector, ILoginAttemptThrottle throttle, ISessionPolicyDirectory sessionPolicy, IIdGenerator idGenerator, IClock clock) { _repository = repository; _passwordHasher = passwordHasher; _tokenIssuer = tokenIssuer; _tokenProtector = tokenProtector; _throttle = throttle; _sessionPolicy = sessionPolicy; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<AuthResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        // Y4: Ardışık başarısızlıkta hesap geçici kilitli — kimlik doğrulamadan önce reddet (brute-force koruması).
        if (await _throttle.IsLockedAsync(normalizedEmail, cancellationToken)) return Result<AuthResponse>.Failure(TooManyAttempts);
        var user = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            await _throttle.RecordFailureAsync(normalizedEmail, cancellationToken);
            return Result<AuthResponse>.Failure(InvalidCredentials);
        }
        if (user.Status is not UserAccountStatus.Active and not UserAccountStatus.PendingActivation) return Result<AuthResponse>.Failure(UserInactive);
        await _throttle.ResetAsync(normalizedEmail, cancellationToken);
        var now = _clock.UtcNow;
        var refreshToken = _tokenProtector.GenerateToken();
        var session = new RefreshTokenSession(_idGenerator.New(), user.Id, _tokenProtector.Hash(refreshToken), command.DeviceName, now, now.AddDays(30));
        user.RefreshSessions.Add(session);
        // M15-3: "Diğer oturumları sonlandır" seçiliyse önceki refresh oturumları iptal edilir.
        if (await _sessionPolicy.ShouldTerminateOtherSessionsAsync(user.Id, cancellationToken))
        {
            user.RevokeOtherSessions(session.Id, now);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        // A-08: Oturum/güvenlik token'ı satırları hiç silinmiyordu; budama girişte, yalnız bu
        // kullanıcının satırları için yapılır (okuma zaten filtreli Include'larla sınırlı).
        await _repository.PurgeStaleCredentialsAsync(user.Id, cancellationToken);
        var access = _tokenIssuer.Issue(user);
        return Result<AuthResponse>.Success(IdentityMappings.ToAuthResponse(user, access.AccessToken, access.ExpiresAtUtc, refreshToken));
    }
}

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private static readonly Error InvalidRefresh = new("identity.invalid_refresh_token", "Refresh token gecersiz veya suresi dolmus.");
    private readonly IUserAccountRepository _repository; private readonly ITokenIssuer _tokenIssuer; private readonly ITokenProtector _tokenProtector; private readonly ITokenRevocationService _tokenRevocation; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public RefreshTokenCommandHandler(IUserAccountRepository repository, ITokenIssuer tokenIssuer, ITokenProtector tokenProtector, ITokenRevocationService tokenRevocation, IIdGenerator idGenerator, IClock clock) { _repository = repository; _tokenIssuer = tokenIssuer; _tokenProtector = tokenProtector; _tokenRevocation = tokenRevocation; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hashed = _tokenProtector.Hash(command.RefreshToken.Trim());
        var user = await _repository.GetByRefreshTokenHashAsync(hashed, cancellationToken);
        var session = user?.RefreshSessions.FirstOrDefault(x => x.RefreshTokenHash == hashed);
        if (user is null || session is null) return Result<AuthResponse>.Failure(InvalidRefresh);

        // A-15: Zaten iptal edilmiş bir refresh token'ın tekrar sunulması ele geçirme sinyalidir.
        // Rotasyon her yenilemede eskisini iptal ettiği için meşru istemci onu bir daha göndermez;
        // gelen istek ya çalınmış token'ı kullanıyor ya da meşru istemci çalınmış olanın ardından
        // geliyordur. Hangisi olduğu ayırt edilemeyeceğinden tüm oturumlar kapatılır ve dolaşımdaki
        // erişim token'ları iptal edilir — kullanıcı yeniden giriş yapar. Eskiden yalnız 401 dönülüyor,
        // olay normal bir oturum sonu gibi görünüyordu.
        if (session.RevokedOnUtc is not null)
        {
            user.RevokeOtherSessions(session.Id, now);
            await _repository.SaveChangesAsync(cancellationToken);
            await _tokenRevocation.RevokeAllAccessTokensAsync(user.Id, cancellationToken);
            return Result<AuthResponse>.Failure(InvalidRefresh);
        }

        if (!session.IsActive(now)) return Result<AuthResponse>.Failure(InvalidRefresh);
        session.Revoke(now);
        var newRefresh = _tokenProtector.GenerateToken();
        user.RefreshSessions.Add(new RefreshTokenSession(_idGenerator.New(), user.Id, _tokenProtector.Hash(newRefresh), command.DeviceName, now, now.AddDays(30)));
        await _repository.SaveChangesAsync(cancellationToken);
        var access = _tokenIssuer.Issue(user);
        return Result<AuthResponse>.Success(IdentityMappings.ToAuthResponse(user, access.AccessToken, access.ExpiresAtUtc, newRefresh));
    }
}

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, Result>
{
    private readonly IUserAccountRepository _repository; private readonly ITokenProtector _tokenProtector; private readonly IClock _clock;
    public LogoutCommandHandler(IUserAccountRepository repository, ITokenProtector tokenProtector, IClock clock) { _repository = repository; _tokenProtector = tokenProtector; _clock = clock; }
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var hash = _tokenProtector.Hash(command.RefreshToken.Trim());
        var user = await _repository.GetByRefreshTokenHashAsync(hash, cancellationToken);
        var session = user?.RefreshSessions.FirstOrDefault(x => x.RefreshTokenHash == hash);
        if (session is not null) { session.Revoke(_clock.UtcNow); await _repository.SaveChangesAsync(cancellationToken); }
        return Result.Success();
    }
}

public sealed class RequestPasswordResetCommandHandler : ICommandHandler<RequestPasswordResetCommand, Result>
{
    private readonly IUserAccountRepository _repository; private readonly ITokenProtector _tokenProtector; private readonly IIdentityNotificationService _notificationService; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public RequestPasswordResetCommandHandler(IUserAccountRepository repository, ITokenProtector tokenProtector, IIdentityNotificationService notificationService, IIdGenerator idGenerator, IClock clock) { _repository = repository; _tokenProtector = tokenProtector; _notificationService = notificationService; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByEmailAsync(command.Email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null) return Result.Success();
        var now = _clock.UtcNow;
        var token = _tokenProtector.GenerateToken();
        user.SecurityTokens.Add(new UserSecurityToken(_idGenerator.New(), user.Id, SecurityTokenPurpose.PasswordReset, _tokenProtector.Hash(token), now, now.AddHours(1)));
        // A-04: Kuyruga alma SaveChanges'ten ONCE — token ile e-posta ayni transaction'da yazilir.
        await _notificationService.SendPasswordResetAsync(user.Email, token, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private static readonly Error InvalidToken = new("identity.invalid_password_reset_token", "Sifre sifirlama tokeni gecersiz.");
    private static readonly Error InvalidPassword = new("identity.invalid_password", "Sifre en az 8 karakter olmalidir.");
    private readonly IUserAccountRepository _repository; private readonly ITokenProtector _tokenProtector; private readonly IPasswordHasher _passwordHasher; private readonly ITokenRevocationService _tokenRevocation; private readonly IClock _clock;
    public ResetPasswordCommandHandler(IUserAccountRepository repository, ITokenProtector tokenProtector, IPasswordHasher passwordHasher, ITokenRevocationService tokenRevocation, IClock clock) { _repository = repository; _tokenProtector = tokenProtector; _passwordHasher = passwordHasher; _tokenRevocation = tokenRevocation; _clock = clock; }
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 8) return Result.Failure(InvalidPassword);
        var user = await _repository.GetByEmailAsync(command.Email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null) return Result.Failure(InvalidToken);
        var now = _clock.UtcNow;
        var hash = _tokenProtector.Hash(command.Token.Trim());
        var token = user.SecurityTokens.FirstOrDefault(x => x.Purpose == SecurityTokenPurpose.PasswordReset && x.TokenHash == hash && x.IsUsable(now));
        if (token is null) return Result.Failure(InvalidToken);
        token.MarkUsed(now);
        user.UpdatePassword(_passwordHasher.Hash(command.NewPassword), now);
        foreach (var session in user.RefreshSessions.Where(x => x.IsActive(now))) session.Revoke(now);
        await _repository.SaveChangesAsync(cancellationToken);
        // A-06: Refresh oturumlarını kapatmak yetmez — dolaşımdaki erişim token'ı 60 dk daha
        // geçerli kalıyordu. Hesabı ele geçirilen kullanıcı parolasını sıfırlasa bile saldırgan
        // bir saat boyunca içeride kalabiliyordu.
        await _tokenRevocation.RevokeAllAccessTokensAsync(user.Id, cancellationToken);
        return Result.Success();
    }
}

public sealed class RequestEmailVerificationCommandHandler : ICommandHandler<RequestEmailVerificationCommand, Result>
{
    private readonly IUserAccountRepository _repository; private readonly ITokenProtector _tokenProtector; private readonly IIdentityNotificationService _notificationService; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public RequestEmailVerificationCommandHandler(IUserAccountRepository repository, ITokenProtector tokenProtector, IIdentityNotificationService notificationService, IIdGenerator idGenerator, IClock clock) { _repository = repository; _tokenProtector = tokenProtector; _notificationService = notificationService; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result> Handle(RequestEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByEmailAsync(command.Email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || user.IsEmailConfirmed) return Result.Success();
        var now = _clock.UtcNow;
        var token = _tokenProtector.GenerateToken();
        user.SecurityTokens.Add(new UserSecurityToken(_idGenerator.New(), user.Id, SecurityTokenPurpose.EmailVerification, _tokenProtector.Hash(token), now, now.AddHours(24)));
        // A-04: Kuyruga alma SaveChanges'ten ONCE — token ile e-posta ayni transaction'da yazilir.
        await _notificationService.SendEmailVerificationAsync(user.Email, token, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ConfirmEmailVerificationCommandHandler : ICommandHandler<ConfirmEmailVerificationCommand, Result>
{
    private static readonly Error InvalidToken = new("identity.invalid_email_verification_token", "E-posta dogrulama tokeni gecersiz.");
    private readonly IUserAccountRepository _repository; private readonly ITokenProtector _tokenProtector; private readonly IClock _clock;
    public ConfirmEmailVerificationCommandHandler(IUserAccountRepository repository, ITokenProtector tokenProtector, IClock clock) { _repository = repository; _tokenProtector = tokenProtector; _clock = clock; }
    public async Task<Result> Handle(ConfirmEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByEmailAsync(command.Email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null) return Result.Failure(InvalidToken);
        var now = _clock.UtcNow;
        var hash = _tokenProtector.Hash(command.Token.Trim());
        var token = user.SecurityTokens.FirstOrDefault(x => x.Purpose == SecurityTokenPurpose.EmailVerification && x.TokenHash == hash && x.IsUsable(now));
        if (token is null) return Result.Failure(InvalidToken);
        token.MarkUsed(now);
        user.ConfirmEmail(now);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, Result<UserAccountResponse>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");
    private readonly IUserAccountRepository _repository;
    public GetUserByIdQueryHandler(IUserAccountRepository repository) { _repository = repository; }
    public async Task<Result<UserAccountResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null) return Result<UserAccountResponse>.Failure(NotFound);
        return Result<UserAccountResponse>.Success(new UserAccountResponse(user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.Status.ToString(), user.IsEmailConfirmed, user.IsProfileVerified, user.RoleMemberships.Select(role => role.Role.ToString()).ToArray(), user.CreatedOnUtc, user.UpdatedOnUtc));
    }
}


// M01-1: Oturum acmis kullanicinin kendi bilgisi. Kimlik mobilde token'dan degil, bu uctan okunur.
public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");
    private readonly IUserAccountRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetCurrentUserQueryHandler(IUserAccountRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result<CurrentUserResponse>.Failure(NotFound);
        }

        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<CurrentUserResponse>.Failure(NotFound);
        }

        return Result<CurrentUserResponse>.Success(new CurrentUserResponse(
            user.Id,
            user.Email,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.RoleMemberships.Select(membership => membership.Role.ToString()).ToArray(),
            user.IsEmailConfirmed,
            user.Status.ToString()));
    }
}

public sealed class AssignRolesCommandHandler : ICommandHandler<AssignRolesCommand, Result<UserAccountResponse>>
{
    private static readonly Error NotFound = new("identity.user_not_found", "Kullanici bulunamadi.");
    private static readonly Error MissingRole = new("identity.missing_role", "En az bir kullanici rolu secilmelidir.");
    private readonly IUserAccountRepository _repository; private readonly ITokenRevocationService _tokenRevocation; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public AssignRolesCommandHandler(IUserAccountRepository repository, ITokenRevocationService tokenRevocation, IIdGenerator idGenerator, IClock clock) { _repository = repository; _tokenRevocation = tokenRevocation; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<UserAccountResponse>> Handle(AssignRolesCommand command, CancellationToken cancellationToken)
    {
        if (command.Roles.Count == 0) return Result<UserAccountResponse>.Failure(MissingRole);
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null) return Result<UserAccountResponse>.Failure(NotFound);
        var now = _clock.UtcNow;
        var existing = user.RoleMemberships.Select(membership => membership.Role).ToHashSet();
        foreach (var role in command.Roles.Distinct().Where(role => !existing.Contains(role)))
        {
            user.RoleMemberships.Add(new UserRoleMembership(_idGenerator.New(), user.Id, role, now));
        }
        await _repository.SaveChangesAsync(cancellationToken);
        // A-06: Roller JWT claim'inden okunur; yeni yetki (ya da alınan yetki) dolaşımdaki token
        // ömrünü tamamlayana kadar yansımıyordu — yetkisi alınan bir yönetici bir saat daha
        // yönetici kalıyordu. Kullanıcı bir sonraki istekte yeniden token alır.
        await _tokenRevocation.RevokeAllAccessTokensAsync(user.Id, cancellationToken);
        return Result<UserAccountResponse>.Success(new UserAccountResponse(user.Id, user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.Status.ToString(), user.IsEmailConfirmed, user.IsProfileVerified, user.RoleMemberships.Select(role => role.Role.ToString()).ToArray(), user.CreatedOnUtc, user.UpdatedOnUtc));
    }
}

internal static class IdentityMappings
{
    public static AuthResponse ToAuthResponse(UserAccount user, string accessToken, DateTime expiresAtUtc, string refreshToken)
        => new(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.RoleMemberships.Select(role => role.Role.ToString()).ToArray(), accessToken, expiresAtUtc, refreshToken);
}
