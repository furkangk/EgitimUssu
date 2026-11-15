using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

public sealed record CreateLessonScheduleCommand(
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? MeetingUrl,
    string? Notes,
    bool AllowTimeOffOverride = false) : ICommand<Result<LessonScheduleResponse>>;

public sealed record UpdateLessonScheduleCommand(
    Guid LessonId,
    string Subject,
    ScheduledLessonFormat LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? MeetingUrl,
    string? Notes,
    bool AllowTimeOffOverride = false) : ICommand<Result<LessonScheduleResponse>>;

public enum OccurrenceScope
{
    Single = 1,
    ThisAndFuture = 2,
    All = 3
}

public sealed record CancelLessonScheduleCommand(
    Guid LessonId,
    CancellationReason Reason,
    bool IsChargeable,
    string? CancellationNote,
    OccurrenceScope? Scope = null,
    DateTime? OccurrenceStartAtUtc = null) : ICommand<Result<LessonScheduleResponse>>;

public sealed record RescheduleLessonScheduleCommand(
    Guid LessonId,
    DateTime NewStartAtUtc,
    DateTime NewEndAtUtc,
    string? Note,
    OccurrenceScope? Scope = null,
    DateTime? OccurrenceStartAtUtc = null,
    bool AllowTimeOffOverride = false) : ICommand<Result<LessonScheduleResponse>>;

public sealed record DeleteLessonScheduleCommand(Guid LessonId) : ICommand<Result>;

public sealed record GetLessonScheduleByIdQuery(Guid LessonId) : IQuery<Result<LessonScheduleResponse>>;

public sealed record ListLessonSchedulesForTeacherQuery(
    Guid TeacherUserId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<LessonScheduleResponse>>>;

public sealed record ListLessonSchedulesForStudentQuery(
    Guid StudentId,
    DateTime StartAtUtc,
    DateTime EndAtUtc) : IQuery<Result<IReadOnlyCollection<LessonScheduleResponse>>>;

public sealed record LessonScheduleResponse(
    Guid Id,
    Guid? TeacherUserId,
    Guid StudentId,
    string Subject,
    string LessonFormat,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string TimeZone,
    string? RecurrenceRule,
    string Status,
    int ReminderOffsetMinutes,
    string? LocationLabel,
    string? MeetingUrl,
    string? Notes,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc,
    DateTime? OriginalStartAtUtc,
    string? CancellationReason,
    bool IsChargeable,
    DateTime OccurrenceStartAtUtc);

public interface ILessonScheduleRepository
{
    Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken);

    // P16 T4 / N-07: `HasTeacherConflictAsync` ve `ListForTeacherAsync` kaldırıldı — ikisi de yalnız serinin çapa satırını
    // karşılaştırıyordu. Çakışma ve tatil listesi `TeacherScheduleConflicts` ile oluşumlar üzerinden hesaplanır.

    Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Öğrencinin iptal edilmemiş dersleri; başlangıcı <paramref name="untilUtc"/>'ye kadar olanlar.
    /// Tekrar kuralları uygulama katmanında genişletildiği için alt sınır uygulanmaz.
    /// </summary>
    Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Öğretmenin iptal edilmemiş dersleri; başlangıcı <paramref name="untilUtc"/>'ye kadar olanlar.
    /// (M04-1) Tekrar kuralları uygulama katmanında genişletildiği için alt sınır uygulanmaz —
    /// aylar önce başlamış haftalık bir seri de bu listeye girer.
    /// </summary>
    Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken);

    Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken);

    void Remove(LessonSchedule lessonSchedule);

    /// <summary>
    /// P16 T3 / N-05: Oluşum istisnasını yazar; aynı <c>(SeriesLessonScheduleId, OriginalStartAtUtc)</c> varsa
    /// yeni eylem/saat/notla günceller (iki erteleme ya da erteleme + iptal tek satırda kalır, son değer geçerli).
    /// </summary>
    Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// P16 T4 / N-07: Bir öğretmenin takvimine yazan işleri serileştirir. Uygulama (Npgsql) işi tek transaction'da,
/// öğretmen başına <c>pg_advisory_xact_lock</c> altında çalıştırır ve yalnız başarılı sonuçta commit eder; böylece
/// "çakışma kontrolü → yaz → kaydet" eşzamanlı iki istekte de tek kazanan verir. İlişkisel olmayan sağlayıcıda
/// (InMemory) işi doğrudan çalıştırır.
/// <para>
/// <c>work</c> bağlantı kopmasında bir bütün olarak yeniden denenebilir (`EnableRetryOnFailure`):
/// okumaları her denemede tekrarlamalı, aggregate değişikliğini bir kez uygulamalıdır.
/// </para>
/// </summary>
public interface ITeacherScheduleLock
{
    Task<TResult> RunExclusiveAsync<TResult>(
        Guid teacherUserId,
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
        where TResult : Result;
}

/// <summary>
/// P16 T4 / N-07: Çakışma kontrolünün ufku — adayın ilk oluşumundan itibaren kaç hafta genişletileceği
/// (config <c>Scheduling:ConflictHorizonWeeks</c>, varsayılan 12).
/// </summary>
public sealed record SchedulingConflictSettings(int HorizonWeeks)
{
    public const string ConfigurationKey = "Scheduling:ConflictHorizonWeeks";
    public const int DefaultHorizonWeeks = 12;
    public const int MaxHorizonWeeks = 104;

    public static SchedulingConflictSettings Default { get; } = new(DefaultHorizonWeeks);

    public TimeSpan Horizon => TimeSpan.FromDays(7 * HorizonWeeks);

