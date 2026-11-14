using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 6 / N-24, P-01: Guard'ların yazılmış olması yetmez — <c>Program.cs</c>'e <b>bağlı</b>
/// oldukları da doğrulanmalı (Öğrenilenler #182). Bu testler gerçek uygulamayı Production ortamında
/// ayağa kaldırır: sahte e-posta/depolama sağlayıcısıyla açılış <b>durur</b>, gerçek sağlayıcılarla
/// (ve bilinçli olarak izinli Logging push'la) açılır.
/// <para>
/// Guard sırası: JWT → bağlantı dizesi → e-posta → push → depolama → Redis. JWT ve bağlantı dizesi
/// geçerli verilir; yoksa açılış onların mesajıyla durur ve test e-posta guard'ını hiç görmez.
/// Bu yüzden testler "fırlattı" demekle yetinmez, <b>mesajı</b> doğrular.
/// </para>
/// </summary>
public sealed class ProductionProviderGuardHostTests
{
    [Fact]
    public void Production_Should_Refuse_To_Start_With_Logging_Email_Provider()
    {
        using var environment = ProductionEnvironment.With(new Dictionary<string, string?>
        {
            // Varsayılan (appsettings.json) Logging'dir — üretim blueprint'inde anahtar unutulursa olan budur.
            ["Email__Provider"] = null,
        });

        var message = StartupFailureMessage();

        Assert.Contains("Email:Provider üretimde Logging olamaz", message);
    }

    [Fact]
    public void Production_Should_Refuse_To_Start_With_Local_Storage_Provider()
    {
        using var environment = ProductionEnvironment.With(new Dictionary<string, string?>(ProductionEnvironment.RealEmail)
        {
            ["Storage__Provider"] = null,
        });

        var message = StartupFailureMessage();

        Assert.Contains("Storage:Provider üretimde Local olamaz", message);
    }

    /// <summary>
    /// Pozitif kontrol: gerçek e-posta + depolama yapılandırmasıyla üretim açılır ve Logging push
    /// bilinçli olarak reddedilmez (Q8 beklenirken yayın yapılabilmeli). Bu test olmasaydı, üretimi
    /// her koşulda açmayan bozuk bir guard da yukarıdaki iki testi geçerdi.
    /// </summary>
    [Fact]
    public async Task Production_Should_Start_With_Real_Email_And_Storage_And_Logging_Push()
    {
        using var environment = ProductionEnvironment.With(new Dictionary<string, string?>(ProductionEnvironment.RealEmail)
        {
            ["Storage__Provider"] = "S3",
            ["Storage__BucketName"] = "egitimussu-integration",
            ["Storage__AccessKeyId"] = "integration-access-key",
            ["Storage__SecretAccessKey"] = "integration-secret-key",
            ["Push__Provider"] = "Logging",
        });

        await using var factory = CreateProductionFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateProductionFactory()
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

    /// <summary>
    /// Host'u başlatır ve açılışın başarısız olduğunu doğrular; istisna zincirindeki tüm mesajları
    /// döndürür (<c>WebApplicationFactory</c> giriş noktasının istisnasını sarabilir).
    /// </summary>
    private static string StartupFailureMessage()
    {
        using var factory = CreateProductionFactory();

        var exception = Record.Exception(() => factory.Services);

        Assert.NotNull(exception);
        return string.Join(" | ", Flatten(exception).Select(e => e.Message));
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;

        IEnumerable<Exception> inner = exception switch
        {
            AggregateException aggregate => aggregate.InnerExceptions,
            { InnerException: { } single } => [single],
            _ => [],
        };

        foreach (var child in inner.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    /// <summary>Süreç-geneli ortam değişkenlerini kurar ve <c>Dispose</c>'ta eski değere döndürür.</summary>
    private sealed class ProductionEnvironment : IDisposable
    {
        /// <summary>Guard'ı geçen eksiksiz bir SMTP yapılandırması (gönderim denenmez).</summary>
        public static readonly IReadOnlyDictionary<string, string?> RealEmail = new Dictionary<string, string?>
        {
            ["Email__Provider"] = "Smtp",
            ["Email__FromEmail"] = "no-reply@egitimussu.local",
            ["Email__Smtp__Host"] = "smtp.integration.test",
            ["Email__Smtp__Username"] = "integration-user",
            ["Email__Smtp__Password"] = "integration-password",
        };

        private readonly Dictionary<string, string?> _previous = [];

        private ProductionEnvironment(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (key, value) in values)
            {
                _previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        /// <summary>
        /// E-posta guard'ından önce koşan guard'ları geçen taban + teste özgü değerler. Teste özgü
        /// <c>null</c> değer, anahtarı kaldırır (appsettings.json varsayılanı geçerli olur).
        /// </summary>
        public static ProductionEnvironment With(IReadOnlyDictionary<string, string?> overrides)
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings__Postgres"] = "InMemory:production-provider-guard",
                ["Jwt__SigningKey"] = "integration-test-signing-key-do-not-use-in-production-0123456789",
                // Production Redis guard'ı gerçek (loopback olmayan) bir adres bekler; bu testler
                // Redis işlemi yapmaz.
                ["Redis__Configuration"] = "redis.integration.test:6379,abortConnect=false",
                ["Outbox__DispatchEnabled"] = "false",
                ["Seeding__Enabled"] = "false",
                ["Email__Provider"] = null,
                ["Email__FromEmail"] = null,
                ["Email__Smtp__Host"] = null,
                ["Email__Smtp__Username"] = null,
                ["Email__Smtp__Password"] = null,
                ["Storage__Provider"] = null,
                ["Storage__BucketName"] = null,
                ["Storage__AccessKeyId"] = null,
                ["Storage__SecretAccessKey"] = null,
                ["Push__Provider"] = null,
            };

            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }

            return new ProductionEnvironment(values);
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
