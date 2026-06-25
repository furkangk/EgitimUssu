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

        var coveredOperands = BuildCoveredOperandSet(allTypes);
        var unprotected = FindUnprotectedOperands(allTypes, coveredOperands);

        Assert.True(
            unprotected.Count == 0,
            $"Eksik authorizer veya IAllowAnonymous işareti olan command/query tipleri:\n" +
            string.Join("\n", unprotected.Order().Select(n => $"  - {n}")));
    }

    private static HashSet<Type> BuildCoveredOperandSet(Type[] allTypes)
    {
        var covered = new HashSet<Type>();

        foreach (var type in allTypes.Where(t => !t.IsAbstract && !t.IsInterface))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = iface.GetGenericTypeDefinition();
                if (def == CommandAuthorizerDef || def == QueryAuthorizerDef)
                {
                    covered.Add(iface.GetGenericArguments()[0]);
                }
            }
        }

        return covered;
    }

    private static List<string> FindUnprotectedOperands(Type[] allTypes, HashSet<Type> coveredOperands)
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

                if (!coveredOperands.Contains(operandType) && !AllowAnonymousType.IsAssignableFrom(operandType))
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
