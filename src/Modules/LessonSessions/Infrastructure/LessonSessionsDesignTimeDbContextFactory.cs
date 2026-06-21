using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

public sealed class LessonSessionsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<LessonSessionsDbContext>
{
    protected override string Schema => LessonSessionsDbContext.SchemaName;
}
