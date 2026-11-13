using EgitimUssu.API.Host.Seeding;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Modules.Teachers.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P13 Task 3 / E-06: Staging demo verisi.
/// <para>
/// İki kural pazarlık dışıdır: (1) tohumlama <b>üretimde asla</b> koşmaz — demo öğretmeni gerçek
/// kullanıcı listesine karışırsa geri alması elle temizlik demektir; (2) <b>idempotenttir</b> —
/// staging her deploy'da yeniden başlar, her açılışta bir demo öğrenci daha eklenirse ortam bir
/// hafta içinde çöplüğe döner.
/// </para>
/// </summary>
public sealed class DemoDataSeederTests
{
    /// <summary>
    /// Guard saf bir karar fonksiyonudur: ortam adı + bayrak → engel nedeni (ya da <c>null</c>).
    /// Üretimde bayrak <b>açık olsa bile</b> engellenir; yanlışlıkla açılan bir env değişkeni
    /// üretim veritabanına demo veri yazamamalı.
    /// </summary>
    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Production", false, false)]
    [InlineData("Development", false, false)]
    [InlineData("Staging", false, false)]
    [InlineData("Staging", true, true)]
    [InlineData("Development", true, true)]
    public void Guard_Should_Block_Production_And_Disabled_Environments(
        string environmentName, bool enabled, bool expectedAllowed)
    {
        var reason = DemoDataSeedGuard.BlockReason(environmentName, enabled);

        Assert.Equal(expectedAllowed, reason is null);
    }

    /// <summary>Ortam adı büyük/küçük harf yazımına takılmamalı.</summary>
    [Theory]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void Guard_Should_Match_Production_Case_Insensitively(string environmentName)
    {
        Assert.NotNull(DemoDataSeedGuard.BlockReason(environmentName, enabled: true));
    }

    /// <summary>
    /// Tohumlama uygulamanın <b>açılışında</b> koşar (<c>Program.cs</c>); bu test hem o bağlantıyı
    /// hem de üretilen veri setinin şeklini gerçek tablolardan doğrular. Özet nesnesine değil
    /// veriye bakar: seeder "5 öğrenci oluşturdum" diyip 3 yazsaydı özet testi bunu göremezdi.
    /// </summary>
    [Fact]
    public async Task Startup_Should_Seed_The_Full_Demo_Set()
    {
        using var environment = TestEnvironment.For("demo-seed-startup", enabled: true);
        await using var factory = new WebApplicationFactory<Program>();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.Equal(2, await services.GetRequiredService<TeachersDbContext>().TeacherProfiles.CountAsync());
        Assert.Equal(5, await services.GetRequiredService<StudentsDbContext>().StudentProfiles.CountAsync());
        Assert.Equal(2, await services.GetRequiredService<ParentsDbContext>().ParentProfiles.CountAsync());

        // Veli bağı **onaylanmış** olmalı: onaylanmamış bağla veli hiçbir çocuk verisi göremez,
        // yani demo ortamının veli tarafı boş kalırdı.
        var links = await services.GetRequiredService<ParentsDbContext>().ParentChildLinks.ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.Equal("Approved", link.Status.ToString()));

        Assert.Equal(15, await services.GetRequiredService<SchedulingDbContext>().LessonSchedules.CountAsync());
        Assert.Equal(15, await services.GetRequiredService<AssignmentsDbContext>().Assignments.CountAsync());
        Assert.Equal(5, await services.GetRequiredService<PaymentsDbContext>().PaymentRecords.CountAsync());
        Assert.Equal(12, await services.GetRequiredService<StudyDbContext>().StudySessions.CountAsync());
    }

    /// <summary>
    /// Staging her deploy'da yeniden başlar; ikinci tur <b>hiçbir</b> yeni kayıt açmamalı.
    /// </summary>
    [Fact]
    public async Task Seeding_Again_Should_Not_Create_Duplicate_Records()
    {
        using var environment = TestEnvironment.For("demo-seed-idempotency", enabled: true);
        await using var factory = new WebApplicationFactory<Program>();

        // Açılış turu zaten koştu; buradaki çağrı ikinci turdur.
        var countAfterStartup = await CountStudentProfilesAsync(factory);
        Assert.Equal(5, countAfterStartup);

        var repeat = await factory.Services.SeedDemoDataAsync();

        Assert.False(repeat.Skipped);
        Assert.Equal(0, repeat.Created);
        Assert.Equal(countAfterStartup, await CountStudentProfilesAsync(factory));
    }

    /// <summary>
    /// Guard'ın yazılmış olması yetmez — seeder'a <b>bağlı</b> olduğu da doğrulanmalı. Bu test
    /// gerçek uygulamayı Production ortamında ayağa kaldırır ve bayrak açıkken bile hiçbir kaydın
    /// oluşmadığını gösterir.
    /// </summary>
    [Fact]
    public async Task Production_Environment_Should_Never_Seed()
    {
        using var environment = TestEnvironment.For("demo-seed-production", enabled: true);
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var summary = await factory.Services.SeedDemoDataAsync();

        Assert.True(summary.Skipped);
        Assert.Equal(0, summary.Created);
        Assert.Equal(0, await CountStudentProfilesAsync(factory));
    }

    private static async Task<int> CountStudentProfilesAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        return await dbContext.StudentProfiles.AsNoTracking().CountAsync();
    }

    /// <summary>Süreç-geneli ortam değişkenlerini kurar ve <c>Dispose</c>'ta eski değere döndürür.</summary>
    private sealed class TestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = [];

        private TestEnvironment(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (key, value) in values)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public static TestEnvironment For(string databaseName, bool enabled) => new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__Postgres"] = "InMemory:" + databaseName,
                ["Seeding__Enabled"] = enabled ? "true" : "false",
                ["Outbox__DispatchEnabled"] = "false",
                ["Jwt__SigningKey"] = "integration-test-signing-key-do-not-use-in-production-0123456789",
                ["Email__FromEmail"] = "no-reply@egitimussu.local",
                // Production startup guard gerçek bir Redis adresi bekler. Bu test Redis işlemi
                // yapmaz; çözümlenmeyen test host'u yalnız yapılandırma sözleşmesini karşılar.
                ["Redis__Configuration"] = "redis.integration.test:6379,abortConnect=false"
            });

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
