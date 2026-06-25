using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Application;
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

public sealed record AuthResponse(Guid UserId, string Email, string FullName, IReadOnlyCollection<string> Roles, string AccessToken, DateTime ExpiresAtUtc, string RefreshToken);
public sealed record UserAccountResponse(Guid UserId, string Email, string FirstName, string LastName, string? PhoneNumber, string Status, bool IsEmailConfirmed, bool IsProfileVerified, IReadOnlyCollection<string> Roles, DateTime CreatedOnUtc, DateTime UpdatedOnUtc);

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken);
    Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordHasher { string Hash(string password); bool Verify(string hashedPassword, string providedPassword); }
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
    private readonly IUserAccountRepository _repository; private readonly IPasswordHasher _passwordHasher; private readonly ITokenIssuer _tokenIssuer; private readonly ITokenProtector _tokenProtector; private readonly IIdentityNotificationService _notificationService; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public RegisterUserCommandHandler(IUserAccountRepository repository, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer, ITokenProtector tokenProtector, IIdentityNotificationService notificationService, IIdGenerator idGenerator, IClock clock) { _repository = repository; _passwordHasher = passwordHasher; _tokenIssuer = tokenIssuer; _tokenProtector = tokenProtector; _notificationService = notificationService; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<AuthResponse>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8) return Result<AuthResponse>.Failure(InvalidPassword);
        if (command.Roles.Count == 0) return Result<AuthResponse>.Failure(MissingRole);
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
        await _repository.SaveChangesAsync(cancellationToken);
        await _notificationService.SendEmailVerificationAsync(user.Email, verificationToken, cancellationToken);
        var access = _tokenIssuer.Issue(user);
        return Result<AuthResponse>.Success(IdentityMappings.ToAuthResponse(user, access.AccessToken, access.ExpiresAtUtc, refreshToken));
    }
}

public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, Result<AuthResponse>>
{
    private static readonly Error InvalidCredentials = new("identity.invalid_credentials", "E-posta veya sifre hatali.");
    private static readonly Error UserInactive = new("identity.user_inactive", "Kullanici hesabi aktif degil.");
    private readonly IUserAccountRepository _repository; private readonly IPasswordHasher _passwordHasher; private readonly ITokenIssuer _tokenIssuer; private readonly ITokenProtector _tokenProtector; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public LoginUserCommandHandler(IUserAccountRepository repository, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer, ITokenProtector tokenProtector, IIdGenerator idGenerator, IClock clock) { _repository = repository; _passwordHasher = passwordHasher; _tokenIssuer = tokenIssuer; _tokenProtector = tokenProtector; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<AuthResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByEmailAsync(command.Email.Trim().ToUpperInvariant(), cancellationToken);
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, command.Password)) return Result<AuthResponse>.Failure(InvalidCredentials);
        if (user.Status is not UserAccountStatus.Active and not UserAccountStatus.PendingActivation) return Result<AuthResponse>.Failure(UserInactive);
        var now = _clock.UtcNow;
        var refreshToken = _tokenProtector.GenerateToken();
        user.RefreshSessions.Add(new RefreshTokenSession(_idGenerator.New(), user.Id, _tokenProtector.Hash(refreshToken), command.DeviceName, now, now.AddDays(30)));
        await _repository.SaveChangesAsync(cancellationToken);
        var access = _tokenIssuer.Issue(user);
        return Result<AuthResponse>.Success(IdentityMappings.ToAuthResponse(user, access.AccessToken, access.ExpiresAtUtc, refreshToken));
    }
}

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private static readonly Error InvalidRefresh = new("identity.invalid_refresh_token", "Refresh token gecersiz veya suresi dolmus.");
    private readonly IUserAccountRepository _repository; private readonly ITokenIssuer _tokenIssuer; private readonly ITokenProtector _tokenProtector; private readonly IIdGenerator _idGenerator; private readonly IClock _clock;
    public RefreshTokenCommandHandler(IUserAccountRepository repository, ITokenIssuer tokenIssuer, ITokenProtector tokenProtector, IIdGenerator idGenerator, IClock clock) { _repository = repository; _tokenIssuer = tokenIssuer; _tokenProtector = tokenProtector; _idGenerator = idGenerator; _clock = clock; }
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hashed = _tokenProtector.Hash(command.RefreshToken.Trim());
        var user = await _repository.GetByRefreshTokenHashAsync(hashed, cancellationToken);
        var session = user?.RefreshSessions.FirstOrDefault(x => x.RefreshTokenHash == hashed);
        if (user is null || session is null || !session.IsActive(now)) return Result<AuthResponse>.Failure(InvalidRefresh);
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
        await _repository.SaveChangesAsync(cancellationToken);
        await _notificationService.SendPasswordResetAsync(user.Email, token, cancellationToken);
        return Result.Success();
    }
}

public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private static readonly Error InvalidToken = new("identity.invalid_password_reset_token", "Sifre sifirlama tokeni gecersiz.");
    private static readonly Error InvalidPassword = new("identity.invalid_password", "Sifre en az 8 karakter olmalidir.");
    private readonly IUserAccountRepository _repository; private readonly ITokenProtector _tokenProtector; private readonly IPasswordHasher _passwordHasher; private readonly IClock _clock;
    public ResetPasswordCommandHandler(IUserAccountRepository repository, ITokenProtector tokenProtector, IPasswordHasher passwordHasher, IClock clock) { _repository = repository; _tokenProtector = tokenProtector; _passwordHasher = passwordHasher; _clock = clock; }
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
        await _repository.SaveChangesAsync(cancellationToken);
        await _notificationService.SendEmailVerificationAsync(user.Email, token, cancellationToken);
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

internal static class IdentityMappings
{
    public static AuthResponse ToAuthResponse(UserAccount user, string accessToken, DateTime expiresAtUtc, string refreshToken)
        => new(user.Id, user.Email, $"{user.FirstName} {user.LastName}".Trim(), user.RoleMemberships.Select(role => role.Role.ToString()).ToArray(), accessToken, expiresAtUtc, refreshToken);
}
