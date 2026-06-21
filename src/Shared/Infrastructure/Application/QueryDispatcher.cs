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

        foreach (var validator in _serviceProvider.GetServices(typeof(IQueryValidator<>).MakeGenericType(queryType)).Cast<object>())
        {
            var result = await ((dynamic)validator).Validate((dynamic)query, cancellationToken);
            if (result.IsFailure)
            {
                return (TResponse)(dynamic)CreateFailureResult<TResponse>(result.Error);
            }
        }

        foreach (var authorizer in _serviceProvider.GetServices(typeof(IQueryAuthorizer<>).MakeGenericType(queryType)).Cast<object>())
        {
            var result = await ((dynamic)authorizer).Authorize((dynamic)query, cancellationToken);
            if (result.IsFailure)
            {
                return (TResponse)(dynamic)CreateFailureResult<TResponse>(result.Error);
            }
        }

        var handler = _serviceProvider.GetRequiredService(typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResponse)));
        return await ((dynamic)handler).Handle((dynamic)query, cancellationToken);
    }

    private static Result CreateFailureResult<TResponse>(Error error)
        where TResponse : Result
    {
        return typeof(TResponse) == typeof(Result)
            ? Result.Failure(error)
            : (Result)typeof(TResponse).GetMethod(nameof(Result<int>.Failure))!.Invoke(null, [error])!;
    }
}
