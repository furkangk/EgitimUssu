using System.Text.RegularExpressions;

namespace EgitimUssu.Tests.Architecture;

/// <summary>
/// P16 T6 / K-19 — yerel saat kuralının kilidi. Gün/hafta/ay sınırı ve kullanıcıya gösterilen saat
/// <c>ILocalCalendar</c> üzerinden hesaplanır; UTC anından gün/hafta türeten kalıplar kaynakta <b>yasaktır</b>.
/// <para>
/// Tarama bilerek fazla yakalar (her <c>.Date</c> erişimi, her <c>DateOnly.FromDateTime(</c>, her <c>ISOWeek.Get…</c>,
/// UTC türünde <c>ToDateTime</c>, UTC adlı değişkenin takvim bileşeni): meşru kullanım <see cref="Allowed"/> listesine
/// <b>gerekçesiyle</b> girer. Anahtar dosya + satırın birebir metnidir (satır numarası değil) — aynı dosyada yeni bir
/// kullanım yine yakalanır. Artık eşleşmeyen giriş de testi kırar (bayat izin kalmaz; P17 T7 iki girişi siler).
/// </para>
/// Kapsam: <c>src/</c> altındaki tüm <c>.cs</c> (modüllerin API katmanı ve <c>API.Host</c> dahil; planın ilk hâli yalnız
/// <c>Application|Domain|Infrastructure</c> diyordu — <c>ReportingModule</c> ve panel BFF'si bu yüzden gözden kaçmıştı),
/// <c>bin/obj/Migrations</c> ve yorum satırları hariç.
/// </summary>
public sealed class LocalTimeRuleTests
{
    private static readonly Regex Forbidden = new(
        @"DateOnly\.FromDateTime\("
        + @"|\.Date\b"
        + @"|ISOWeek\.Get(Year|WeekOfYear)\("
        + @"|ToDateTime\([^)]*DateTimeKind\.Utc"
        + @"|(Utc|utc|UtcNow)\.(Year|Month|Day|DayOfWeek|Hour)\b",
        RegexOptions.Compiled);

    /// <summary>(dosya, satırın birebir metni, gerekçe).</summary>
    private static readonly (string Path, string Line, string Reason)[] Allowed =
    [
        // --- Dönüşümün kendisi / zaten yerel tarih üzerinde çalışanlar ---
        ("src/Shared/Infrastructure/LocalCalendar.cs",
            "public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));",
            "Kuralın tek uygulama noktası: önce yerel saate çevirir (K-19)."),
        ("src/Modules/Reporting/Domain/ReportingProjections.cs",
            "return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));",
            "Girdi yerel tarihin gece yarısıdır (ReportingPeriods.WeekOf(DateOnly)), UTC an değil."),
        ("src/Modules/Notifications/Infrastructure/ParentWeeklySummaryService.cs",
            "var isoWeek = ISOWeek.GetWeekOfYear(localMidnight);",
            "Girdi ILocalCalendar.ToLocalDate'ten gelen yerel gece yarısı (P16 T6)."),
        ("src/Modules/Notifications/Infrastructure/ParentWeeklySummaryService.cs",
            "var isoYear = ISOWeek.GetYear(localMidnight);",
            "Girdi ILocalCalendar.ToLocalDate'ten gelen yerel gece yarısı (P16 T6)."),
        ("src/Modules/Reporting/Application/ReportingAnalyticsFeatures.cs",
            "DateOnly.FromDateTime(System.Globalization.ISOWeek.ToDateTime(item.Year, item.WeekOfYear, DayOfWeek.Monday)),",
            "Takvim tarihi (ISO hafta → Pazartesi), saat dilimi ve UTC an yok."),
        ("src/Modules/Scheduling/Application/StudentCalendarQuery.cs",
            "var completedSet = completions.Select(c => (c.LessonId, c.Date)).ToHashSet();",
            "PlanCompletion.Date bir DateOnly özelliği (yerel gün), DateTime.Date değil."),

        // --- Tohum verisi: göreli zaman damgası üretir, gün/hafta kararı vermez ---
        ("src/API.Host/Seeding/DemoDataSeeder.cs",
            "var dueDateUtc = clock.UtcNow.Date.AddDays(isOverdue ? -5 : 10);",
            "Demo verisi (yalnız Development/Staging): göreli vade."),
        ("src/API.Host/Seeding/DemoDataSeeder.cs",
            "var fromUtc = clock.UtcNow.Date.AddDays(-HistoryWeeks * 7);",
            "Demo verisi: geçmiş penceresinin başı."),
        ("src/API.Host/Seeding/DemoDataSeeder.cs",
            "StudiedOnUtc: clock.UtcNow.Date.AddDays(-day * 3).AddHours(19),",
            "Demo verisi: göreli çalışma anı."),
        ("src/API.Host/Seeding/DemoDataSeeder.cs",
            "var monday = nowUtc.Date.AddDays(-(((int)nowUtc.DayOfWeek + 6) % 7));",
            "Demo verisi: ders geçmişinin haftalık hizası."),
    ];