    /// <summary>
    /// Config değerini çözer: boşsa varsayılan; aralık dışı ya da sayı olmayan değer açılışı durdurur — sessizce
    /// varsayılana düşmek çakışma kontrolünü fark edilmeden daraltırdı.
    /// </summary>
    public static SchedulingConflictSettings Parse(string? configuredWeeks)
    {
        if (string.IsNullOrWhiteSpace(configuredWeeks))
        {
            return Default;
        }

        if (!int.TryParse(configuredWeeks.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var weeks)
            || weeks < 1
            || weeks > MaxHorizonWeeks)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} 1 ile {MaxHorizonWeeks} arasında bir tam sayı olmalı (verilen: '{configuredWeeks}').");
        }

        return new SchedulingConflictSettings(weeks);
    }
}

/// <summary>
/// P16 T4 / N-07: Öğretmenin ders çakışması ve tatil bloğu kontrolü <b>somut oluşumlar</b> üzerinden yapılır — eskiden
/// yalnız serinin ilk satırı (çapa) karşılaştırılıyordu, Eylül'de başlamış haftalık Salı serisi Kasım'ın Salı'sını
/// korumuyordu. Mevcut dersler öğretmen listesiyle aynı yoldan genişletilir (dilim <c>SeriesZone</c>, serinin tüm
/// istisnaları; Öğrenilenler #222 b, #223 c): iptal edilen oluşum yer tutmaz, ertelenen oluşum yeni saatinde tutar.
/// </summary>
internal static class TeacherScheduleConflicts
{
    public static readonly Error LessonConflict = new("scheduling.teacher_conflict", "Ogretmenin bu zaman araliginda baska bir dersi var.");

    // Mesaj öğretmene gösterilir (mobil erteleme yolu sunucu mesajını basar); override bayrağının adı burada geçmez.
    public static readonly Error TimeOffConflict = new(
        "scheduling.time_off_conflict",
        "Bu saat, tatil / müsait değil olarak işaretlediğin bir zamana denk geliyor.");

    /// <summary>
    /// Adayın denetlenecek oluşumları. Tekil aday her zaman kendi anında denetlenir. Tekrarlı aday
    /// <c>max(çapa, şimdi) + ufuk</c>'a kadar genişletilir; başlangıç <paramref name="includePastOccurrences"/>'a bağlıdır:
    /// <list type="bullet">
    /// <item><b>Oluşturma</b> (<c>true</c>): çapadan — geçmişe girilen serinin her oluşumu yenidir, çapa dahil denetlenir
    /// (COUNT'u tükenmiş geçmiş seri de boş listeyle kontrolü atlamaz).</item>
    /// <item><b>Güncelleme / seri ertelemesi</b> (<c>false</c>): <c>max(çapa, şimdi)</c>'den — Eylül'de çapalanmış serinin
    /// notunu düzeltmek geçmişteki (bu kontrolden önce yazılmış) bir çakışma yüzünden reddedilmesin.</item>
    /// </list>
    /// <paramref name="ownExceptions"/> serinin kendi istisnalarıdır (güncellemede iptal edilmiş kendi oluşumu yer tutmaz).
    /// </summary>
    public static IReadOnlyList<ScheduleOccurrence> CandidateOccurrences(
        DateTime startAtUtc,
        DateTime endAtUtc,
        string? recurrenceRule,
        TimeZoneInfo zone,
        IReadOnlyCollection<OccurrenceOverride> ownExceptions,
        DateTime nowUtc,
        TimeSpan horizon,
        bool includePastOccurrences)
    {
        if (string.IsNullOrWhiteSpace(recurrenceRule))
        {
            return new[] { new ScheduleOccurrence(startAtUtc, endAtUtc) };
        }

        var futureFromUtc = startAtUtc > nowUtc ? startAtUtc : nowUtc;
        var fromUtc = includePastOccurrences ? startAtUtc : futureFromUtc;
        return RecurrenceExpander
            .Expand(startAtUtc, endAtUtc, recurrenceRule, zone, fromUtc, futureFromUtc + horizon, ownExceptions)
            .Where(occurrence => !occurrence.IsCancelled)
            .ToArray();
    }

    /// <summary>
    /// Öğretmenin <c>[fromUtc, toUtc)</c> ile kesişen, iptal edilmemiş somut oluşumları (seriler genişletilmiş,
    /// istisnalar uygulanmış). Tatil oluştururken çakışan derslerin listesi de buradan gelir.
    /// </summary>
    public static async Task<IReadOnlyList<(LessonSchedule Lesson, ScheduleOccurrence Occurrence)>> ListOccurrencesAsync(
        ILessonScheduleRepository repository,
        ILocalCalendar calendar,
        Guid teacherUserId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var lessons = await repository.ListActiveForTeacherUntilAsync(teacherUserId, toUtc, cancellationToken);
        if (lessons.Count == 0)
        {
            return Array.Empty<(LessonSchedule, ScheduleOccurrence)>();
        }

        var exceptionsByLesson = (await repository.ListExceptionsForTeacherAsync(teacherUserId, cancellationToken))
            .GroupBy(x => x.SeriesLessonScheduleId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<OccurrenceOverride>)group.Select(ToOverride).ToArray());

        var found = new List<(LessonSchedule, ScheduleOccurrence)>();
        foreach (var lesson in lessons)
        {
            var exceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var own) ? own : Array.Empty<OccurrenceOverride>();

