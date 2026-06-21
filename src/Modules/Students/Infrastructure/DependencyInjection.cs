using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStudentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudentsDbContext>(configuration, "Students", StudentsDbContext.SchemaName);
        services.AddScoped<IStudentProfileRepository, StudentProfileRepository>();
        services.AddScoped<ICommandHandler<CreateStudentProfileCommand, Result<StudentProfileResponse>>, CreateStudentProfileCommandHandler>();
        services.AddScoped<IQueryHandler<GetStudentProfileByIdQuery, Result<StudentProfileResponse>>, GetStudentProfileByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetStudentProfileByUserIdQuery, Result<StudentProfileResponse>>, GetStudentProfileByUserIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListStudentsByTeacherQuery, Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>, ListStudentsByTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateStudentProfileCommand>, CreateStudentProfileCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateStudentProfileCommand>, CreateStudentProfileCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentProfileByIdQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentProfileByUserIdQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListStudentsByTeacherQuery>, StudentProfileQueryAuthorizer>();
        return services;
    }
}
