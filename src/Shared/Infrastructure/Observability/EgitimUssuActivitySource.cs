using System.Diagnostics;

namespace EgitimUssu.Shared.Infrastructure.Observability;

/// <summary>
/// C-04 / P13 Task 9: Uygulamanın kendi span'larını açtığı tek <see cref="ActivitySource"/>.
/// <para>
/// Enstrümantasyon paketleri (ASP.NET Core, HttpClient, Npgsql) yalnız kendi katmanlarını görür.
/// Aradaki adımlar — komut/sorgu dispatch'i, outbox turu, olay handler'ı — bu kaynaktan yayılır.
/// </para>
/// <para>
/// <b>Neden önemli:</b> enstrümante edilmemiş bir arka plan işi trace'i temizlemez, <i>kirletir</i>:
/// Npgsql her SQL komutunu yayar ve ebeveyni yoksa her biri ayrı bir <b>kök trace</b> olur.
/// P13 Task 8 ölçümünde 1500 trace'in <b>1488'i</b> bu şekilde oluşmuş, adları da veritabanı adı
/// (<c>egitimussu_staging</c>) olarak görünmüştü. Periyodik bir işçi eklerken span'ını da ekleyin.
/// </para>
/// </summary>
public static class EgitimUssuActivitySource
{
    /// <summary>Kaynak adı; <c>AddObservability</c> bunu <c>AddSource</c> ile kaydeder.</summary>
    public const string Name = "EgitimUssu";

    /// <summary>Paylaşılan kaynak örneği. Dinleyici yoksa <c>StartActivity</c> null döner — maliyet sıfırdır.</summary>
    public static readonly ActivitySource Instance = new(Name);
}
