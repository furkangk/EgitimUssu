using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Application;

/// <summary>
/// O4: Dispatcher boru hattının reflection maliyetini tip başına <b>bir kez</b> ödetir.
/// İstek tipi başına kapalı servis tipleri (<c>ICommandValidator&lt;T&gt;</c> vb.) ile
/// derlenmiş çağrı delegeleri üretilip önbelleklenir; sıcak yolda çalışma-zamanı bağlayıcısı (runtime binder) kullanılmaz.
/// </summary>
internal static class DispatcherInvokerCache
{
    private const string ValidateMethod = nameof(ICommandValidator<ICommand<Result>>.Validate);
    private const string AuthorizeMethod = nameof(ICommandAuthorizer<ICommand<Result>>.Authorize);

    private static readonly ConcurrentDictionary<Type, RequestInvokers> Cache = new();

    /// <summary>Testler için: önbellekte kaç istek tipi tutulduğu.</summary>
    internal static int CachedTypeCount => Cache.Count;

    public static RequestInvokers ForCommand(Type commandType) =>
        Cache.GetOrAdd(commandType, static type => Build(type, typeof(ICommandValidator<>), typeof(ICommandAuthorizer<>)));

    public static RequestInvokers ForQuery(Type queryType) =>
        Cache.GetOrAdd(queryType, static type => Build(type, typeof(IQueryValidator<>), typeof(IQueryAuthorizer<>)));

    private static RequestInvokers Build(Type requestType, Type validatorDefinition, Type authorizerDefinition)
    {
        var validatorType = validatorDefinition.MakeGenericType(requestType);
        var authorizerType = authorizerDefinition.MakeGenericType(requestType);

        return new RequestInvokers(
            validatorType,
            authorizerType,
            BuildInvoker(validatorType, requestType, ValidateMethod),
            BuildInvoker(authorizerType, requestType, AuthorizeMethod));
    }

    /// <summary>
    /// <c>(service, request, ct) =&gt; ((TService)service).Method((TRequest)request, ct)</c> ifadesini derler.
    /// </summary>
    private static Func<object, object, CancellationToken, Task<Result>> BuildInvoker(
        Type serviceType,
        Type requestType,
        string methodName)
    {
        var method = serviceType.GetMethod(methodName)
            ?? throw new InvalidOperationException($"{serviceType.Name} üzerinde {methodName} metodu bulunamadı.");

        var service = Expression.Parameter(typeof(object), "service");
        var request = Expression.Parameter(typeof(object), "request");
        var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(
            Expression.Convert(service, serviceType),
            method,
            Expression.Convert(request, requestType),
            cancellationToken);

        return Expression.Lambda<Func<object, object, CancellationToken, Task<Result>>>(
            call, service, request, cancellationToken).Compile();
    }

    internal sealed record RequestInvokers(
        Type ValidatorServiceType,
        Type AuthorizerServiceType,
        Func<object, object, CancellationToken, Task<Result>> Validate,
        Func<object, object, CancellationToken, Task<Result>> Authorize);
}

/// <summary>
/// Handler çağrısı yanıt tipini de gerektirir; jenerik statik sınıf sayesinde
/// <typeparamref name="TResponse"/> başına ayrı, tipli bir önbellek tutulur.
/// </summary>
internal static class DispatcherHandlerInvokerCache<TResponse>
    where TResponse : Result
{
    private const string HandleMethod = nameof(ICommandHandler<ICommand<Result>, Result>.Handle);

    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Cache = new();

    public static HandlerInvoker ForCommand(Type commandType) =>
        Cache.GetOrAdd(commandType, static type => Build(type, typeof(ICommandHandler<,>)));

    public static HandlerInvoker ForQuery(Type queryType) =>
        Cache.GetOrAdd(queryType, static type => Build(type, typeof(IQueryHandler<,>)));

    private static HandlerInvoker Build(Type requestType, Type handlerDefinition)
    {
        var handlerType = handlerDefinition.MakeGenericType(requestType, typeof(TResponse));
        var method = handlerType.GetMethod(HandleMethod)
            ?? throw new InvalidOperationException($"{handlerType.Name} üzerinde {HandleMethod} metodu bulunamadı.");

        var handler = Expression.Parameter(typeof(object), "handler");
        var request = Expression.Parameter(typeof(object), "request");
        var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var call = Expression.Call(
            Expression.Convert(handler, handlerType),
            method,
            Expression.Convert(request, requestType),
            cancellationToken);

        var invoke = Expression.Lambda<Func<object, object, CancellationToken, Task<TResponse>>>(
            call, handler, request, cancellationToken).Compile();

        return new HandlerInvoker(handlerType, invoke);
    }

    internal sealed record HandlerInvoker(
        Type ServiceType,
        Func<object, object, CancellationToken, Task<TResponse>> Invoke);
}

/// <summary>
/// <c>Result</c> / <c>Result&lt;T&gt;</c> hata örneğini yanıt tipi başına derlenmiş bir fabrika ile üretir;
/// eski <c>GetMethod(...).Invoke(...)</c> yolunu ve geç bağlanan dönüşümü ortadan kaldırır.
/// </summary>
internal static class DispatcherFailureFactory<TResponse>
    where TResponse : Result
{
    public static readonly Func<Error, TResponse> Create = Build();

    private static Func<Error, TResponse> Build()
    {
        var method = typeof(TResponse).GetMethod(
            nameof(Result.Failure),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null,
            types: [typeof(Error)],
            modifiers: null);

        if (method is null || !typeof(TResponse).IsAssignableFrom(method.ReturnType))
        {
            return static error => (TResponse)Result.Failure(error);
        }

        var parameter = Expression.Parameter(typeof(Error), "error");
        return Expression.Lambda<Func<Error, TResponse>>(
            Expression.Convert(Expression.Call(method, parameter), typeof(TResponse)), parameter).Compile();
    }
}
