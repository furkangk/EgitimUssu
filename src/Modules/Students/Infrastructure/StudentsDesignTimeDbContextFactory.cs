using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Students.Infrastructure;

public sealed class StudentsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<StudentsDbContext>
{
    protected override string Schema => StudentsDbContext.SchemaName;
}
