using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, "Identity", IdentityDbContext.SchemaName);
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<ITokenProtector, Sha256TokenProtector>();
        services.AddScoped<ILoginAttemptThrottle, RedisLoginAttemptThrottle>();
        services.AddScoped<IIdentityNotificationService, EmailIdentityNotificationService>();
        services.AddScoped<ICommandHandler<RegisterUserCommand, Result<AuthResponse>>, RegisterUserCommandHandler>();
        services.AddScoped<ICommandHandler<LoginUserCommand, Result<AuthResponse>>, LoginUserCommandHandler>();
        services.AddScoped<ICommandHandler<RefreshTokenCommand, Result<AuthResponse>>, RefreshTokenCommandHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand, Result>, LogoutCommandHandler>();
        services.AddScoped<ICommandHandler<RequestPasswordResetCommand, Result>, RequestPasswordResetCommandHandler>();
        services.AddScoped<ICommandHandler<ResetPasswordCommand, Result>, ResetPasswordCommandHandler>();
        services.AddScoped<ICommandHandler<RequestEmailVerificationCommand, Result>, RequestEmailVerificationCommandHandler>();
        services.AddScoped<ICommandHandler<ConfirmEmailVerificationCommand, Result>, ConfirmEmailVerificationCommandHandler>();
        services.AddScoped<IQueryHandler<GetUserByIdQuery, Result<UserAccountResponse>>, GetUserByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>, GetCurrentUserQueryHandler>();
        services.AddScoped<ICommandHandler<AssignRolesCommand, Result<UserAccountResponse>>, AssignRolesCommandHandler>();
        services.AddScoped<ICommandValidator<RegisterUserCommand>, RegisterUserCommandValidator>();
        services.AddScoped<ICommandValidator<LoginUserCommand>, LoginUserCommandValidator>();
        services.AddScoped<ICommandValidator<RefreshTokenCommand>, RefreshTokenCommandValidator>();
        services.AddScoped<ICommandValidator<LogoutCommand>, LogoutCommandValidator>();
        services.AddScoped<ICommandValidator<RequestPasswordResetCommand>, RequestPasswordResetCommandValidator>();
        services.AddScoped<ICommandValidator<ResetPasswordCommand>, ResetPasswordCommandValidator>();
        services.AddScoped<ICommandValidator<RequestEmailVerificationCommand>, RequestEmailVerificationCommandValidator>();
        services.AddScoped<ICommandValidator<ConfirmEmailVerificationCommand>, ConfirmEmailVerificationCommandValidator>();
        services.AddScoped<IQueryAuthorizer<GetUserByIdQuery>, GetUserByIdQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetCurrentUserQuery>, GetCurrentUserQueryAuthorizer>();
        services.AddScoped<ICommandValidator<AssignRolesCommand>, AssignRolesCommandValidator>();
        // E-07: KVKK hesap silme
        services.AddScoped<IAccountDeletionRequestRepository, AccountDeletionRequestRepository>();
        services.AddScoped<IAccountDeletionProcessor, AccountDeletionProcessor>();
        services.AddHostedService<AccountDeletionService>();
        services.AddScoped<ICommandHandler<RequestAccountDeletionCommand, Result<AccountDeletionResponse>>, RequestAccountDeletionCommandHandler>();
        services.AddScoped<ICommandHandler<CancelAccountDeletionCommand, Result>, CancelAccountDeletionCommandHandler>();
        services.AddScoped<IQueryHandler<GetAccountDeletionQuery, Result<AccountDeletionResponse>>, GetAccountDeletionQueryHandler>();
        services.AddScoped<ICommandAuthorizer<RequestAccountDeletionCommand>, RequestAccountDeletionCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CancelAccountDeletionCommand>, CancelAccountDeletionCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetAccountDeletionQuery>, GetAccountDeletionQueryAuthorizer>();

        services.AddScoped<ICommandAuthorizer<AssignRolesCommand>, AssignRolesCommandAuthorizer>();
        return services;
    }
}
