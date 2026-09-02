using EgitimUssu.Modules.Settings.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.Settings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SettingsDbContext>(configuration, "Settings", SettingsDbContext.SchemaName);

        services.AddScoped<IUserSettingRepository, UserSettingRepository>();
        services.AddScoped<IStudentPrivacyDirectory, StudentPrivacyDirectory>();
        services.AddScoped<IUserNotificationPreferences, UserNotificationPreferencesDirectory>();
        services.AddScoped<ISessionPolicyDirectory, SessionPolicyDirectory>();

        services.AddScoped<ICommandHandler<SetStudySharingCommand, Result<StudySharingResponse>>, SetStudySharingCommandHandler>();
        services.AddScoped<ICommandValidator<SetStudySharingCommand>, SetStudySharingCommandValidator>();
        services.AddScoped<ICommandAuthorizer<SetStudySharingCommand>, SettingsAuthorizer>();

        services.AddScoped<IQueryHandler<GetUserSettingsQuery, Result<UserSettingsResponse>>, GetUserSettingsQueryHandler>();
        services.AddScoped<IQueryAuthorizer<GetUserSettingsQuery>, GetUserSettingsQueryAuthorizer>();
        services.AddScoped<ICommandHandler<UpdateUserSettingsCommand, Result<UserSettingsResponse>>, UpdateUserSettingsCommandHandler>();
        services.AddScoped<ICommandValidator<UpdateUserSettingsCommand>, UpdateUserSettingsCommandValidator>();
        services.AddScoped<ICommandAuthorizer<UpdateUserSettingsCommand>, UpdateUserSettingsCommandAuthorizer>();

        return services;
    }
}
