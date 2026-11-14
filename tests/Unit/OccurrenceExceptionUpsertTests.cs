using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Infrastructure;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P16 T3 / N-05, N-14: Oluşum istisnalarının okuma tarafı.
/// <para>
/// N-05: Aynı oluşuma ikinci istisna (iki erteleme ya da erteleme + iptal) genişleticideki <c>ToDictionary</c>'yi
/// <c>ArgumentException</c> ile patlatıyor, öğretmen listesi / dashboard / öğrenci takvimi kalıcı 500 dönüyordu.
/// Yazma tarafı artık upsert eder ve <c>(SeriesLessonScheduleId, OriginalStartAtUtc)</c> benzersizdir (gerçek
/// Postgres kanıtı <c>RealOccurrenceExceptionTests</c>'te); genişletici yine de tekrarlara dayanıklıdır — son gelen geçerli.
/// </para>
/// <para>
/// N-14: Pazartesi dersi gelecek haftanın Salı'sına ertelenince iki haftanın hiçbirinde görünmüyordu — genişletici
/// istisnayı yalnız orijinali pencereye düşen oluşuma uyguluyordu. Pencereye <b>taşınan</b> erteleme artık ayrıca
/// eklenir; orijinali serinin gerçek bir oluşumu değilse (bayat istisna) diriltilmez.
/// </para>
/// </summary>
public sealed class OccurrenceExceptionUpsertTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    // Pazartesi 16 Kasım 2026 17:00 TR = 14:00Z (13 Kasım 2026 Cuma).
    private static readonly DateTime Monday = new(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);
    private const string WeeklyMonday = "FREQ=WEEKLY;BYDAY=MO";

    // Yerel haftalar: Pazartesi 00:00 TR (= Pazar 21:00Z) → bir sonraki Pazartesi 00:00 TR'den bir tık önce.
    private static readonly DateTime ThisWeekStart = new(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ThisWeekEnd = ThisWeekStart.AddDays(7).AddTicks(-1);
    private static readonly DateTime NextWeekStart = ThisWeekStart.AddDays(7);
    private static readonly DateTime NextWeekEnd = NextWeekStart.AddDays(7).AddTicks(-1);

    private static readonly LocalCalendar Calendar = new("Europe/Istanbul", new FakeClock(Monday));

    [Fact]
    public void Duplicate_overrides_for_one_occurrence_do_not_throw_and_last_wins()
    {
        var exceptions = new[]
        {
            Moved(Monday, Monday.AddHours(2)),
            Moved(Monday, Monday.AddHours(5)),
        };

        var moved = Assert.Single(Expand(ThisWeekStart, ThisWeekEnd, exceptions));

        Assert.Equal(Monday.AddHours(5), moved.StartAtUtc);
        Assert.Equal(Monday, moved.RecurrenceIdUtc);
    }

    [Fact]
    public void Occurrence_moved_into_next_week_is_listed_there_and_not_this_week()
    {
        // Pazartesi 16 Kasım dersi → gelecek haftanın Salı'sı (24 Kasım 17:00 TR).
        var tuesdayNextWeek = Monday.AddDays(8);
        var exceptions = new[] { Moved(Monday, tuesdayNextWeek) };

        Assert.Empty(Expand(ThisWeekStart, ThisWeekEnd, exceptions));

        var nextWeek = Expand(NextWeekStart, NextWeekEnd, exceptions);
        Assert.Equal(
            new[] { Monday.AddDays(7), tuesdayNextWeek },
            nextWeek.Select(occurrence => occurrence.StartAtUtc).OrderBy(start => start));
        var moved = Assert.Single(nextWeek, occurrence => occurrence.StartAtUtc == tuesdayNextWeek);
        // Tek oluşum uçları istisnayı orijinal anahtarla hedefler; taşınan oluşum da aynı anahtarı taşımalı.
        Assert.Equal(Monday, moved.RecurrenceIdUtc);
        Assert.Equal(tuesdayNextWeek.AddHours(1), moved.EndAtUtc);
        Assert.False(moved.IsCancelled);
    }

    [Fact]
    public void Occurrence_moved_into_an_earlier_week_is_listed_there()
    {
        // Gelecek Pazartesi dersi bu haftanın Cuma'sına öne alındı.
        var friday = Monday.AddDays(4);
        var exceptions = new[] { Moved(Monday.AddDays(7), friday) };

        Assert.Equal(
            new[] { Monday, friday },
            Expand(ThisWeekStart, ThisWeekEnd, exceptions).Select(occurrence => occurrence.StartAtUtc).OrderBy(start => start));
        Assert.Empty(Expand(NextWeekStart, NextWeekEnd, exceptions));
    }

    [Fact]
    public void Occurrence_moved_within_the_window_is_emitted_once()
    {
        var wednesday = Monday.AddDays(2);

        var moved = Assert.Single(Expand(ThisWeekStart, ThisWeekEnd, new[] { Moved(Monday, wednesday) }));

        Assert.Equal(wednesday, moved.StartAtUtc);
        Assert.Equal(Monday, moved.RecurrenceIdUtc);
    }

    [Fact]
    public void Moved_override_at_window_end_is_included_like_series_occurrences()
    {
        // Genişletici pencerenin iki ucunu da dahil sayar; taşınan erteleme aynı yüklemi kullanır.
        var exceptions = new[] { Moved(Monday, NextWeekEnd) };

        Assert.Contains(Expand(NextWeekStart, NextWeekEnd, exceptions), occurrence => occurrence.StartAtUtc == NextWeekEnd);
    }

    public static TheoryData<string, DateTime> StaleOriginals => new()
    {
        // Seri 17 Kasım'da bitti (UNTIL); 23 Kasım oluşumu artık yok.
        { "FREQ=WEEKLY;BYDAY=MO;UNTIL=20261117T000000Z", Monday.AddDays(7) },
        // Pazartesi serisinde Çarşamba oluşumu hiç olmadı (kural değişmiş ya da bozuk istemci anahtarı).
        { WeeklyMonday, Monday.AddDays(9) },
        // Çapadan önceki an serinin oluşumu değil.
        { WeeklyMonday, Monday.AddDays(-7) },
    };

    [Theory]
    [MemberData(nameof(StaleOriginals))]
    public void Stale_override_from_outside_the_window_is_not_resurrected(string rule, DateTime staleOriginal)
    {
        var thursday = Monday.AddDays(3);
        var exceptions = new[] { Moved(staleOriginal, thursday) };

        Assert.Equal(
            new[] { Monday },
            Expand(ThisWeekStart, ThisWeekEnd, exceptions, rule).Select(occurrence => occurrence.StartAtUtc));
    }

    [Theory]
    [InlineData(OccurrenceExceptionAction.Cancelled)]
    [InlineData(OccurrenceExceptionAction.Skipped)]
    public void Non_rescheduled_override_from_outside_the_window_does_not_leak(OccurrenceExceptionAction action)
    {
        // Bozuk satır: iptal/atlama kaydında override saati dolu olsa da oluşum pencereye taşınmaz.
        var thursday = Monday.AddDays(3);
        var exceptions = new[] { new OccurrenceOverride(Monday.AddDays(7), action, thursday, thursday.AddHours(1)) };

        Assert.Equal(
            new[] { Monday },
            Expand(ThisWeekStart, ThisWeekEnd, exceptions).Select(occurrence => occurrence.StartAtUtc));
    }

    [Fact]
    public async Task Teacher_list_survives_duplicate_rows_and_applies_the_last_one()
    {
        // Benzersiz indeksten önce yazılmış (ya da göç edilmemiş) aynı anahtarlı iki satır: liste 500 değil başarı döner.
        var lesson = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik",
            ScheduledLessonFormat.Online, Monday, Monday.AddHours(1), "Europe/Istanbul",
            WeeklyMonday, LessonScheduleStatus.Planned, 60, null, null, null, Monday);
        var rescheduled = new LessonOccurrenceException(
            Guid.NewGuid(), lesson.Id, Monday, OccurrenceExceptionAction.Rescheduled,
            Monday.AddHours(2), Monday.AddHours(3), null, Monday);
        var cancelled = new LessonOccurrenceException(
            Guid.NewGuid(), lesson.Id, Monday, OccurrenceExceptionAction.Cancelled,
            null, null, null, Monday.AddMinutes(5));
        var handler = new ListLessonSchedulesForTeacherQueryHandler(new FakeRepo(lesson, rescheduled, cancelled), Calendar);

        var result = await handler.Handle(
            new ListLessonSchedulesForTeacherQuery(lesson.TeacherUserId!.Value, ThisWeekStart, NextWeekEnd), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { Monday.AddDays(7) }, result.Value!.Select(item => item.StartAtUtc));
    }

    private static ScheduleOccurrence[] Expand(
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        IReadOnlyCollection<OccurrenceOverride> exceptions,
        string rule = WeeklyMonday)
        => RecurrenceExpander
            .Expand(Monday, Monday.AddHours(1), rule, Istanbul, rangeStartUtc, rangeEndUtc, exceptions)
            .ToArray();

    private static OccurrenceOverride Moved(DateTime originalStartUtc, DateTime newStartUtc)
        => new(originalStartUtc, OccurrenceExceptionAction.Rescheduled, newStartUtc, newStartUtc.AddHours(1));

    private sealed class FakeRepo(LessonSchedule lesson, params LessonOccurrenceException[] exceptions) : ILessonScheduleRepository
    {
        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSchedule>>(new[] { lesson });

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(exceptions);

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void Remove(LessonSchedule lessonSchedule) => throw new NotImplementedException();
        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