            // Genişletici tekrarlı oluşumu BAŞLANGICINA göre süzer: pencereden önce başlayıp içine taşan oluşum
            // (16:30–17:30 dersi 17:00 adayına karşı) kaybolmasın diye pencere ders süresi kadar geriden açılır.
            var duration = lesson.EndAtUtc > lesson.StartAtUtc ? lesson.EndAtUtc - lesson.StartAtUtc : TimeSpan.FromHours(1);
            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, calendar.SeriesZone(lesson.TimeZone),
                fromUtc - duration, toUtc, exceptions))
            {
                if (!occurrence.IsCancelled && Overlaps(occurrence.StartAtUtc, occurrence.EndAtUtc, fromUtc, toUtc))
                {
                    found.Add((lesson, occurrence));
                }
            }
        }

        return found;
    }

    /// <summary>
    /// İlk çakışma hatası ya da <c>null</c>. Ders çakışması tatilden önce bakılır ve override edilemez; kesişim kesindir
    /// (arka arkaya dersler serbest). <paramref name="isSelf"/> adayın kendisinin eski hâlini dışarıda tutar.
    /// </summary>
    public static async Task<Error?> FindAsync(
        ILessonScheduleRepository lessons,
        ITimeOffBlockRepository timeOff,
        ILocalCalendar calendar,
        Guid teacherUserId,
        IReadOnlyList<ScheduleOccurrence> candidates,
        Func<LessonSchedule, ScheduleOccurrence, bool> isSelf,
        bool allowTimeOffOverride,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var fromUtc = candidates.Min(candidate => candidate.StartAtUtc);
        var toUtc = candidates.Max(candidate => candidate.EndAtUtc);

        foreach (var (lesson, occurrence) in await ListOccurrencesAsync(lessons, calendar, teacherUserId, fromUtc, toUtc, cancellationToken))
        {
            if (!isSelf(lesson, occurrence)
                && candidates.Any(candidate => Overlaps(candidate.StartAtUtc, candidate.EndAtUtc, occurrence.StartAtUtc, occurrence.EndAtUtc)))
            {
                return LessonConflict;
            }
        }

        if (allowTimeOffOverride)
        {
            return null;
        }

        var blocks = await timeOff.ListForTeacherAsync(teacherUserId, fromUtc, toUtc, cancellationToken);
        return blocks.Any(block => candidates.Any(candidate => Overlaps(candidate.StartAtUtc, candidate.EndAtUtc, block.StartAtUtc, block.EndAtUtc)))
            ? TimeOffConflict
            : null;
    }

    public static async Task<IReadOnlyCollection<OccurrenceOverride>> OwnExceptionsAsync(
        ILessonScheduleRepository repository, LessonSchedule lesson, string? recurrenceRule, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(recurrenceRule)
            ? Array.Empty<OccurrenceOverride>()
            : (await repository.ListExceptionsForSeriesAsync(lesson.Id, cancellationToken)).Select(ToOverride).ToArray();

    private static OccurrenceOverride ToOverride(LessonOccurrenceException exception)
        => new(exception.OriginalStartAtUtc, exception.Action, exception.OverrideStartAtUtc, exception.OverrideEndAtUtc);

    private static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd)
        => start < otherEnd && end > otherStart;
}

/// <summary>
/// P17 T1 / N-01 (K-20): tekrarlı derste iptal/erteleme kapsamı zorunludur — eskiden kapsamsız istek <c>All</c>
/// sayılıyordu ve "bu haftanın iptali" bütün seriyi (geçmiş haftalar dahil) iptal ediyordu. <c>Single</c>/<c>ThisAndFuture</c>
/// anahtar ister ve anahtar serinin <b>orijinal</b> oluşumu olmalıdır (master §4; görünen ertelenmiş saat değil — #223 g).
/// Tekil derste kapsam yok sayılır (<c>All</c>).
/// </summary>
internal static class OccurrenceScopeRules
{
    public static readonly Error ScopeRequired = new(
        "scheduling.scope_required",
        "Tekrarlı derste kapsam seçilmeli: yalnız bu ders, bu ve sonrakiler ya da tüm seri.");

    public static readonly Error OccurrenceNotFound = new(
        "scheduling.occurrence_not_found",
        "Belirtilen ders tarihi bu serinin bir dersi değil.");

    /// <summary>Kapsamı çözer; kural ihlalinde hatayı, aksi halde etkili kapsamı döner.</summary>
    public static (Error? Error, OccurrenceScope Scope) Resolve(
        LessonSchedule lesson,
        OccurrenceScope? requested,
        DateTime? occurrenceStartAtUtc,
        ILocalCalendar calendar,
        Error invalidRequest)
    {
        if (requested is { } given && !Enum.IsDefined(given))
        {
            return (invalidRequest, OccurrenceScope.All);
        }

        if (string.IsNullOrWhiteSpace(lesson.RecurrenceRule))
        {
            return (null, OccurrenceScope.All);
        }

        if (requested is not { } scope)
        {
            return (ScopeRequired, OccurrenceScope.All);
        }

        if (scope == OccurrenceScope.All)
        {
            return (null, scope);
        }

        if (occurrenceStartAtUtc is not { } key)
        {
            return (invalidRequest, scope);
        }

        return IsOccurrence(lesson, key, calendar) ? (null, scope) : (OccurrenceNotFound, scope);
    }

    public static bool IsOccurrence(LessonSchedule lesson, DateTime occurrenceStartAtUtc, ILocalCalendar calendar)
        => RecurrenceExpander.IsOccurrence(
            lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, calendar.SeriesZone(lesson.TimeZone), occurrenceStartAtUtc);
}

