using System.Diagnostics.Metrics;

namespace EgitimUssu.Shared.Infrastructure.Observability;

/// <summary>
/// C-04: Uygulamanın kendi iş metrikleri. ASP.NET Core / HttpClient / veritabanı metriklerini
/// enstrümantasyon paketleri zaten üretir; burada yalnız <b>bizim</b> bilmemiz gereken üç akış vardır:
/// outbox kuyruğu, push gönderimi ve e-posta gönderimi.
/// <para>
/// <b>Yüksek kardinaliteli etiket yasağı API ile zorlanır:</b> hiçbir metot kullanıcı kimliği,
/// e-posta veya token almaz. Böyle bir boyut, zaman serisi sayısını kullanıcı sayısıyla birlikte
/// büyütür ve toplayıcı maliyetini patlatır — kural yorumda kalsaydı ilk acelede delinirdi.
/// </para>
/// </summary>
public sealed class EgitimUssuMetrics
{
    /// <summary>Toplayıcıda bu uygulamanın metriklerini seçmek için kullanılan meter adı.</summary>
    public const string MeterName = "EgitimUssu";

    private readonly Gauge<int> _outboxPending;
    private readonly Counter<long> _outboxDispatched;
    private readonly Counter<long> _pushSent;
    private readonly Counter<long> _emailSent;
    private readonly Counter<long> _redisUnavailable;
    private readonly Counter<long> _entitlementFallback;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="meterFactory">DI'dan gelen meter fabrikası (test yalıtımı da buna dayanır).</param>
    public EgitimUssuMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _outboxPending = meter.CreateGauge<int>(
            "egitimussu.outbox.pending",
            unit: "{message}",
            description: "Bekleyen outbox mesajı sayısı (son dispatch turunda gözlenen).");

        _outboxDispatched = meter.CreateCounter<long>(
            "egitimussu.outbox.dispatched",
            unit: "{message}",
            description: "Yayınlanan outbox mesajları; olay tipine ve sonuca göre.");

        _pushSent = meter.CreateCounter<long>(
            "egitimussu.push.sent",
            unit: "{message}",
            description: "Push gönderim sonuçları (Delivered | TokenInvalid | TransientFailure).");

        _emailSent = meter.CreateCounter<long>(
            "egitimussu.email.sent",
            unit: "{message}",
            description: "E-posta gönderim sonuçları.");

        // A-01: Redis'e bağlı korumalar (hız sınırı, token kara listesi, giriş kilidi, idempotency)
        // fail-open çalışır — erişilemediğinde istisna yükselmez, istek başarılı görünür. Bu sayaç,
        // "sessizce korumasız çalışıyoruz" durumunun tek dışa vurumudur; alarmı buna kur.
        _redisUnavailable = meter.CreateCounter<long>(
            "egitimussu.redis.unavailable",
            unit: "{operation}",
            description: "Redis erişilemediği için fail-open geçilen işlem sayısı.");

        // A-11: Üyelik okuması hata yutup Free döndüğünde artan sayaç. Ödeme yapmış kullanıcının
        // sessizce Free'ye düşmesinin log dışında tek dışa vurumu budur.
        _entitlementFallback = meter.CreateCounter<long>(
            "egitimussu.entitlement.fallback",
            unit: "{user}",
            description: "Üyelik hakları okunamadığı için Free varsayılan kullanıcı sayısı.");
    }

    /// <summary>
    /// Kuyruk derinliğini bildirir. <b>Sayaç değil ölçümdür</b>: toplanmaz, son değeri okunur —
    /// "şu an kaç mesaj bekliyor" sorusunun cevabı budur ve alarm eşiği buna kurulur.
    /// </summary>
    /// <param name="module">Kuyruğun ait olduğu modül. <b>Zorunlu boyut:</b> outbox tablosu modül
    /// başına ayrıdır ve dispatcher tek turda 15 modülü dolaşır; etiketsiz bir gauge'da her modül
    /// bir öncekinin üzerine yazar ve dışa aktarılan değer yalnız son modülünki olur (kardinalite 15).</param>
    /// <param name="count">Bekleyen mesaj sayısı.</param>
    public void RecordOutboxPending(string module, int count) => _outboxPending.Record(
        count, new KeyValuePair<string, object?>("module", module));

    /// <summary>Bir outbox mesajının yayın sonucunu sayar.</summary>
    /// <param name="eventName">Olay tipi adı (sınırlı kardinalite: birkaç düzine değer).</param>
    /// <param name="success">Yayın başarılı mı.</param>
    public void RecordOutboxDispatched(string eventName, bool success) => _outboxDispatched.Add(
        1,
        new KeyValuePair<string, object?>("event", eventName),
        new KeyValuePair<string, object?>("outcome", Outcome(success)));

    /// <summary>Bir push gönderiminin sonucunu sayar.</summary>
    /// <param name="status">`Delivered` | `TokenInvalid` | `TransientFailure`.</param>
    public void RecordPushSent(string status) => _pushSent.Add(
        1, new KeyValuePair<string, object?>("status", status));

    /// <summary>Bir e-posta gönderiminin sonucunu sayar.</summary>
    /// <param name="success">Gönderim başarılı mı.</param>
    public void RecordEmailSent(bool success) => _emailSent.Add(
        1, new KeyValuePair<string, object?>("outcome", Outcome(success)));

    /// <summary>
    /// A-11: Üyelik hakları okunamadığı için Free varsayılan bir kullanıcıyı sayar.
    /// <para>
    /// Bu düşüş kullanıcıya hata olarak gitmez — ödeme yapmış biri sessizce premium özelliklerini
    /// kaybeder. Log satırı dışında hiçbir izi olmadığından sayaç alarmın tek dayanağıdır.
    /// </para>
    /// </summary>
    public void RecordEntitlementFallback() => _entitlementFallback.Add(1);

    /// <summary>Redis erişilemediği için fail-open geçilen bir işlemi sayar.</summary>
    /// <param name="operation">Etkilenen koruma: `ratelimit` | `blacklist` | `login-throttle` | `idempotency`
    /// (sınırlı kardinalite — kullanıcıya özgü hiçbir değer alınmaz).</param>
    public void RecordRedisUnavailable(string operation) => _redisUnavailable.Add(
        1, new KeyValuePair<string, object?>("operation", operation));

    private static string Outcome(bool success) => success ? "success" : "failure";
}
