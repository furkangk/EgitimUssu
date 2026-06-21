using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Application;

public interface IQueryDispatcher
{
    Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
        where TResponse : Result;
}