/// <summary>P17 T2: RRULE içindeki toplam <c>COUNT</c> değerini eski ve yeni seri arasında paylaştırır.</summary>
internal static class SeriesSplitRules
{
    private static readonly System.Text.RegularExpressions.Regex CountPattern = new(
        @"\bCOUNT=(\d+)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static string? RuleForNewSeries(LessonSchedule lesson, DateTime cutoffUtc, DateTime newStartAtUtc, ILocalCalendar calendar)
    {
        var rule = lesson.RecurrenceRule;
        if (string.IsNullOrWhiteSpace(rule))
        {
            return rule;
        }

        var match = CountPattern.Match(rule);
        if (match.Success && int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var totalCount))
        {
            var beforeCutoff = RecurrenceExpander.Expand(lesson.StartAtUtc, lesson.EndAtUtc, rule,
                calendar.SeriesZone(lesson.TimeZone), lesson.StartAtUtc, cutoffUtc.AddTicks(-1)).Count();
            var remaining = totalCount - beforeCutoff;
            if (remaining < 1) throw new InvalidOperationException("Bölme noktasından sonra seride oluşum kalmadı.");
            rule = CountPattern.Replace(rule, $"COUNT={remaining}", 1);
        }
        return RecurrenceExpander.ShiftRule(rule, cutoffUtc, newStartAtUtc, calendar.SeriesZone(lesson.TimeZone));
    }
}

public sealed class CreateLessonScheduleCommandHandler : ICommandHandler<CreateLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private readonly ILessonScheduleRepository _repository;
    private readonly ITimeOffBlockRepository _timeOff;
    private readonly ITeacherScheduleLock _scheduleLock;
    private readonly ILocalCalendar _calendar;
    private readonly SchedulingConflictSettings _settings;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        ITimeOffBlockRepository timeOff,
        ITeacherScheduleLock scheduleLock,
        ILocalCalendar calendar,
        SchedulingConflictSettings settings,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _timeOff = timeOff;
        _scheduleLock = scheduleLock;
        _calendar = calendar;
        _settings = settings;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(CreateLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRange);
        }

        // Varlık kilitten önce kurulur: bağlantı kopmasında iş yeniden denenirse aynı nesne yeniden eklenir,
        // yeni kimlikli ikinci bir ders doğmaz. Çakışma varsa hiç izlenmez/kaydedilmez.
        var lesson = new LessonSchedule(
            _idGenerator.New(),
            command.TeacherUserId,
            command.StudentId,
            command.Subject.Trim(),
            command.LessonFormat,
            command.StartAtUtc,
            command.EndAtUtc,
            command.TimeZone.Trim(),
            command.RecurrenceRule?.Trim(),
            LessonScheduleStatus.Planned,
            command.ReminderOffsetMinutes,
            command.LocationLabel?.Trim(),
            command.MeetingUrl?.Trim(),
            command.Notes?.Trim(),
            _clock.UtcNow);

        var candidates = TeacherScheduleConflicts.CandidateOccurrences(
            lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone),
            Array.Empty<OccurrenceOverride>(), _clock.UtcNow, _settings.Horizon, includePastOccurrences: true);

        // P16 T4 / N-07: kontrol + ekleme + kaydetme öğretmen kilidinde, tek transaction'da — eşzamanlı iki istekten biri 409.
        return await _scheduleLock.RunExclusiveAsync(command.TeacherUserId, async token =>
        {
            var conflict = await TeacherScheduleConflicts.FindAsync(
                _repository, _timeOff, _calendar, command.TeacherUserId, candidates,
                static (_, _) => false, command.AllowTimeOffOverride, token);
            if (conflict is not null)
            {
                return Result<LessonScheduleResponse>.Failure(conflict);
            }

            await _repository.AddAsync(lesson, token);
            await _repository.SaveChangesAsync(token);
            // Y1: Hatirlatma, LessonScheduledDomainEvent -> outbox -> Notifications handler yoluyla olusturulur;
            // Scheduling artik Notifications'a senkron/dogrudan yazmaz (modul izolasyonu + atomiklik).
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }, cancellationToken);
    }
}

public sealed class UpdateLessonScheduleCommandHandler : ICommandHandler<UpdateLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Yalnizca planli ders duzenlenebilir.");
    private readonly ILessonScheduleRepository _repository;
    private readonly ITimeOffBlockRepository _timeOff;
    private readonly ITeacherScheduleLock _scheduleLock;
    private readonly ILocalCalendar _calendar;
    private readonly SchedulingConflictSettings _settings;
    private readonly IClock _clock;

    public UpdateLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        ITimeOffBlockRepository timeOff,
        ITeacherScheduleLock scheduleLock,
        ILocalCalendar calendar,
        SchedulingConflictSettings settings,
        IClock clock)
    {
        _repository = repository;
        _timeOff = timeOff;
        _scheduleLock = scheduleLock;
        _calendar = calendar;
        _settings = settings;
        _clock = clock;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(UpdateLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.EndAtUtc <= command.StartAtUtc)
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRange);
        }

        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        if (!lesson.IsEditable)
        {
            return Result<LessonScheduleResponse>.Failure(NotEditable);
        }

        var teacherUserId = lesson.TeacherUserId ?? Guid.Empty;
        var recurrenceRule = command.RecurrenceRule?.Trim();
        var zone = _calendar.SeriesZone(command.TimeZone);
        var applied = false;

        // P16 T4 / N-07: dersin kendisi hariç; adaya serinin kendi istisnaları uygulanır (iptal edilmiş kendi oluşumu
        // yer tutmaz). Aggregate yalnız bir kez değişir — iş yeniden denenirse olay ikinci kez yayılmasın.
        return await _scheduleLock.RunExclusiveAsync(teacherUserId, async token =>
        {
            var completionError = await OccurrenceCompletionRules.CheckAsync(_repository, lesson, OccurrenceScope.All, null, token);
            if (completionError is not null) return Result<LessonScheduleResponse>.Failure(completionError);
            var ownExceptions = await TeacherScheduleConflicts.OwnExceptionsAsync(_repository, lesson, recurrenceRule, token);
            var candidates = TeacherScheduleConflicts.CandidateOccurrences(
                command.StartAtUtc, command.EndAtUtc, recurrenceRule, zone, ownExceptions, _clock.UtcNow, _settings.Horizon, includePastOccurrences: false);
            var conflict = await TeacherScheduleConflicts.FindAsync(
                _repository, _timeOff, _calendar, teacherUserId, candidates,
                (other, _) => other.Id == lesson.Id, command.AllowTimeOffOverride, token);
            if (conflict is not null)
            {
                return Result<LessonScheduleResponse>.Failure(conflict);
            }

            if (!applied)
            {
                lesson.UpdateDetails(
                    command.Subject.Trim(),
                    command.LessonFormat,
                    command.StartAtUtc,
                    command.EndAtUtc,
                    command.TimeZone.Trim(),
                    recurrenceRule,
                    command.ReminderOffsetMinutes,
                    command.LocationLabel?.Trim(),
                    command.MeetingUrl?.Trim(),
                    command.Notes?.Trim(),
                    _clock.UtcNow);
                applied = true;
            }

            await _repository.SaveChangesAsync(token);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }, cancellationToken);
    }
}

