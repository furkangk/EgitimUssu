using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EgitimUssu.Tests.Architecture;

public sealed class ProjectArchitectureTests
{
    private static readonly string[] Modules =
    [
        "Identity",
        "Teachers",
        "Students",
        "Scheduling",
        "LessonSessions",
        "Assignments",
        "Payments",
        "Study",
        "Parents",
        "ProgressTracking",
        "Notifications",
        "Matching",
        "Reviews",
        "Reporting",
        "Settings"
    ];

    /// <summary>
    /// Y2: Modül izolasyonu — hiçbir modül projesi (herhangi bir katman) başka bir modülün projesine
    /// referans veremez. Modüller-arası iletişim yalnız Shared (Contracts/Application/Kernel/Infrastructure)
    /// üzerinden olur (integration event veya paylaşılan read kontratı). Y1'deki gerçek ihlaller bu kuralla yakalanır.
    /// </summary>
    [Fact]
    public void Modules_Should_Not_Reference_Other_Modules()
    {
        var moduleReferencePattern = new Regex(@"EgitimUssu\.Modules\.([A-Za-z]+)\.", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            var moduleDirectory = Path.Combine(GetRoot(), "src", "Modules", module);
            var projectFiles = Directory.GetFiles(moduleDirectory, "*.csproj", SearchOption.AllDirectories);

            foreach (var projectFile in projectFiles)
            {
                foreach (var reference in GetProjectReferences(XDocument.Load(projectFile)))
                {
                    var match = moduleReferencePattern.Match(reference.Replace("\\", "/"));
                    if (match.Success && match.Groups[1].Value != module)
                    {
                        violations.Add($"{Path.GetFileName(projectFile)} -> {reference}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Modüller-arası doğrudan proje referansı yasaktır (Shared üzerinden gidin):" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Application_Projects_Should_Not_Reference_Infrastructure()
    {
        foreach (var module in Modules)
        {
            var project = LoadProject($"src/Modules/{module}/Application/EgitimUssu.Modules.{module}.Application.csproj");
            var references = GetProjectReferences(project);

            Assert.DoesNotContain(references, reference => reference.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Api_Projects_Should_Only_Reference_Their_Own_Module_And_Shared_Libraries()
    {
        foreach (var module in Modules)
        {
            var project = LoadProject($"src/Modules/{module}/API/EgitimUssu.Modules.{module}.API.csproj");
            var references = GetProjectReferences(project);

                    Assert.All(references, reference =>
                    {
                        var normalized = reference.Replace("\\", "/");
                        Assert.True(
                            normalized.Contains($"/Shared/", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains($"EgitimUssu.Modules.{module}.Application", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains($"EgitimUssu.Modules.{module}.Infrastructure", StringComparison.OrdinalIgnoreCase),
                            $"Unexpected API reference for {module}: {reference}");
                    });
                }
            }

    [Fact]
    public void Domain_Projects_Should_Not_Take_Web_Or_EfCore_Dependencies()
    {
        foreach (var module in Modules)
        {
            var project = LoadProject($"src/Modules/{module}/Domain/EgitimUssu.Modules.{module}.Domain.csproj");
            var references = string.Join(" ", GetProjectReferences(project));
            Assert.DoesNotContain("Infrastructure", references, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("API", references, StringComparison.OrdinalIgnoreCase);

            var domainSource = Directory
                .GetFiles(Path.Combine(GetRoot(), "src", "Modules", module, "Domain"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .ToArray();

            Assert.DoesNotContain(domainSource, source => source.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
            Assert.DoesNotContain(domainSource, source => source.Contains("Microsoft.AspNetCore", StringComparison.Ordinal));
        }
    }

    private static XDocument LoadProject(string relativePath) => XDocument.Load(Path.Combine(GetRoot(), relativePath));

    private static string[] GetProjectReferences(XDocument project)
    {
        return project
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
    }

    private static string GetRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "global.json")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
