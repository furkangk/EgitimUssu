using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLessonSessionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LessonSessionsDbContext>(configuration, "LessonSessions", LessonSessionsDbContext.SchemaName);
        services.AddScoped<ILessonSessionRepository, LessonSessionRepository>();
        services.AddScoped<ILessonSessionAccessService, LessonSessionAccessService>();
        services.AddScoped<EgitimUssu.Shared.Contracts.IStudentLastLessonDirectory, StudentLastLessonDirectory>();
        services.AddScoped<ICommandHandler<CreateLessonSessionCommand, Result<LessonSessionResponse>>, CreateLessonSessionCommandHandler>();
        services.AddScoped<ICommandHandler<CompleteLessonSessionCommand, Result<LessonSessionResponse>>, CompleteLessonSessionCommandHandler>();
        services.AddScoped<IQueryHandler<GetLessonSessionByIdQuery, Result<LessonSessionResponse>>, GetLessonSessionByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListLessonSessionsQuery, Result<IReadOnlyCollection<LessonSessionResponse>>>, ListLessonSessionsQueryHandler>();
        services.AddScoped<ICommandValidator<CreateLessonSessionCommand>, CreateLessonSessionCommandValidator>();
        services.AddScoped<ICommandValidator<CompleteLessonSessionCommand>, CompleteLessonSessionCommandValidator>();
        services.AddScoped<ICommandAuthorizer<CreateLessonSessionCommand>, LessonSessionCommandAuthorizer>();
        services.AddScoped<ICommandAuthorizer<CompleteLessonSessionCommand>, LessonSessionCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<GetLessonSessionByIdQuery>, LessonSessionCommandAuthorizer>();
        services.AddScoped<IQueryAuthorizer<ListLessonSessionsQuery>, LessonSessionCommandAuthorizer>();

        // Ö-C: profil birleştirmede kaynak öğrenciye ait ders seanslarını kanonik öğrenciye taşır.
        services.AddScoped<IIntegrationEventHandler, LessonSessionsStudentMergedHandler>();
        return services;
    }
}