public sealed class CancelLessonScheduleCommandHandler(
    ILessonScheduleRepository repository, ITeacherScheduleLock scheduleLock, ILocalCalendar calendar,
    IClock clock, IIdGenerator idGenerator, ICancellationPolicyDirectory policies, IStudentDirectory students)
    : ICommandHandler<CancelLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    public async Task<Result<LessonScheduleResponse>> Handle(CancelLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var initial = await repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (initial is null) return Result<LessonScheduleResponse>.Failure(new("scheduling.lesson_not_found", "Ders planı bulunamadı."));
        return await scheduleLock.RunExclusiveAsync(initial.TeacherUserId ?? Guid.Empty, async token =>
        {
            var lesson = await repository.GetByIdAsync(command.LessonId, token);
            if (lesson is null) return Result<LessonScheduleResponse>.Failure(new("scheduling.lesson_not_found", "Ders planı bulunamadı."));
            if (lesson.Status == LessonScheduleStatus.Completed) return Result<LessonScheduleResponse>.Failure(OccurrenceCompletionRules.Completed);
            if (!lesson.IsEditable) return Result<LessonScheduleResponse>.Failure(new("scheduling.not_editable", "Yalnızca planlı ders iptal edilebilir."));
            if (!Enum.IsDefined(command.Reason) || command.CancellationNote?.Length > 500)
                return Result<LessonScheduleResponse>.Failure(new("scheduling.invalid_request", "İptal nedeni geçersiz."));
            var (error, scope) = OccurrenceScopeRules.Resolve(lesson, command.Scope, command.OccurrenceStartAtUtc, calendar,
                new("scheduling.invalid_request", "Tekrar kapsamı ve ders tarihi eksik veya hatalı."));
            if (error is not null) return Result<LessonScheduleResponse>.Failure(error);
            var completionError = await OccurrenceCompletionRules.CheckAsync(repository, lesson, scope, command.OccurrenceStartAtUtc, token);
            if (completionError is not null) return Result<LessonScheduleResponse>.Failure(completionError);
            var recurring = !string.IsNullOrWhiteSpace(lesson.RecurrenceRule);
            var key = recurring ? command.OccurrenceStartAtUtc : lesson.StartAtUtc;
            var exceptions = await repository.ListExceptionsForSeriesAsync(lesson.Id, token);
            var targetException = exceptions.SingleOrDefault(x => x.OriginalStartAtUtc == key);
            if (scope == OccurrenceScope.Single && targetException?.Action is OccurrenceExceptionAction.Cancelled or OccurrenceExceptionAction.Skipped)
                return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
            var now = clock.UtcNow;
            var effective = targetException?.OverrideStartAtUtc ?? key;
            var policy = lesson.TeacherUserId is { } teacher
                ? await policies.GetCancellationPolicyAsync(teacher, token) : TeacherCancellationPolicy.Disabled;
            var rate = lesson.TeacherUserId is { } teacherId && command.IsChargeable && policy.ChargePercent > 0m
                && command.Reason == CancellationReason.StudentCancelled
                ? await students.GetAgreedLessonRateAsync(teacherId, lesson.StudentId, token) : null;
            var chargeable = false;
            if (rate is not null)
            {
                var overrides = exceptions.Select(x => new OccurrenceOverride(x.OriginalStartAtUtc, x.Action, x.OverrideStartAtUtc, x.OverrideEndAtUtc)).ToArray();
                var candidates = RecurrenceExpander.Expand(lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule,
                    calendar.SeriesZone(lesson.TimeZone), now, now.AddHours(policy.NoticeHours), overrides);
                foreach (var occurrence in candidates.Where(x => !x.IsCancelled && !x.IsCompleted))
                {
                    var original = occurrence.RecurrenceIdUtc;
                    if (recurring && scope == OccurrenceScope.Single && original != key) continue;
                    if (recurring && scope == OccurrenceScope.ThisAndFuture && original < key) continue;
                    var amount = policy.Charge(occurrence.StartAtUtc, now, rate.Amount, true, command.IsChargeable);
                    if (amount <= 0m) continue;
                    lesson.RecordOccurrenceCharge(original, amount, rate.Currency);
                    chargeable = true;
                }
            }
            lesson.RecordCancellation(recurring ? scope.ToString() : "All", key, effective, now);
            if (recurring && scope == OccurrenceScope.Single && key is { } occurrenceKey)
                await repository.UpsertExceptionAsync(new LessonOccurrenceException(idGenerator.New(), lesson.Id, occurrenceKey,
                    OccurrenceExceptionAction.Cancelled, null, null,
                    command.CancellationNote?.Trim(), now), token);
            else if (recurring && scope == OccurrenceScope.ThisAndFuture && key is { } cutoff)
                lesson.EndSeriesBefore(cutoff, now);
            else lesson.Cancel(command.Reason, chargeable, command.CancellationNote?.Trim(), now);
            await repository.SaveChangesAsync(token);
            return Result<LessonScheduleResponse>.Success(lesson.ToResponse());
        }, cancellationToken);
    }
}