    [Fact]
    public void Business_code_does_not_derive_days_weeks_or_months_from_utc_instants()
    {
        var root = GetRoot();
        var hits = Scan(root).ToArray();

        var violations = hits
            .Where(hit => !Allowed.Any(allowed => allowed.Path == hit.Path && allowed.Line == hit.Line))
            .Select(hit => $"{hit.Path}:{hit.LineNumber}: {hit.Line}")
            .ToArray();
        var stale = Allowed
            .Where(allowed => !hits.Any(hit => hit.Path == allowed.Path && hit.Line == allowed.Line))
            .Select(allowed => $"{allowed.Path}: {allowed.Line}")
            .ToArray();

        Assert.True(
            violations.Length == 0 && stale.Length == 0,
            "UTC anından gün/hafta/ay türetmek yasak (K-19) — ILocalCalendar kullanın ya da gerekçesiyle izinli listeye alın:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations)
            + Environment.NewLine + "İzinli listede artık kaynağa karşılık gelmeyen giriş var — silin:"
            + Environment.NewLine + string.Join(Environment.NewLine, stale));
    }

    [Fact]
    public void Scan_actually_reads_the_source_tree()
    {
        var files = SourceFiles(GetRoot()).ToArray();

        Assert.True(files.Length > 300, $"Yalnız {files.Length} kaynak dosyası tarandı — kök/kapsam yanlış olabilir.");
        Assert.Contains(files, file => file.EndsWith("src/Shared/Infrastructure/LocalCalendar.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.Contains("/Migrations/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("var day = DateOnly.FromDateTime(lesson.StartAtUtc);", true)]
    [InlineData("var today = now.Date;", true)]
    [InlineData("var start = DateTime.SpecifyKind(dayUtc.Date, DateTimeKind.Utc);", true)]
    [InlineData("var week = ISOWeek.GetYear(payload.CompletedOnUtc);", true)]
    [InlineData("var from = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);", true)]
    [InlineData("var weekday = nowUtc.DayOfWeek;", true)]
    [InlineData("var month = clock.UtcNow.Month;", true)]
    [InlineData("var today = calendar.ToLocalDate(utc);", false)]
    [InlineData("foreach (var slot in slots.Where(s => s.DayOfWeek == day.DayOfWeek))", false)]
    [InlineData("var due = payload.DueDateUtc;", false)]
    [InlineData("// DateOnly.FromDateTime(x) eskiden böyleydi", false)]
    [InlineData("/// <c>utc.Date</c> yasaktır", false)]
    [InlineData("var x = calendar.Today(); // eskiden now.Date", false)]
    public void Matcher_flags_utc_day_patterns_and_ignores_comments(string line, bool flagged)
        => Assert.Equal(flagged, IsForbidden(line));

    private static bool IsForbidden(string rawLine)
    {
        var code = StripComment(rawLine);
        return code.Length > 0 && Forbidden.IsMatch(code);
    }

    private static string StripComment(string rawLine)
    {
        var trimmed = rawLine.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        // Satır sonu yorumu: boşlukla başlayan "//" (URL'lerdeki "://" bölünmez).
        var comment = Regex.Match(trimmed, @"\s//");
        return comment.Success ? trimmed[..comment.Index].TrimEnd() : trimmed;
    }

    private static IEnumerable<(string Path, int LineNumber, string Line)> Scan(string root)
    {
        foreach (var file in SourceFiles(root))
        {
            var lines = File.ReadAllLines(Path.Combine(root, file));
            for (var index = 0; index < lines.Length; index++)
            {
                if (IsForbidden(lines[index]))
                {
                    yield return (file, index + 1, lines[index].Trim());
                }
            }
        }
    }

    /// <summary>Kökten göreli, <c>/</c> ayraçlı yollar (Windows, macOS ve CI aynı anahtarı üretir).</summary>
    private static IEnumerable<string> SourceFiles(string root)
        => Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => !file.Contains("/bin/", StringComparison.Ordinal)
                && !file.Contains("/obj/", StringComparison.Ordinal)
                && !file.Contains("/Migrations/", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal);

    private static string GetRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "global.json")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
