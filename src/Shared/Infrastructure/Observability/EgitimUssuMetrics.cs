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
    }

    /// <summary>
    /// Kuyruk derinliğini bildirir. <b>Sayaç değil ölçümdür</b>: toplanmaz, son değeri okunur —
    /// "şu an kaç mesaj bekliyor" sorusunun cevabı budur ve alarm eşiği buna kurulur.
    /// </summary>
    /// <param name="count">Bekleyen mesaj sayısı.</param>
    public void RecordOutboxPending(int count) => _outboxPending.Record(count);

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

    private static string Outcome(bool success) => success ? "success" : "failure";
}
