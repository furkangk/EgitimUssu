using System.Globalization;
using System.Text;

namespace EgitimUssu.Shared.Kernel;

/// <summary>Azalan (UTC an, kimlik) sıralı liste için doğrulanmış sayfa isteği.</summary>
public sealed record KeysetPage(int Limit, DateTime? BeforeUtc = null, Guid? BeforeId = null)
{
    public static readonly Error Invalid = new("shared.invalid_pagination", "Sayfa sınırı veya imleci geçersiz.");

    public static bool TryCreate(string? after, int? limit, out KeysetPage page)
    {
        page = new(limit ?? 50);
        if (page.Limit is < 1 or > 100) return false;
        if (after is null) return true;
        if (after.Length is 0 or > 128) return false;
        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(after.Replace('-', '+').Replace('_', '/').PadRight((after.Length + 3) / 4 * 4, '=')));
            var parts = text.Split(':');
            if (parts.Length != 3 || parts[0] != "1"
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks
                || !Guid.TryParseExact(parts[2], "N", out var id)) return false;
            page = page with { BeforeUtc = new DateTime(ticks, DateTimeKind.Utc), BeforeId = id };
            return true;
        }
        catch (FormatException) { return false; }
    }

    public static string Encode(DateTime utc, Guid id)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(FormattableString.Invariant($"1:{utc.Ticks}:{id:N}")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public PagedResult<T> Finish<T>(IReadOnlyCollection<T> rows, Func<T, DateTime> time, Func<T, Guid> id)
    {
        var items = rows.Take(Limit).ToArray();
        var cursor = rows.Count > Limit ? Encode(time(items[^1]), id(items[^1])) : null;
        return new(items, 1, Limit, null, cursor);
    }
}
