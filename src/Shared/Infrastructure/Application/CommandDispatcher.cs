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

        foreach (var validator in _serviceProvider.GetServices(typeof(ICommandValidator<>).MakeGenericType(commandType)).Cast<object>())
        {
            var result = await ((dynamic)validator).Validate((dynamic)command, cancellationToken);
            if (result.IsFailure)
            {
                return (TResponse)(dynamic)CreateFailureResult<TResponse>(result.Error);
            }
        }

        foreach (var authorizer in _serviceProvider.GetServices(typeof(ICommandAuthorizer<>).MakeGenericType(commandType)).Cast<object>())
        {
            var result = await ((dynamic)authorizer).Authorize((dynamic)command, cancellationToken);
            if (result.IsFailure)
            {
                return (TResponse)(dynamic)CreateFailureResult<TResponse>(result.Error);
            }
        }

        var handler = _serviceProvider.GetRequiredService(typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResponse)));
        return await ((dynamic)handler).Handle((dynamic)command, cancellationToken);
    }

    private static Result CreateFailureResult<TResponse>(Error error)
        where TResponse : Result
    {
        return typeof(TResponse) == typeof(Result)
            ? Result.Failure(error)
            : (Result)typeof(TResponse).GetMethod(nameof(Result<int>.Failure))!.Invoke(null, [error])!;
    }
}
