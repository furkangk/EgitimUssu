using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 Task 1 / N-20: Davet ve katılım kodları tahmin edilemez olmalı. Eski kod
/// <c>Random.Shared.Next(0, 1_000_000)</c> ile 10⁶ olasılıklı, süresiz ve rol kontrolsüzdü.
/// </summary>
public sealed class SecureCodeGeneratorTests
{
    [Fact]
    public void NewCode_Default_Is_Eight_Characters_From_The_Unambiguous_Alphabet()
    {
        var generator = new SecureCodeGenerator();

        for (var i = 0; i < 1_000; i++)
        {
            var code = generator.NewCode();
            Assert.Equal(8, code.Length);
            Assert.All(code, character => Assert.Contains(character, SecureCode.Alphabet));
        }
    }

    [Fact]
    public void Alphabet_Excludes_Confusable_Characters()
    {
        foreach (var confusable in "01OIL")
        {
            Assert.DoesNotContain(confusable, SecureCode.Alphabet);
        }
    }

    [Fact]
    public void NewCode_Produces_No_Collision_In_Ten_Thousand_Codes()
    {
        var generator = new SecureCodeGenerator();
        var codes = Enumerable.Range(0, 10_000).Select(_ => generator.NewCode()).ToHashSet();

        Assert.Equal(10_000, codes.Count);
    }

    [Fact]
    public void NewCode_Rejects_Lengths_Shorter_Than_Six()
    {
        var generator = new SecureCodeGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.NewCode(5));
    }

    [Theory]
    [InlineData(" abcd2345 ", "ABCD2345")]
    [InlineData("AbCd2345", "ABCD2345")]
    [InlineData(null, "")]
    public void Normalize_Trims_And_Uppercases(string? input, string expected)
        => Assert.Equal(expected, SecureCode.Normalize(input));

    /// <summary>Kaynak kilidi: davet kodu üreten modüllerde zayıf rastgelelik kalmamalı.</summary>
    [Fact]
    public void Invite_Modules_Do_Not_Use_Random_Shared()
    {
        var root = FindRepositoryRoot();
        var offenders = new[] { "Students", "Parents" }
            .SelectMany(module => Directory.EnumerateFiles(
                Path.Combine(root, "src", "Modules", module), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("Random.Shared", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EgitimUssu.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Depo kökü bulunamadı.");
    }
}
