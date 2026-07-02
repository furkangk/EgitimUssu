using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Application;

public sealed class RegisterUserCommandValidator : ICommandValidator<RegisterUserCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "Kayıt bilgileri eksik veya hatalı.");

    public Task<Result> Validate(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var isValid = !string.IsNullOrWhiteSpace(command.Email)
            && !string.IsNullOrWhiteSpace(command.Password)
            && !string.IsNullOrWhiteSpace(command.FirstName)
            && !string.IsNullOrWhiteSpace(command.LastName);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class LoginUserCommandValidator : ICommandValidator<LoginUserCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "Giriş bilgileri eksik veya hatalı.");

    public Task<Result> Validate(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var isValid = !string.IsNullOrWhiteSpace(command.Email) && !string.IsNullOrWhiteSpace(command.Password);
        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class RefreshTokenCommandValidator : ICommandValidator<RefreshTokenCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "Refresh token gerekli.");
    public Task<Result> Validate(RefreshTokenCommand command, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(command.RefreshToken) ? Result.Failure(InvalidRequest) : Result.Success());
}

public sealed class LogoutCommandValidator : ICommandValidator<LogoutCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "Refresh token gerekli.");
    public Task<Result> Validate(LogoutCommand command, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(command.RefreshToken) ? Result.Failure(InvalidRequest) : Result.Success());
}

public sealed class RequestPasswordResetCommandValidator : ICommandValidator<RequestPasswordResetCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "E-posta gerekli.");
    public Task<Result> Validate(RequestPasswordResetCommand command, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(command.Email) ? Result.Failure(InvalidRequest) : Result.Success());
}

public sealed class ResetPasswordCommandValidator : ICommandValidator<ResetPasswordCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "E-posta, token ve yeni sifre gerekli.");
    public Task<Result> Validate(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var ok = !string.IsNullOrWhiteSpace(command.Email) && !string.IsNullOrWhiteSpace(command.Token) && !string.IsNullOrWhiteSpace(command.NewPassword);
        return Task.FromResult(ok ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class RequestEmailVerificationCommandValidator : ICommandValidator<RequestEmailVerificationCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "E-posta gerekli.");
    public Task<Result> Validate(RequestEmailVerificationCommand command, CancellationToken cancellationToken)
        => Task.FromResult(string.IsNullOrWhiteSpace(command.Email) ? Result.Failure(InvalidRequest) : Result.Success());
}

public sealed class ConfirmEmailVerificationCommandValidator : ICommandValidator<ConfirmEmailVerificationCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "E-posta ve token gerekli.");
    public Task<Result> Validate(ConfirmEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var ok = !string.IsNullOrWhiteSpace(command.Email) && !string.IsNullOrWhiteSpace(command.Token);
        return Task.FromResult(ok ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class AssignRolesCommandValidator : ICommandValidator<AssignRolesCommand>
{
    private static readonly Error InvalidRequest = new("identity.invalid_request", "Rol atama bilgileri eksik veya hatalı.");
    public Task<Result> Validate(AssignRolesCommand command, CancellationToken cancellationToken)
    {
        var ok = command.UserId != Guid.Empty && command.Roles is { Count: > 0 } && command.Roles.All(Enum.IsDefined);
        return Task.FromResult(ok ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

// K1: Yükseltilmiş rol ataması yalnızca Admin'e açıktır (varsayılan-deny).
public sealed class AssignRolesCommandAuthorizer : ICommandAuthorizer<AssignRolesCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public AssignRolesCommandAuthorizer(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result> Authorize(AssignRolesCommand command, CancellationToken cancellationToken)
    {
        var isAdmin = _currentUser.IsAuthenticated && _currentUser.Roles.Contains(UserRole.Admin.ToString());
        return Task.FromResult(isAdmin ? Result.Success() : Result.Failure(Forbidden));
    }
}

public sealed class GetUserByIdQueryAuthorizer : IQueryAuthorizer<GetUserByIdQuery>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu kaynağa erişim yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public GetUserByIdQueryAuthorizer(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<Result> Authorize(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Task.FromResult(Result.Failure(Forbidden));
        }

        var isAdmin = _currentUser.Roles.Contains(UserRole.Admin.ToString());
        var isSelf = Guid.TryParse(_currentUser.UserId, out var currentUserId) && currentUserId == query.UserId;
        return Task.FromResult(isAdmin || isSelf ? Result.Success() : Result.Failure(Forbidden));
    }
}
