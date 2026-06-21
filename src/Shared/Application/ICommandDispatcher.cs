using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Application;

public interface ICommandDispatcher
{
    Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
        where TResponse : Result;
}
