using System.Diagnostics.Metrics;
using System.Reflection;
using EgitimUssu.Shared.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 1 / C-04: Uygulama metriklerinin sözleşmesi.
/// <para>
/// Kritik kural — <b>yüksek kardinaliteli etiket yok</b>: kullanıcı kimliği, e-posta veya token bir
/// metrik boyutu olursa zaman serisi sayısı kullanıcı sayısıyla birlikte büyür ve toplayıcı maliyeti
/// patlar. Bu yüzden kural yorumla değil, <b>API'nin kendisiyle</b> zorlanır: böyle bir parametre yok.
/// </para>
/// </summary>
public sealed class EgitimUssuMetricsTests
{
    /// <summary>Metrik boyutu olması yasak, yüksek kardinaliteli/PII alan adları.</summary>
    private static readonly string[] ForbiddenDimensions =
    [
        "userid", "user", "email", "phone", "token", "password", "studentid", "teacheruserid",
    ];

    [Fact]
    public void RecordPushSent_Should_Increment_Counter_With_Status_Tag()
    {
        using var probe = new MeterProbe("egitimussu.push.sent");

        probe.Metrics.RecordPushSent("Delivered");
        probe.Metrics.RecordPushSent("Delivered");
        probe.Metrics.RecordPushSent("TokenInvalid");

        Assert.Equal(2, probe.SumWhere("status", "Delivered"));
        Assert.Equal(1, probe.SumWhere("status", "TokenInvalid"));
    }

    [Fact]
    public void RecordEmailSent_Should_Separate_Success_And_Failure()
    {
        using var probe = new MeterProbe("egitimussu.email.sent");

        probe.Metrics.RecordEmailSent(success: true);
        probe.Metrics.RecordEmailSent(success: false);
        probe.Metrics.RecordEmailSent(success: false);

        Assert.Equal(1, probe.SumWhere("outcome", "success"));
        Assert.Equal(2, probe.SumWhere("outcome", "failure"));
    }

    [Fact]
    public void RecordOutboxDispatched_Should_Tag_Event_And_Outcome()
    {
        using var probe = new MeterProbe("egitimussu.outbox.dispatched");

        probe.Metrics.RecordOutboxDispatched("MessageSentDomainEvent", success: true);
        probe.Metrics.RecordOutboxDispatched("MessageSentDomainEvent", success: false);

        Assert.Equal(1, probe.SumWhere("outcome", "success"));
        Assert.Equal(1, probe.SumWhere("outcome", "failure"));
        Assert.Equal(2, probe.SumWhere("event", "MessageSentDomainEvent"));
    }

    /// <summary>Kuyruk derinliği <b>sayaç değil ölçüm</b>dur: son değer okunur, toplanmaz.</summary>
    [Fact]
    public void RecordOutboxPending_Should_Report_Last_Observed_Value()
    {
        using var probe = new MeterProbe("egitimussu.outbox.pending");

        probe.Metrics.RecordOutboxPending("Scheduling", 7);
        probe.Metrics.RecordOutboxPending("Scheduling", 3);

        Assert.Equal(3, probe.LastValue);
    }

    /// <summary>
    /// A-01/A-10: Kuyruk derinliği modül başına ayrı bir zaman serisi olmalıdır. Dispatcher tek turda
    /// 15 modülü dolaşır; etiket olmasaydı her modül bir öncekinin üzerine yazar ve dışa aktarılan
    /// değer yalnız <b>son</b> modülünki olurdu — dolu bir kuyruk boş görünür, alarm hiç ötmezdi.
    /// </summary>
    [Fact]
    public void RecordOutboxPending_Should_Keep_Modules_On_Separate_Series()
    {
        using var probe = new MeterProbe("egitimussu.outbox.pending");

        probe.Metrics.RecordOutboxPending("Scheduling", 10_000);
        probe.Metrics.RecordOutboxPending("Teachers", 0);

        Assert.Equal(10_000, probe.LastValueWhere("module", "Scheduling"));
        Assert.Equal(0, probe.LastValueWhere("module", "Teachers"));
    }

    /// <summary>
    /// A-01: Redis'e bağlı korumalar fail-open olduğu için kesinti hiçbir isteği hata döndürmez;
    /// bu sayaç "sessizce korumasız çalışıyoruz" durumunun tek dışa vurumudur.
    /// </summary>
    [Fact]
    public void RecordRedisUnavailable_Should_Separate_Protections()
    {
        using var probe = new MeterProbe("egitimussu.redis.unavailable");

        probe.Metrics.RecordRedisUnavailable("ratelimit");
        probe.Metrics.RecordEntitlementFallback();
        probe.Metrics.RecordRedisUnavailable("ratelimit");
        probe.Metrics.RecordEntitlementFallback();
        probe.Metrics.RecordRedisUnavailable("blacklist");

        Assert.Equal(2, probe.SumWhere("operation", "ratelimit"));
        Assert.Equal(1, probe.SumWhere("operation", "blacklist"));
    }

