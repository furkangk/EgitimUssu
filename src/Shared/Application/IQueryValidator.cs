using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Application;

public interface IQueryValidator<in TQuery>
{
    Task<Result> Validate(TQuery query, CancellationToken cancellationToken);
}
