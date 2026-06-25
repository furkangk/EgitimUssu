using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTeachersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TeachersDbContext>(configuration, "Teachers", TeachersDbContext.SchemaName);
        services.AddScoped<ITeacherProfileRepository, TeacherProfileRepository>();
        services.AddScoped<ICommandHandler<CreateTeacherProfileCommand, Result<TeacherProfileResponse>>, CreateTeacherProfileCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTeacherProfileCommand, Result<TeacherProfileResponse>>, UpdateTeacherProfileCommandHandler>();
        services.AddScoped<IQueryHandler<GetTeacherProfileByUserIdQuery, Result<TeacherProfileResponse>>, GetTeacherProfileByUserIdQueryHandler>();
        services.AddScoped<ICommandValidator<CreateTeacherProfileCommand>, CreateTeacherProfileCommandValidator>();
        services.AddScoped<ICommandValidator<UpdateTeacherProfileCommand>, UpdateTeacherProfileCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateTeacherProfileCommand>, TeacherProfileCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<UpdateTeacherProfileCommand>, TeacherProfileCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetTeacherProfileByUserIdQuery>, TeacherProfileQueryAuthorizer>();
        return services;
    }
}
