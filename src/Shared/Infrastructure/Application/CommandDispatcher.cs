using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Application;

internal sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IServiceProvider serviceProvider, ILogger<CommandDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
        where TResponse : Result
    {
        var commandType = command.GetType();
        _logger.LogInformation("Dispatching command {CommandType}", commandType.Name);

        var invokers = DispatcherInvokerCache.ForCommand(commandType);

        foreach (var validator in _serviceProvider.GetServices(invokers.ValidatorServiceType))
        {
            var result = await invokers.Validate(validator!, command, cancellationToken);
            if (result.IsFailure)
            {
                return DispatcherFailureFactory<TResponse>.Create(result.Error);
            }
        }

        foreach (var authorizer in _serviceProvider.GetServices(invokers.AuthorizerServiceType))
        {
            var result = await invokers.Authorize(authorizer!, command, cancellationToken);
            if (result.IsFailure)
            {
                return DispatcherFailureFactory<TResponse>.Create(result.Error);
            }
        }

        var handlerInvoker = DispatcherHandlerInvokerCache<TResponse>.ForCommand(commandType);
        var handler = _serviceProvider.GetRequiredService(handlerInvoker.ServiceType);
        return await handlerInvoker.Invoke(handler, command, cancellationToken);
    }
}