public sealed class RescheduleLessonScheduleCommandHandler : ICommandHandler<RescheduleLessonScheduleCommand, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Ders baslangic ve bitis araligi gecersiz.");
    private static readonly Error NotEditable = new("scheduling.not_editable", "Yalnizca planli ders ertelenebilir.");
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Tekrar kapsami ve ders tarihi eksik veya hatali.");
    private readonly ILessonScheduleRepository _repository;
    private readonly ITimeOffBlockRepository _timeOff;
    private readonly ITeacherScheduleLock _scheduleLock;
    private readonly ILocalCalendar _calendar;
    private readonly SchedulingConflictSettings _settings;
    private readonly IClock _clock;
    private readonly IIdGenerator _idGenerator;

    public RescheduleLessonScheduleCommandHandler(
        ILessonScheduleRepository repository,
        ITimeOffBlockRepository timeOff,
        ITeacherScheduleLock scheduleLock,
        ILocalCalendar calendar,
        SchedulingConflictSettings settings,
        IClock clock,
        IIdGenerator idGenerator)
    {
        _repository = repository;
        _timeOff = timeOff;
        _scheduleLock = scheduleLock;
        _calendar = calendar;
        _settings = settings;
        _clock = clock;
        _idGenerator = idGenerator;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(RescheduleLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        if (command.NewEndAtUtc <= command.NewStartAtUtc)
        {
            return Result<LessonScheduleResponse>.Failure(InvalidRange);
        }

        var lesson = await _repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result<LessonScheduleResponse>.Failure(NotFound);
        }

        if (lesson.Status == LessonScheduleStatus.Completed)
            return Result<LessonScheduleResponse>.Failure(OccurrenceCompletionRules.Completed);

        if (!lesson.IsEditable)
        {
            return Result<LessonScheduleResponse>.Failure(NotEditable);
        }

        // P17 T1 / N-01: tekrarlı derste kapsam zorunlu, anahtar serinin orijinal oluşumu (OccurrenceScopeRules).
        var (scopeError, scope) = OccurrenceScopeRules.Resolve(lesson, command.Scope, command.OccurrenceStartAtUtc, _calendar, InvalidRequest);
        if (scopeError is not null)
        {
            return Result<LessonScheduleResponse>.Failure(scopeError);
        }

        var teacherUserId = lesson.TeacherUserId ?? Guid.Empty;
        var recurring = !string.IsNullOrWhiteSpace(lesson.RecurrenceRule);
        var singleOccurrence = !string.IsNullOrWhiteSpace(lesson.RecurrenceRule)
            && scope == OccurrenceScope.Single
            && command.OccurrenceStartAtUtc is not null;
        var splitOccurrence = recurring
            && scope == OccurrenceScope.ThisAndFuture
            && command.OccurrenceStartAtUtc is not null;
        var occurrenceException = singleOccurrence
            ? new LessonOccurrenceException(
                _idGenerator.New(), lesson.Id, command.OccurrenceStartAtUtc!.Value,
                OccurrenceExceptionAction.Rescheduled, command.NewStartAtUtc, command.NewEndAtUtc, command.Note?.Trim(), _clock.UtcNow)
            : null;
        var splitSeriesId = splitOccurrence ? _idGenerator.New() : Guid.Empty;
        string? splitRule = null;
        IReadOnlyCollection<LessonOccurrenceException>? exceptionsToMove = null;
        IReadOnlyCollection<OccurrenceOverride>? shiftedExceptions = null;
        LessonSchedule? splitSeries = null;
        var applied = false;

        return await _scheduleLock.RunExclusiveAsync(teacherUserId, async token =>
        {
            var completionError = await OccurrenceCompletionRules.CheckAsync(_repository, lesson, scope, command.OccurrenceStartAtUtc, token);
            if (completionError is not null) return Result<LessonScheduleResponse>.Failure(completionError);
            // P16 T4 / N-07: tek oluşum taşınırken yalnız o oluşum hariç tutulur — aynı serinin kardeş oluşumunun üstüne
            // taşımak da çakışmadır. Seri bütün taşınırken aday yeni çapadan genişletilir, dersin kendisi hariç.
            IReadOnlyList<ScheduleOccurrence> candidates;
            Func<LessonSchedule, ScheduleOccurrence, bool> isSelf;
            if (occurrenceException is not null)
            {
                candidates = new[] { new ScheduleOccurrence(command.NewStartAtUtc, command.NewEndAtUtc) };
                isSelf = (other, occurrence) => other.Id == lesson.Id && occurrence.RecurrenceIdUtc == occurrenceException.OriginalStartAtUtc;
            }
            else if (splitOccurrence)
            {
                var cutoff = command.OccurrenceStartAtUtc!.Value;
                var shift = command.NewStartAtUtc - cutoff;
                if (exceptionsToMove is null)
                {
                    var allExceptions = await _repository.ListExceptionsForSeriesAsync(lesson.Id, token);
                    exceptionsToMove = allExceptions
                        .Where(item => item.OriginalStartAtUtc >= cutoff
                            && RecurrenceExpander.IsOccurrence(
                                lesson.StartAtUtc,
                                lesson.EndAtUtc,
                                lesson.RecurrenceRule,
                                _calendar.SeriesZone(lesson.TimeZone),
                                item.OriginalStartAtUtc))
                        .ToArray();
                    shiftedExceptions = exceptionsToMove
                        .Select(item => new OccurrenceOverride(
                            item.OriginalStartAtUtc.Add(shift),
                            item.Action,
                            item.OverrideStartAtUtc?.Add(shift),
                            item.OverrideEndAtUtc?.Add(shift)))
                        .ToArray();
                    splitRule = SeriesSplitRules.RuleForNewSeries(lesson, cutoff, command.NewStartAtUtc, _calendar);
                }

                candidates = TeacherScheduleConflicts.CandidateOccurrences(
                    command.NewStartAtUtc,
                    command.NewEndAtUtc,
                    splitRule,
                    _calendar.SeriesZone(lesson.TimeZone),
                    shiftedExceptions!,
                    _clock.UtcNow,
                    _settings.Horizon,
                    includePastOccurrences: false);
                // Eski serinin yalnız cutoff ve sonraki oluşumları yeni seriyle yer değiştirir. Daha önceki bir oluşuma
                // doğru taşıma yapılırsa geçmiş bölüm gerçek bir çakışma olarak kalır.
                isSelf = (other, occurrence) => other.Id == lesson.Id && occurrence.RecurrenceIdUtc >= cutoff;
            }
            else
            {
                var ownExceptions = await TeacherScheduleConflicts.OwnExceptionsAsync(_repository, lesson, lesson.RecurrenceRule, token);
                candidates = TeacherScheduleConflicts.CandidateOccurrences(
                    command.NewStartAtUtc, command.NewEndAtUtc, lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone),
                    ownExceptions, _clock.UtcNow, _settings.Horizon, includePastOccurrences: false);
                isSelf = (other, _) => other.Id == lesson.Id;
            }

            var conflict = await TeacherScheduleConflicts.FindAsync(
                _repository, _timeOff, _calendar, teacherUserId, candidates, isSelf, command.AllowTimeOffOverride, token);
            if (conflict is not null)
            {
                return Result<LessonScheduleResponse>.Failure(conflict);
            }

            if (occurrenceException is not null)
            {
                // Upsert yeniden denemede aynı satırı (ya da izlenen eklemeyi) günceller; tekrar satırı doğmaz.
                await _repository.UpsertExceptionAsync(occurrenceException, token);
                if (!applied)
                {
                    lesson.RecordOccurrenceRescheduled(occurrenceException.OriginalStartAtUtc, _clock.UtcNow);
                    applied = true;
                }
            }
            else if (splitOccurrence && !applied)
            {
                var cutoff = command.OccurrenceStartAtUtc!.Value;
                var shift = command.NewStartAtUtc - cutoff;
                splitSeries = lesson.SplitSeriesAt(
                    splitSeriesId,
                    cutoff,
                    command.NewStartAtUtc,
                    command.NewEndAtUtc,
                    splitRule,
                    command.Note?.Trim(),
                    _clock.UtcNow);
                foreach (var item in exceptionsToMove!)
                {
                    item.MoveToSeries(splitSeries.Id, shift);
                }

                await _repository.AddAsync(splitSeries, token);
                applied = true;
            }
            else if (!applied)
            {
                lesson.Reschedule(command.NewStartAtUtc, command.NewEndAtUtc, command.Note?.Trim(), _clock.UtcNow);
                applied = true;
            }

            await _repository.SaveChangesAsync(token);
            return Result<LessonScheduleResponse>.Success((splitSeries ?? lesson).ToResponse());
        }, cancellationToken);
    }
}

