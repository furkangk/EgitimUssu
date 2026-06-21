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
