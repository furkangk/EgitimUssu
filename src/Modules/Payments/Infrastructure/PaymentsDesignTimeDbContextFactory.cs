using EgitimUssu.Shared.Infrastructure.Design;

namespace EgitimUssu.Modules.Payments.Infrastructure;

public sealed class PaymentsDesignTimeDbContextFactory : DesignTimeDbContextFactoryBase<PaymentsDbContext>
{
    protected override string Schema => PaymentsDbContext.SchemaName;
}
