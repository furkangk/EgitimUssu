using System.Reflection;
using EgitimUssu.Shared.Application;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// Handler'ı olan her command/query tipinin ya IAllowAnonymous uyguladığını ya da
/// karşılık gelen bir ICommandAuthorizer/IQueryAuthorizer implementasyonunun var olduğunu doğrular.
/// Yeni bir handler eklenip authorizer unutulduğunda bu test derleme/CI'da yakalar.
/// </summary>
public sealed class AuthorizationCoverageTests
{
    private static readonly Type CommandHandlerDef = typeof(ICommandHandler<,>);
    private static readonly Type QueryHandlerDef = typeof(IQueryHandler<,>);
    private static readonly Type CommandAuthorizerDef = typeof(ICommandAuthorizer<>);
    private static readonly Type QueryAuthorizerDef = typeof(IQueryAuthorizer<>);
    private static readonly Type AllowAnonymousType = typeof(IAllowAnonymous);

    [Fact]
    public void Every_Handler_Must_Have_Authorizer_Or_AllowAnonymous()
    {
        var assemblies = LoadModuleAssemblies();
        var allTypes = assemblies.SelectMany(SafeGetTypes).ToArray();

        var coverage = BuildCoverage(allTypes);
        var unprotected = FindUnprotectedOperands(allTypes, coverage);

        Assert.True(
            unprotected.Count == 0,
            $"Eksik authorizer veya IAllowAnonymous işareti olan command/query tipleri:\n" +
            string.Join("\n", unprotected.Order().Select(n => $"  - {n}")));
    }

    /// <summary>
    /// Authorizer kapsamını toplar. İki biçimi de tanır:
    /// (1) Kapalı authorizer (ör. <c>IQueryAuthorizer&lt;GetXQuery&gt;</c>) → o operand kapsanır.
    /// (2) Açık-generik authorizer (ör. <c>StudyOwnershipQueryAuthorizer&lt;T&gt; where T : IStudentScopedRequest</c>)
    ///     → jenerik parametrenin kısıt (constraint) arayüzünü uygulayan tüm operand'lar kapsanır.
    ///     Bu desen DI'da her somut tip için kapalı olarak kaydedilir; startup validator ayrıca doğrular.
    /// </summary>
    private static (HashSet<Type> ClosedOperands, HashSet<Type> MarkerInterfaces) BuildCoverage(Type[] allTypes)
    {
        var closedOperands = new HashSet<Type>();
        var markerInterfaces = new HashSet<Type>();

        foreach (var type in allTypes.Where(t => !t.IsInterface))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = iface.GetGenericTypeDefinition();
                if (def != CommandAuthorizerDef && def != QueryAuthorizerDef)
                {
                    continue;
                }

                var operand = iface.GetGenericArguments()[0];
                if (operand.IsGenericParameter)
                {
                    foreach (var constraint in operand.GetGenericParameterConstraints().Where(c => c.IsInterface))
                    {
                        markerInterfaces.Add(constraint);
                    }
                }
                else
                {
                    closedOperands.Add(operand);
                }
            }
        }

        return (closedOperands, markerInterfaces);
    }

    private static List<string> FindUnprotectedOperands(
        Type[] allTypes,
        (HashSet<Type> ClosedOperands, HashSet<Type> MarkerInterfaces) coverage)
    {
        var seen = new HashSet<Type>();
        var unprotected = new List<string>();

        foreach (var type in allTypes.Where(t => !t.IsAbstract && !t.IsInterface))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = iface.GetGenericTypeDefinition();
                if (def != CommandHandlerDef && def != QueryHandlerDef) continue;

                var operandType = iface.GetGenericArguments()[0];
                if (!seen.Add(operandType)) continue;

                var isCovered = coverage.ClosedOperands.Contains(operandType)
                    || coverage.MarkerInterfaces.Any(marker => marker.IsAssignableFrom(operandType))
                    || AllowAnonymousType.IsAssignableFrom(operandType);

                if (!isCovered)
                {
                    unprotected.Add(operandType.FullName ?? operandType.Name);
                }
            }
        }

        return unprotected;
    }

    private static Assembly[] LoadModuleAssemblies()
    {
        var baseDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;

        var modulePaths = Directory.GetFiles(baseDir, "EgitimUssu.Modules.*.dll");
        var sharedPath = Path.Combine(baseDir, "EgitimUssu.Shared.Application.dll");

        return modulePaths
            .Append(sharedPath)
            .Where(File.Exists)
            .Select(TryLoadAssembly)
            .OfType<Assembly>()
            .ToArray();
    }

    private static Assembly? TryLoadAssembly(string path)
    {
        try { return Assembly.LoadFrom(path); }
        catch { return null; }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
    }
}
