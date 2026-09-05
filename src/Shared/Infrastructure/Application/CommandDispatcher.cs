using System.Diagnostics;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Observability;
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

        // C-04: Trace'te HTTP span'ı ile EF/Npgsql span'ları arasında bir boşluk vardı; hangi
        // komutun koştuğu ve doğrulama/yetki adımlarının ne kadar sürdüğü görünmüyordu.
        using var activity = EgitimUssuActivitySource.Instance.StartActivity(
            $"command {commandType.Name}",
            ActivityKind.Internal);

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
