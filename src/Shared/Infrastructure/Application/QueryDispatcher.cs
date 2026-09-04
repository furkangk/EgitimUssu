using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Shared.Infrastructure.Application;

internal sealed class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryDispatcher> _logger;

    public QueryDispatcher(IServiceProvider serviceProvider, ILogger<QueryDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
        where TResponse : Result
    {
        var queryType = query.GetType();
        _logger.LogInformation("Dispatching query {QueryType}", queryType.Name);

        var invokers = DispatcherInvokerCache.ForQuery(queryType);

        foreach (var validator in _serviceProvider.GetServices(invokers.ValidatorServiceType))
        {
            var result = await invokers.Validate(validator!, query, cancellationToken);
            if (result.IsFailure)
            {
                return DispatcherFailureFactory<TResponse>.Create(result.Error);
            }
        }

        foreach (var authorizer in _serviceProvider.GetServices(invokers.AuthorizerServiceType))
        {
            var result = await invokers.Authorize(authorizer!, query, cancellationToken);
            if (result.IsFailure)
            {
                return DispatcherFailureFactory<TResponse>.Create(result.Error);
            }
        }

        var handlerInvoker = DispatcherHandlerInvokerCache<TResponse>.ForQuery(queryType);
        var handler = _serviceProvider.GetRequiredService(handlerInvoker.ServiceType);
        return await handlerInvoker.Invoke(handler, query, cancellationToken);
    }
}