public sealed class DeleteLessonScheduleCommandHandler(ILessonScheduleRepository repository, IClock clock, ITeacherScheduleLock scheduleLock)
    : ICommandHandler<DeleteLessonScheduleCommand, Result>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders planı bulunamadı.");
    public async Task<Result> Handle(DeleteLessonScheduleCommand command, CancellationToken cancellationToken)
    {
        var initial = await repository.GetByIdAsync(command.LessonId, cancellationToken);
        if (initial is null) return Result.Failure(NotFound);
        return await scheduleLock.RunExclusiveAsync(initial.TeacherUserId ?? Guid.Empty, async token =>
        {
            var lesson = await repository.GetByIdAsync(command.LessonId, token);
            if (lesson is null) return Result.Failure(NotFound);
            if (!lesson.CanBeDeletedAt(clock.UtcNow)) return Result.Failure(new("scheduling.delete_not_allowed", "Ders yalnızca oluşturulduktan sonraki 24 saat içinde ve gelecekteyse silinebilir."));
            var completionError = await OccurrenceCompletionRules.CheckAsync(repository, lesson, OccurrenceScope.All, null, token);
            if (completionError is not null) return Result.Failure(completionError);
            lesson.RecordDeleted(clock.UtcNow);
            repository.Remove(lesson);
            await repository.SaveChangesAsync(token);
            return Result.Success();
        }, cancellationToken);
    }
}

public sealed class GetLessonScheduleByIdQueryHandler : IQueryHandler<GetLessonScheduleByIdQuery, Result<LessonScheduleResponse>>
{
    private static readonly Error NotFound = new("scheduling.lesson_not_found", "Ders plani bulunamadi.");
    private readonly ILessonScheduleRepository _repository;

    public GetLessonScheduleByIdQueryHandler(ILessonScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<LessonScheduleResponse>> Handle(GetLessonScheduleByIdQuery query, CancellationToken cancellationToken)
    {
        var lesson = await _repository.GetByIdAsync(query.LessonId, cancellationToken);
        return lesson is null
            ? Result<LessonScheduleResponse>.Failure(NotFound)
            : Result<LessonScheduleResponse>.Success(lesson.ToResponse());
    }
}

public sealed class ListLessonSchedulesForTeacherQueryHandler : IQueryHandler<ListLessonSchedulesForTeacherQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>
{
    private readonly ILessonScheduleRepository _repository;
    private readonly ILocalCalendar _calendar;

