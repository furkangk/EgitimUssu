using System.Reflection;
using EgitimUssu.API.Host;
using EgitimUssu.Shared.Application;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// Handler'ı olan her command/query tipinin ya IAllowAnonymous uyguladığını ya da
/// karşılık gelen bir ICommandAuthorizer/IQueryAuthorizer implementasyonunun var olduğunu doğrular.
/// Yeni bir handler eklenip authorizer unutulduğunda bu test derleme/CI'da yakalar.
/// <para>
/// Sınırı (rapor §1.6): yalnız authorizer'ın <b>varlığına</b> bakar, sahiplik kuralının doğruluğuna değil — o
/// <c>RealCrossTenantAccessTests</c>'in işidir. Integration event handler'ları ve modüller arası yazma portları
/// (ör. otomatik ders ücreti) authorizer'a tabi değildir; onların kuralı kendi senaryo testlerinde durur.
/// </para>
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
        var scan = ModuleAssemblyScan.Load(ModuleAssemblyPaths());
        Assert.True(
            scan.Failures.Count == 0,
            "Kapsama denetimi bu assembly'leri göremedi (içlerindeki handler'lar denetlenmemiş olurdu):\n" +
            string.Join("\n", scan.Failures.Select(f => $"  - {f}")));

        // Boş tarama da yeşil geçmesin: host'a kayıtlı her modülün Application assembly'si taranmış olmalı.
        var scanned = scan.Assemblies.Select(a => a.GetName().Name).ToHashSet(StringComparer.Ordinal);
        var missingModules = ModuleAssemblies.All
            .Select(api => api.GetName().Name!.Replace(".API", ".Application", StringComparison.Ordinal))
            .Where(application => !scanned.Contains(application))
            .ToArray();
        Assert.True(missingModules.Length == 0, "Taranmayan modüller: " + string.Join(", ", missingModules));

        var allTypes = scan.Types.ToArray();
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

    /// <summary>
    /// P15 T9: Taranan yolları yükleyen dikiş. Testin kendi yüklemesi denetlenebilsin diye ayrıldı — yüklenemeyen bir
    /// modül assembly'si kapsama denetiminden sessizce düşerse, içindeki korumasız handler'lar da düşer.
    /// </summary>
    [Fact]
    public void Unloadable_Module_Assembly_Is_Reported_Not_Skipped()
    {
        var directory = Directory.CreateTempSubdirectory("authz-coverage-");
        try
        {
            var junk = Path.Combine(directory.FullName, "EgitimUssu.Modules.Bozuk.Application.dll");
            File.WriteAllText(junk, "bu bir assembly degil");
            var missing = Path.Combine(directory.FullName, "EgitimUssu.Shared.Application.dll");

            var scan = ModuleAssemblyScan.Load([junk, missing]);

            Assert.Contains(scan.Failures, failure => failure.Contains("EgitimUssu.Modules.Bozuk.Application.dll"));
            Assert.Contains(scan.Failures, failure => failure.Contains("EgitimUssu.Shared.Application.dll"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static IReadOnlyList<string> ModuleAssemblyPaths()
    {
        var baseDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;

        return Directory.GetFiles(baseDir, "EgitimUssu.Modules.*.dll")
            .Append(Path.Combine(baseDir, "EgitimUssu.Shared.Application.dll"))
            .ToArray();
    }

    /// <summary>
    /// Yükleme sonucu: bulunan tipler ve yüklenemeyen yollar. Sessiz atlama yok (P15 T9, rapor §1.6): eksik dosya,
    /// yüklenemeyen assembly ve kısmen yüklenen tipler (<see cref="ReflectionTypeLoadException"/>) hata olarak döner.
    /// </summary>
    private sealed record ModuleAssemblyScan(IReadOnlyList<Assembly> Assemblies, IReadOnlyList<Type> Types, IReadOnlyList<string> Failures)
    {
        public static ModuleAssemblyScan Load(IEnumerable<string> paths)
        {
            var assemblies = new List<Assembly>();
            var types = new List<Type>();
            var failures = new List<string>();

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    failures.Add($"{Path.GetFileName(path)}: dosya yok ({path})");
                    continue;
                }

                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(path);
                }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
                {
                    failures.Add($"{Path.GetFileName(path)}: yüklenemedi — {ex.Message}");
                    continue;
                }

                try
                {
                    types.AddRange(assembly.GetTypes());
                    assemblies.Add(assembly);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var reasons = ex.LoaderExceptions.OfType<Exception>().Select(inner => inner.Message).Distinct();
                    failures.Add($"{Path.GetFileName(path)}: tipleri kısmen yüklendi — {string.Join(" | ", reasons)}");
                }
            }

            return new ModuleAssemblyScan(assemblies, types, failures);
        }
    }
}
