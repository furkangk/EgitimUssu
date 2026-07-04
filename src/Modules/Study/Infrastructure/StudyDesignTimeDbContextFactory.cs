using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Study.Infrastructure;

public sealed class StudyDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<StudyDbContext>
{
    protected override string Schema => StudyDbContext.SchemaName;
}
