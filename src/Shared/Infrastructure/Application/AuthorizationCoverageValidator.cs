using EgitimUssu.Shared.Application;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Shared.Infrastructure;

public static class AuthorizationCoverageValidator
{
    private static readonly Type CommandHandlerDef = typeof(ICommandHandler<,>);
    private static readonly Type QueryHandlerDef = typeof(IQueryHandler<,>);
    private static readonly Type CommandAuthorizerDef = typeof(ICommandAuthorizer<>);
    private static readonly Type QueryAuthorizerDef = typeof(IQueryAuthorizer<>);
    private static readonly Type AllowAnonymousType = typeof(IAllowAnonymous);

    /// <summary>
    /// Kayıtlı her ICommandHandler ve IQueryHandler için ya bir authorizer ya da IAllowAnonymous işareti
    /// zorunlu tutar. İkisi de yoksa InvalidOperationException fırlatır (startup fail-fast).
    /// </summary>
    public static IServiceCollection ValidateAuthorizationCoverage(this IServiceCollection services)
    {
        var registeredServiceTypes = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType)
            .ToHashSet();

        var unprotected = new List<string>();

        foreach (var descriptor in services)
        {
            var serviceType = descriptor.ServiceType;
            if (!serviceType.IsGenericType) continue;

            var genericDef = serviceType.GetGenericTypeDefinition();
            Type? operandType = null;
            Type? expectedAuthorizerType = null;

            if (genericDef == CommandHandlerDef)
            {
                operandType = serviceType.GetGenericArguments()[0];
                expectedAuthorizerType = CommandAuthorizerDef.MakeGenericType(operandType);
            }
            else if (genericDef == QueryHandlerDef)
            {
                operandType = serviceType.GetGenericArguments()[0];
                expectedAuthorizerType = QueryAuthorizerDef.MakeGenericType(operandType);
            }

            if (operandType is null || expectedAuthorizerType is null) continue;

            var hasAuthorizer = registeredServiceTypes.Contains(expectedAuthorizerType);
            var isAllowAnonymous = AllowAnonymousType.IsAssignableFrom(operandType);

            if (!hasAuthorizer && !isAllowAnonymous)
            {
                unprotected.Add(operandType.Name);
            }
        }

        if (unprotected.Count > 0)
        {
            var list = string.Join(", ", unprotected.Distinct().Order());
            throw new InvalidOperationException(
                $"Eksik authorizer tespit edildi. Aşağıdaki command/query tipleri ne IAllowAnonymous " +
                $"uygulayarak ne de kayıtlı bir ICommandAuthorizer/IQueryAuthorizer ile korunuyor: {list}");
        }

        return services;
    }
}
