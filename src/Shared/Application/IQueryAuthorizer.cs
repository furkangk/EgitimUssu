using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Application;

public interface IQueryAuthorizer<in TQuery>
{
    Task<Result> Authorize(TQuery query, CancellationToken cancellationToken);
}
