using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStudentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StudentsDbContext>(configuration, "Students", StudentsDbContext.SchemaName);
        services.AddScoped<IStudentProfileRepository, StudentProfileRepository>();
        services.AddScoped<ITeacherStudentLinkRepository, TeacherStudentLinkRepository>();
        services.AddScoped<IStudentDirectory, StudentDirectory>();
        services.AddScoped<ICommandHandler<CreateStudentProfileCommand, Result<StudentProfileResponse>>, CreateStudentProfileCommandHandler>();
        services.AddScoped<IQueryHandler<GetStudentProfileByIdQuery, Result<StudentProfileResponse>>, GetStudentProfileByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetStudentProfileByUserIdQuery, Result<StudentProfileResponse>>, GetStudentProfileByUserIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListStudentsByTeacherQuery, Result<IReadOnlyCollection<StudentProfileSummaryResponse>>>, ListStudentsByTeacherQueryHandler>();
        services.AddScoped<ICommandValidator<CreateStudentProfileCommand>, CreateStudentProfileCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateStudentProfileCommand>, CreateStudentProfileCommandAuthorizer>();
        services.AddScoped<ICommandHandler<UpdateStudentProfileCommand, Result<StudentProfileResponse>>, UpdateStudentProfileCommandHandler>();
        services.AddScoped<ICommandValidator<UpdateStudentProfileCommand>, UpdateStudentProfileCommandValidator>();
        services.AddScoped<ICommandAuthorizer<UpdateStudentProfileCommand>, UpdateStudentProfileCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentProfileByIdQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetStudentProfileByUserIdQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListStudentsByTeacherQuery>, StudentProfileQueryAuthorizer>();
        services.AddScoped<EgitimUssu.Shared.Infrastructure.Messaging.IIntegrationEventHandler, ParentChildLinkApprovedIntegrationEventHandler>();
        return services;
    }
}