    public ListLessonSchedulesForTeacherQueryHandler(ILessonScheduleRepository repository, ILocalCalendar calendar)
    {
        _repository = repository;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyCollection<LessonScheduleResponse>>> Handle(ListLessonSchedulesForTeacherQuery query, CancellationToken cancellationToken)
    {
        // M04-1: Takvim artık serileri değil somut oluşumları döner. Seri tek satır olarak gelir,
        // occurrence istisnaları (iptal/atlama/erteleme) burada uygulanır.
        var lessons = await _repository.ListActiveForTeacherUntilAsync(query.TeacherUserId, query.EndAtUtc, cancellationToken);
        var exceptions = await _repository.ListExceptionsForTeacherAsync(query.TeacherUserId, cancellationToken);
        var exceptionsByLesson = exceptions
            .GroupBy(x => x.SeriesLessonScheduleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<OccurrenceOverride>)group
                    .Select(x => new OccurrenceOverride(x.OriginalStartAtUtc, x.Action, x.OverrideStartAtUtc, x.OverrideEndAtUtc))
                    .ToArray());

        var payload = new List<LessonScheduleResponse>();
        foreach (var lesson in lessons)
        {
            var lessonExceptions = exceptionsByLesson.TryGetValue(lesson.Id, out var found)
                ? found
                : Array.Empty<OccurrenceOverride>();

            // P16 T2 / N-04: seri kendi diliminin yerel takviminde genişletilir (BYDAY yerel gün).
            foreach (var occurrence in RecurrenceExpander.Expand(
                lesson.StartAtUtc, lesson.EndAtUtc, lesson.RecurrenceRule, _calendar.SeriesZone(lesson.TimeZone),
                query.StartAtUtc, query.EndAtUtc, lessonExceptions))
            {
                // İptal edilen tek oluşum takvimde gösterilmez (öğrenci takvimiyle aynı kural).
                if (occurrence.IsCancelled)
                {
                    continue;
                }

                payload.Add(lesson.ToResponse(occurrence));
            }
        }

        return Result<IReadOnlyCollection<LessonScheduleResponse>>.Success(
            payload.OrderBy(item => item.StartAtUtc).ToArray());
    }
}

public sealed class ListLessonSchedulesForStudentQueryHandler : IQueryHandler<ListLessonSchedulesForStudentQuery, Result<IReadOnlyCollection<LessonScheduleResponse>>>
{
    private readonly ILessonScheduleRepository _repository;

    public ListLessonSchedulesForStudentQueryHandler(ILessonScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonScheduleResponse>>> Handle(ListLessonSchedulesForStudentQuery query, CancellationToken cancellationToken)
    {
        var lessons = await _repository.ListForStudentAsync(query.StudentId, query.StartAtUtc, query.EndAtUtc, cancellationToken);
        var payload = lessons
            .OrderBy(lesson => lesson.StartAtUtc)
            .Select(lesson => lesson.ToResponse())
            .ToArray();

        return Result<IReadOnlyCollection<LessonScheduleResponse>>.Success(payload);
    }
}

internal static class LessonScheduleMappings
{
    /// <summary>
    /// <paramref name="occurrence"/> verildiğinde yanıt somut oluşumun saatlerini taşır;
    /// <c>OccurrenceStartAtUtc</c> ise istisna uçlarının (tek oluşum iptal/erteleme) hedef anahtarıdır.
    /// </summary>
    public static LessonScheduleResponse ToResponse(this LessonSchedule lesson, ScheduleOccurrence? occurrence = null)
    {
        return new LessonScheduleResponse(
            lesson.Id,
            lesson.TeacherUserId,
            lesson.StudentId,
            lesson.Subject,
            // Ç-06: self-lesson'da LessonFormat null; Nullable<T>.ToString() zaten "" döner — davranış aynı, uyarı susturuluyor.
            lesson.LessonFormat?.ToString() ?? string.Empty,
            occurrence?.StartAtUtc ?? lesson.StartAtUtc,
            occurrence?.EndAtUtc ?? lesson.EndAtUtc,
            lesson.TimeZone,
            lesson.RecurrenceRule,
            occurrence?.IsCompleted == true ? "Completed" : lesson.Status.ToString(),
            lesson.ReminderOffsetMinutes,
            lesson.LocationLabel,
            lesson.MeetingUrl,
            lesson.Notes,
            lesson.CreatedOnUtc,
            lesson.UpdatedOnUtc,
            lesson.OriginalStartAtUtc,
            lesson.CancellationReason?.ToString(),
            lesson.IsChargeable,
            occurrence?.RecurrenceIdUtc ?? lesson.StartAtUtc);
    }
}

internal static class OccurrenceCompletionRules
{
    public static readonly Error Completed = new("scheduling.occurrence_completed", "Tamamlanmış ders oluşumu değiştirilemez.");

    public static async Task<Error?> CheckAsync(ILessonScheduleRepository repository, LessonSchedule lesson,
        OccurrenceScope scope, DateTime? key, CancellationToken ct)
    {
        if (lesson.Status == LessonScheduleStatus.Completed) return Completed;
        if (string.IsNullOrWhiteSpace(lesson.RecurrenceRule)) return null;
        var exceptions = await repository.ListExceptionsForSeriesAsync(lesson.Id, ct);
        return exceptions.Any(item => item.Action == OccurrenceExceptionAction.Completed
            && (scope == OccurrenceScope.All
                || (scope == OccurrenceScope.Single && item.OriginalStartAtUtc == key)
                || (scope == OccurrenceScope.ThisAndFuture && item.OriginalStartAtUtc >= key))) ? Completed : null;
    }
}
