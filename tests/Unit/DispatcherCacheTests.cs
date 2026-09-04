using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Application;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// O4: Dispatcher'lar tip başına derlenmiş delegate önbelleği kullanır; <c>dynamic</c> yoktur.
/// Testler hem önbelleğin istek tipi başına <b>bir kez</b> kurulduğunu hem de boru hattı davranışının
/// (validator → authorizer → handler, hata durumunda kısa devre) korunduğunu sabitler.
/// </summary>
public sealed class DispatcherCacheTests
{
    private static readonly Error Denied = new("test.denied", "izin yok");

    // Önbellek sayımını ölçen testler yalnız kendilerine ait tipleri gönderir;
    // böylece sınıftaki diğer testlerin sırası ölçümü etkilemez.
    private sealed record CachedCommand(int Value) : ICommand<Result<int>>;

    private sealed record CachedQuery(int Value) : IQuery<Result<string>>;

    private sealed record OrderedCommand : ICommand<Result>;

    private sealed record OrderedQuery : IQuery<Result<string>>;

    private sealed record RejectedCommand : ICommand<Result>;

    private sealed record RejectedQuery : IQuery<Result<string>>;

    private sealed class Trace
    {
        public List<string> Steps { get; } = [];
    }

    private sealed class TracedCommandValidator<TCommand>(Trace trace) : ICommandValidator<TCommand>
    {
        public Task<Result> Validate(TCommand command, CancellationToken cancellationToken)
        {
            trace.Steps.Add("validate");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RejectingCommandValidator<TCommand>(Trace trace) : ICommandValidator<TCommand>
    {
        public Task<Result> Validate(TCommand command, CancellationToken cancellationToken)
        {
            trace.Steps.Add("validate");
            return Task.FromResult(Result.Failure(Denied));
        }
    }

    private sealed class TracedCommandAuthorizer<TCommand>(Trace trace) : ICommandAuthorizer<TCommand>
    {
        public Task<Result> Authorize(TCommand command, CancellationToken cancellationToken)
        {
            trace.Steps.Add("authorize");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RejectingCommandAuthorizer<TCommand>(Trace trace) : ICommandAuthorizer<TCommand>
    {
        public Task<Result> Authorize(TCommand command, CancellationToken cancellationToken)
        {
            trace.Steps.Add("authorize");
            return Task.FromResult(Result.Failure(Denied));
        }
    }

    private sealed class TracedCommandHandler<TCommand>(Trace trace) : ICommandHandler<TCommand, Result>
        where TCommand : ICommand<Result>
    {
        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            trace.Steps.Add("handle");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class CachedCommandHandler(Trace trace) : ICommandHandler<CachedCommand, Result<int>>
    {
        public Task<Result<int>> Handle(CachedCommand command, CancellationToken cancellationToken)
        {
            trace.Steps.Add("handle");
            return Task.FromResult(Result<int>.Success(command.Value * 2));
        }
    }

    private sealed class TracedQueryValidator<TQuery>(Trace trace) : IQueryValidator<TQuery>
    {
        public Task<Result> Validate(TQuery query, CancellationToken cancellationToken)
        {
            trace.Steps.Add("validate");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RejectingQueryValidator<TQuery>(Trace trace) : IQueryValidator<TQuery>
    {
        public Task<Result> Validate(TQuery query, CancellationToken cancellationToken)
        {
            trace.Steps.Add("validate");
            return Task.FromResult(Result.Failure(Denied));
        }
    }

    private sealed class TracedQueryAuthorizer<TQuery>(Trace trace) : IQueryAuthorizer<TQuery>
    {
        public Task<Result> Authorize(TQuery query, CancellationToken cancellationToken)
        {
            trace.Steps.Add("authorize");
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TracedQueryHandler<TQuery>(Trace trace) : IQueryHandler<TQuery, Result<string>>
        where TQuery : IQuery<Result<string>>
    {
        public Task<Result<string>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            trace.Steps.Add("handle");
            return Task.FromResult(Result<string>.Success("ok"));
        }
    }

    private sealed class CachedQueryHandler(Trace trace) : IQueryHandler<CachedQuery, Result<string>>
    {
        public Task<Result<string>> Handle(CachedQuery query, CancellationToken cancellationToken)
        {
            trace.Steps.Add("handle");
            return Task.FromResult(Result<string>.Success($"v{query.Value}"));
        }
    }

    private static ServiceProvider BuildProvider(Trace trace)
    {
        var services = new ServiceCollection();
        services.AddSingleton(trace);

        services.AddSingleton<ICommandValidator<CachedCommand>, TracedCommandValidator<CachedCommand>>();
        services.AddSingleton<ICommandAuthorizer<CachedCommand>, TracedCommandAuthorizer<CachedCommand>>();
        services.AddSingleton<ICommandHandler<CachedCommand, Result<int>>, CachedCommandHandler>();

        services.AddSingleton<ICommandValidator<OrderedCommand>, TracedCommandValidator<OrderedCommand>>();
        services.AddSingleton<ICommandAuthorizer<OrderedCommand>, TracedCommandAuthorizer<OrderedCommand>>();
        services.AddSingleton<ICommandHandler<OrderedCommand, Result>, TracedCommandHandler<OrderedCommand>>();

        services.AddSingleton<ICommandValidator<RejectedCommand>, TracedCommandValidator<RejectedCommand>>();
        services.AddSingleton<ICommandAuthorizer<RejectedCommand>, RejectingCommandAuthorizer<RejectedCommand>>();
        services.AddSingleton<ICommandHandler<RejectedCommand, Result>, TracedCommandHandler<RejectedCommand>>();

        services.AddSingleton<IQueryValidator<CachedQuery>, TracedQueryValidator<CachedQuery>>();
        services.AddSingleton<IQueryAuthorizer<CachedQuery>, TracedQueryAuthorizer<CachedQuery>>();
        services.AddSingleton<IQueryHandler<CachedQuery, Result<string>>, CachedQueryHandler>();

        services.AddSingleton<IQueryValidator<OrderedQuery>, TracedQueryValidator<OrderedQuery>>();
        services.AddSingleton<IQueryAuthorizer<OrderedQuery>, TracedQueryAuthorizer<OrderedQuery>>();
        services.AddSingleton<IQueryHandler<OrderedQuery, Result<string>>, TracedQueryHandler<OrderedQuery>>();

        services.AddSingleton<IQueryValidator<RejectedQuery>, RejectingQueryValidator<RejectedQuery>>();
        services.AddSingleton<IQueryAuthorizer<RejectedQuery>, TracedQueryAuthorizer<RejectedQuery>>();
        services.AddSingleton<IQueryHandler<RejectedQuery, Result<string>>, TracedQueryHandler<RejectedQuery>>();

        return services.BuildServiceProvider();
    }

    private static CommandDispatcher NewCommandDispatcher(ServiceProvider provider) =>
        new(provider, NullLogger<CommandDispatcher>.Instance);

    private static QueryDispatcher NewQueryDispatcher(ServiceProvider provider) =>
        new(provider, NullLogger<QueryDispatcher>.Instance);

    [Fact]
    public async Task Command_invoker_is_built_once_per_type()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);
        var dispatcher = NewCommandDispatcher(provider);

        var before = DispatcherInvokerCache.CachedTypeCount;

        for (var i = 0; i < 100; i++)
        {
            var result = await dispatcher.Dispatch(new CachedCommand(i), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(i * 2, result.Value);
        }

        Assert.Equal(before + 1, DispatcherInvokerCache.CachedTypeCount);
        Assert.Equal(300, trace.Steps.Count);
    }

    [Fact]
    public async Task Query_invoker_is_built_once_per_type()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);
        var dispatcher = NewQueryDispatcher(provider);

        var before = DispatcherInvokerCache.CachedTypeCount;

        for (var i = 0; i < 100; i++)
        {
            var result = await dispatcher.Dispatch(new CachedQuery(i), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal($"v{i}", result.Value);
        }

        Assert.Equal(before + 1, DispatcherInvokerCache.CachedTypeCount);
        Assert.Equal(300, trace.Steps.Count);
    }

    [Fact]
    public async Task Command_pipeline_order_is_validator_authorizer_handler()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);

        var result = await NewCommandDispatcher(provider).Dispatch(new OrderedCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["validate", "authorize", "handle"], trace.Steps);
    }

    [Fact]
    public async Task Query_pipeline_order_is_validator_authorizer_handler()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);

        var result = await NewQueryDispatcher(provider).Dispatch(new OrderedQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["validate", "authorize", "handle"], trace.Steps);
    }

    [Fact]
    public async Task Failed_command_authorizer_short_circuits_handler()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);

        var result = await NewCommandDispatcher(provider).Dispatch(new RejectedCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(Denied, result.Error);
        Assert.Equal(["validate", "authorize"], trace.Steps);
    }

    [Fact]
    public async Task Failed_query_validator_short_circuits_authorizer_and_handler()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);

        var result = await NewQueryDispatcher(provider).Dispatch(new RejectedQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(Denied, result.Error);
        Assert.Equal(["validate"], trace.Steps);
    }

    [Fact]
    public async Task Failure_of_generic_response_keeps_result_type()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace);

        Result<string> result = await NewQueryDispatcher(provider).Dispatch(new RejectedQuery(), CancellationToken.None);

        Assert.IsType<Result<string>>(result);
        Assert.Null(result.Value);
    }
}
