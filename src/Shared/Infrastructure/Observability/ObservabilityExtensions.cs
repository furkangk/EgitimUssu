using System.Reflection;
using EgitimUssu.Shared.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EgitimUssu.Shared.Infrastructure.Observability;

/// <summary>
/// C-04 / K-10: OpenTelemetry kurulumu — trace (ASP.NET Core + HttpClient + Npgsql) ve metrik
/// (ASP.NET Core + HttpClient + <see cref="EgitimUssuMetrics"/>), OTLP ya da konsol exporter ile.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Gözlemlenebilirliği kaydeder.
    /// <para>
    /// <see cref="EgitimUssuMetrics"/> **her hâlükârda** kaydedilir: iş kodu ona bağımlıdır ve
    /// gözlemlenebilirlik kapalıyken de çağırabilmelidir (dinleyici yoksa ölçüm ücretsizdir).
    /// Kapalıyken kurulmayan tek şey exporter ve enstrümantasyondur — testte/CI'da maliyet sıfır.
    /// </para>
    /// </summary>
    /// <param name="services">Servis koleksiyonu.</param>
    /// <param name="configuration">Konfigürasyon (bölüm: <c>Observability</c>).</param>
    /// <param name="serviceName">Varsayılan servis adı; ayarda değer varsa o kazanır.</param>
    /// <returns>Aynı koleksiyon.</returns>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.Configure<ObservabilityOptions>(configuration.GetSection(ObservabilityOptions.SectionName));

        var options = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        // `IMeterFactory` ve `EgitimUssuMetrics` koşulsuz: metrik yazan kod her ortamda çalışır.
        services.AddMetrics();
        services.AddSingleton<EgitimUssuMetrics>();

        if (!options.Enabled)
        {
            return services;
        }

        var resolvedName = string.IsNullOrWhiteSpace(options.ServiceName) ? serviceName : options.ServiceName;
        var version = string.IsNullOrWhiteSpace(options.ServiceVersion)
            ? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0"
            : options.ServiceVersion;

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(resolvedName, serviceVersion: version))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        // Sağlık uçları saniyede bir çağrılır; trace'i doldurup gerçek isteği gizlerler.
                        instrumentation.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    // EF Core enstrümantasyonunun kararlı sürümü yok; Npgsql'in kendi kararlı
                    // köprüsü SQL komutlarını zaten span olarak yayar (bkz. Öğrenilenler).
                    .AddNpgsql()
                    // P13 Task 9: Uygulamanın kendi span'ları (komut/sorgu dispatch'i, outbox turu ve
                    // mesaj dispatch'i, olay handler'ları, saklama turu). Bu kaynak kaydedilmezse
                    // Npgsql span'ları ebeveynsiz kalır ve her SQL komutu ayrı bir kök trace olur.
                    .AddSource(EgitimUssuActivitySource.Name);

                Export(tracing, options);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(EgitimUssuMetrics.MeterName);

                Export(metrics, options);
            });

        return services;
    }

    private static void Export(TracerProviderBuilder tracing, ObservabilityOptions options)
    {
        if (options.HasOtlpEndpoint)
        {
            tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint));
            return;
        }

        // Toplayıcı adresi yoksa sessizce hiçbir şey yapmak yerine konsola yazarız:
        // "açtım ama hiçbir yere gitmiyor" durumu fark edilmeden kalmasın.
        tracing.AddConsoleExporter();
    }

    private static void Export(MeterProviderBuilder metrics, ObservabilityOptions options)
    {
        if (options.HasOtlpEndpoint)
        {
            metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint));
            return;
        }

        metrics.AddConsoleExporter();
    }
}