    /// <summary>
    /// Metrik adları toplayıcıda kalıcı sözleşmedir (alarm ve panolar bunlara bağlanır);
    /// sessizce değişirlerse alarmlar veri görmeden yeşile döner.
    /// </summary>
    [Fact]
    public void Instrument_Names_Should_Be_Stable()
    {
        using var probe = new MeterProbe(instrumentName: null);

        probe.Metrics.RecordOutboxPending("Scheduling", 1);
        probe.Metrics.RecordOutboxDispatched("X", success: true);
        probe.Metrics.RecordPushSent("Delivered");
        probe.Metrics.RecordEmailSent(success: true);
        probe.Metrics.RecordRedisUnavailable("ratelimit");
        probe.Metrics.RecordEntitlementFallback();

        Assert.Equal(
            [
                "egitimussu.email.sent",
                "egitimussu.entitlement.fallback",
                "egitimussu.outbox.dispatched",
                "egitimussu.outbox.pending",
                "egitimussu.push.sent",
                "egitimussu.redis.unavailable",
            ],
            probe.PublishedNames.Order().ToArray());
    }

    [Fact]
    public void Meter_Name_Should_Be_The_Documented_Scope()
    {
        Assert.Equal("EgitimUssu", EgitimUssuMetrics.MeterName);
    }

    /// <summary>
    /// C-04 kuralı API ile zorlanır: hiçbir kayıt metodu kullanıcı kimliği/PII almaz. Bir gün biri
    /// `RecordPushSent(string status, Guid userId)` yazmak isterse bu test kırmızıya döner.
    /// </summary>
    [Fact]
    public void Public_Api_Should_Not_Accept_High_Cardinality_Dimensions()
    {
        var offenders = typeof(EgitimUssuMetrics)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters()
                .Where(parameter => ForbiddenDimensions.Contains(
                    parameter.Name?.ToLowerInvariant() ?? string.Empty))
                .Select(parameter => $"{method.Name}({parameter.Name})"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Metrik API'si yüksek kardinaliteli boyut kabul etmemeli: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Tek bir <see cref="EgitimUssuMetrics"/> örneğinin ürettiği ölçümleri dinler.
    /// <para>
    /// Her sonda kendi <see cref="IMeterFactory"/>'sini kurar ve yalnız o fabrikanın kapsamındaki
    /// araçları dinler; aksi hâlde aynı adı taşıyan başka testlerin ölçümleri sayaca karışırdı.
    /// </para>
    /// </summary>
    private sealed class MeterProbe : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IMeterFactory _factory;
        private readonly MeterListener _listener;
        private readonly List<(string Name, long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> _measurements = [];

        public MeterProbe(string? instrumentName)
        {
            _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
            _factory = _provider.GetRequiredService<IMeterFactory>();
            Metrics = new EgitimUssuMetrics(_factory);

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (!ReferenceEquals(instrument.Meter.Scope, _factory))
                    {
                        return;
                    }

                    PublishedNames.Add(instrument.Name);
                    if (instrumentName is null || instrument.Name == instrumentName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };

            _listener.SetMeasurementEventCallback<long>(Record);
            _listener.SetMeasurementEventCallback<int>(
                (instrument, value, tags, _) => Record(instrument, value, tags, null));
            _listener.Start();
        }

        public EgitimUssuMetrics Metrics { get; }

        public List<string> PublishedNames { get; } = [];

        public long LastValue => _measurements[^1].Value;

        public long LastValueWhere(string tagKey, string tagValue) => _measurements
            .Where(row => row.Tags.Any(tag => tag.Key == tagKey && Equals(tag.Value, tagValue)))
            .Select(row => row.Value)
            .Last();

        public long SumWhere(string tagKey, string tagValue) => _measurements
            .Where(row => row.Tags.Any(tag => tag.Key == tagKey && Equals(tag.Value, tagValue)))
            .Sum(row => row.Value);

        public void Dispose()
        {
            _listener.Dispose();
            _provider.Dispose();
        }

        private void Record(
            Instrument instrument,
            long value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
        {
            _measurements.Add((instrument.Name, value, tags.ToArray()));
        }
    }
}
