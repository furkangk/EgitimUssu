namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// P16 T1 / K-19: Yerel takvimin zaman dilimini açılışta çözer (<see cref="StorageOptionsGuard"/> deseni:
/// <see cref="Validate"/> nedeni döner, <see cref="Resolve"/> fırlatır).
/// <para>
/// Kimlik IANA biçimindedir (<c>Europe/Istanbul</c>). Windows'ta .NET onu ICU ile Windows kimliğine
/// eşler; Linux'ta <c>/usr/share/zoneinfo</c> (paket <c>tzdata</c>) gerekir. Çözülemeyen kimlikle
/// uygulama açılmaz: aksi halde her "bugün" ve her bildirim saati ilk istekte 500'e dönerdi.
/// </para>
/// </summary>
public static class LocalCalendarGuard
{
    /// <summary>Yapılandırma anahtarı.</summary>
    public const string ConfigurationKey = "Localization:TimeZone";

    /// <summary>Anahtar verilmezse kullanılan dilim.</summary>
    public const string DefaultTimeZoneId = "Europe/Istanbul";

    /// <summary>Kimlik çözülemiyorsa nedenini döndürür; çözülüyorsa <c>null</c>.</summary>
    /// <param name="timeZoneId">IANA zaman dilimi kimliği.</param>
    public static string? Validate(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return $"{ConfigurationKey} boş olamaz. Anahtarı hiç vermeyin (varsayılan \"{DefaultTimeZoneId}\") "
                + "ya da Localization__TimeZone ile geçerli bir IANA kimliği verin.";
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return null;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return $"{ConfigurationKey} \"{timeZoneId}\" çözülemedi ({exception.GetType().Name}). "
                + "Kimlik IANA biçiminde olmalı (ör. \"Europe/Istanbul\"); Linux imajında tzdata paketi kurulu olmalı.";
        }
    }

    /// <summary>Kimliği zaman dilimine çevirir; çözülemiyorsa açılışı fail-fast durdurur.</summary>
    /// <param name="timeZoneId">IANA zaman dilimi kimliği.</param>
    /// <returns>Çözülen zaman dilimi.</returns>
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        var error = Validate(timeZoneId);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!.Trim());
    }
}
