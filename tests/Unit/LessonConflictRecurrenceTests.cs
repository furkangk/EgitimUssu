using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P16 T4 / N-07 — öğretmen ders çakışması tekrarı, istisnaları ve tatil bloklarını görür; yazan her yol öğretmen
/// kilidinin içinde okur ve kaydeder.
/// <para>
/// Eskiden çakışma yalnız serinin <b>ilk</b> satırıyla (çapa) karşılaştırılıyordu: Eylül'de başlamış haftalık Salı 17:00
/// serisi varken Kasım'ın bir Salı'sına 17:00 dersi açılabiliyordu; tatil bloğu ders açmayı engellemiyordu; tatil
/// oluştururken bloktan önce başlayıp içine taşan ders ve serinin bloğa düşen oluşumu listelenmiyordu.
/// </para>
/// Saatler TR (UTC+3): 14:00Z = 17:00 TR. "Şimdi" 2026-11-14 Cumartesi 12:00 TR.
/// </summary>
public sealed class LessonConflictRecurrenceTests
{
    private static readonly Guid Teacher = Guid.NewGuid();
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    private static readonly FixedClock Clock = new(new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc));
    private static readonly LocalCalendar Calendar = new("Europe/Istanbul", Clock);

    /// <summary>2026-09-01 Salı 17:00 TR — "Eylül'de başlamış" haftalık serinin çapası.</summary>
    private static readonly DateTime SeptemberTuesday = Utc(2026, 9, 1, 14);
    private static readonly DateTime NovemberTuesday = Utc(2026, 11, 17, 14);

    private readonly RecordingScheduleLock _lock = new();
    private readonly InMemoryTimeOffRepository _timeOff = new();
    private readonly ScheduleStore _store;

    public LessonConflictRecurrenceTests()
    {
        _store = new ScheduleStore(_lock);
    }

    // ---- Tekrarlı mevcut ders ----

    [Fact]
    public async Task Single_lesson_on_a_tuesday_of_a_weekly_series_started_in_september_conflicts()
    {
        AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");

        var result = await Create(NovemberTuesday);

        AssertError(result, "scheduling.teacher_conflict");
        Assert.Single(_store.Lessons);
    }

    [Theory]
    [InlineData(-60, false)] // 12:30–13:30Z: seriden hemen önce, arka arkaya
    [InlineData(-30, true)]
    [InlineData(0, true)]
    [InlineData(30, true)]   // 14:00–15:00Z: serinin 13:30–14:30Z oluşumu adaydan ÖNCE başlıyor
    [InlineData(60, false)]  // 14:30–15:30Z: seriden hemen sonra, arka arkaya
    public async Task Partially_overlapping_occurrence_conflicts_and_back_to_back_lessons_do_not(int offsetMinutes, bool conflicts)
    {
        AddLesson(SeptemberTuesday.AddMinutes(-30), "FREQ=WEEKLY;BYDAY=TU"); // 16:30–17:30 TR
        var start = NovemberTuesday.AddMinutes(-30 + offsetMinutes);

        var result = await Create(start);

        if (conflicts)
        {
            AssertError(result, "scheduling.teacher_conflict");
        }
        else
        {
            Assert.True(result.IsSuccess, result.Error.Code);
        }
    }

    [Fact]
    public async Task Cancelled_occurrence_frees_its_slot()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        AddException(series, NovemberTuesday, OccurrenceExceptionAction.Cancelled);

        var result = await Create(NovemberTuesday);

        Assert.True(result.IsSuccess, result.Error.Code);
    }

    [Fact]
    public async Task Rescheduled_occurrence_occupies_its_new_slot_instead_of_the_original()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        var wednesday = NovemberTuesday.AddDays(1);
        AddException(series, NovemberTuesday, OccurrenceExceptionAction.Rescheduled, wednesday, wednesday + OneHour);

        Assert.True((await Create(NovemberTuesday)).IsSuccess);
        AssertError(await Create(wednesday.AddMinutes(30)), "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Other_teachers_series_does_not_conflict()
    {
        AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU", teacher: Guid.NewGuid());

        Assert.True((await Create(NovemberTuesday)).IsSuccess);
    }

    // ---- Tekrarlı aday + ufuk ----

    [Fact]
    public async Task Weekly_candidate_conflicts_with_a_single_lesson_four_weeks_later()
    {
        AddLesson(NovemberTuesday.AddDays(28)); // 15 Aralık Salı

        AssertError(await Create(NovemberTuesday, "FREQ=WEEKLY;BYDAY=TU"), "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Conflict_beyond_the_horizon_is_not_checked_and_the_horizon_is_configurable()
    {
        AddLesson(NovemberTuesday.AddDays(7 * 13)); // 13. hafta: 12 haftalık ufkun dışında

        var created = await Create(NovemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        Assert.True(created.IsSuccess, created.Error.Code);
        _store.Lessons.RemoveAll(lesson => lesson.Id == created.Value!.Id); // ikinci deneme kendi kopyasına çarpmasın

        AssertError(
            await Create(NovemberTuesday, "FREQ=WEEKLY;BYDAY=TU", settings: new SchedulingConflictSettings(20)),
            "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Backdated_weekly_create_is_checked_from_its_own_anchor()
    {
        // Geçmişe kayıt: her oluşum yenidir, çapa dahil denetlenir (eski kod çapayı hep karşılaştırıyordu).
        var lastTuesday = Utc(2026, 11, 10, 14);
        AddLesson(lastTuesday);

        AssertError(await Create(lastTuesday, "FREQ=WEEKLY;BYDAY=TU"), "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Backdated_series_that_already_ended_is_still_checked()
    {
        // COUNT tükenmiş geçmiş seri: "şimdi"den sonra hiç oluşumu yok — aday listesi boş kalıp kontrol atlanmamalı.
        AddLesson(Utc(2026, 10, 13, 14));

        AssertError(await Create(Utc(2026, 10, 6, 14), "FREQ=WEEKLY;BYDAY=TU;COUNT=2"), "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Updating_a_september_series_is_not_blocked_by_a_past_overlap()
    {
        // Bu düzeltmeden önce yazılmış (çakışma görülmeden) geçmiş bir ders, serinin 15 Eylül oluşumunun üstünde.
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        AddLesson(SeptemberTuesday.AddDays(14));

        var result = await Update(series, SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU", notes: "Not düzeltildi");

        Assert.True(result.IsSuccess, result.Error.Code);
    }

    [Fact]
    public async Task Updating_a_series_ignores_itself_and_honours_its_own_cancelled_occurrence()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        AddException(series, NovemberTuesday, OccurrenceExceptionAction.Cancelled);
        AddLesson(NovemberTuesday); // iptalle boşalan saate başka öğrencinin dersi

        var result = await Update(series, SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU", notes: "Not düzeltildi");

        Assert.True(result.IsSuccess, result.Error.Code);
    }

    [Fact]
    public async Task Moving_a_series_onto_another_series_conflicts()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        AddLesson(Utc(2026, 9, 9, 14), "FREQ=WEEKLY;BYDAY=WE"); // Çarşamba 17:00 serisi

        var result = await Update(series, Utc(2026, 9, 2, 14), "FREQ=WEEKLY;BYDAY=WE");

        AssertError(result, "scheduling.teacher_conflict");
        Assert.Equal(SeptemberTuesday, series.StartAtUtc);
    }

    // ---- Tek oluşum erteleme ----

    [Fact]
    public async Task Moving_one_occurrence_onto_another_series_conflicts()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        AddLesson(Utc(2026, 9, 2, 14), "FREQ=WEEKLY;BYDAY=WE");
        var wednesday = NovemberTuesday.AddDays(1);

        AssertError(await RescheduleSingle(series, NovemberTuesday, wednesday), "scheduling.teacher_conflict");
        Assert.Empty(_store.Exceptions);
    }

    [Fact]
    public async Task Moving_one_occurrence_onto_its_own_next_occurrence_conflicts()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");

        AssertError(await RescheduleSingle(series, NovemberTuesday, NovemberTuesday.AddDays(7)), "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Moving_one_occurrence_by_half_an_hour_over_its_own_slot_is_allowed()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");

        var result = await RescheduleSingle(series, NovemberTuesday, NovemberTuesday.AddMinutes(30));

        Assert.True(result.IsSuccess, result.Error.Code);
        var exception = Assert.Single(_store.Exceptions);
        Assert.Equal(NovemberTuesday.AddMinutes(30), exception.OverrideStartAtUtc);
    }

    // ---- Tatil bloğu ----

    [Fact]
    public async Task Lesson_inside_a_time_off_block_needs_an_explicit_override()
    {
        AddTimeOff(Utc(2026, 11, 22, 21), Utc(2026, 11, 27, 21)); // 23–27 Kasım TR, tam gün
        var monday = Utc(2026, 11, 23, 10);

        AssertError(await Create(monday), "scheduling.time_off_conflict");
        Assert.Empty(_store.Lessons);

        Assert.True((await Create(monday, allowTimeOffOverride: true)).IsSuccess);
        Assert.Single(_store.Lessons);
    }

    [Fact]
    public async Task Weekly_candidate_whose_second_occurrence_falls_into_time_off_needs_an_override()
    {
        AddTimeOff(Utc(2026, 11, 22, 21), Utc(2026, 11, 27, 21));

        AssertError(await Create(NovemberTuesday, "FREQ=WEEKLY;BYDAY=TU"), "scheduling.time_off_conflict");
        Assert.True((await Create(NovemberTuesday, "FREQ=WEEKLY;BYDAY=TU", allowTimeOffOverride: true)).IsSuccess);
    }

    [Fact]
    public async Task Lesson_conflict_wins_over_time_off_and_cannot_be_overridden()
    {
        AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        AddTimeOff(NovemberTuesday.AddHours(-2), NovemberTuesday.AddHours(4));

        AssertError(await Create(NovemberTuesday), "scheduling.teacher_conflict");
        AssertError(await Create(NovemberTuesday, allowTimeOffOverride: true), "scheduling.teacher_conflict");
    }

    [Fact]
    public async Task Rescheduling_into_time_off_needs_an_override()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        var thursday = NovemberTuesday.AddDays(2);
        AddTimeOff(thursday.AddHours(-14), thursday.AddHours(10));

        AssertError(await RescheduleSingle(series, NovemberTuesday, thursday), "scheduling.time_off_conflict");
        Assert.True((await RescheduleSingle(series, NovemberTuesday, thursday, allowTimeOffOverride: true)).IsSuccess);
    }

    // ---- Tatil oluştururken çakışan dersler ----

    [Fact]
    public async Task Creating_time_off_lists_expanded_occurrences_and_lessons_spilling_into_the_block()
    {
        var blockStart = Utc(2026, 11, 23, 9);
        var blockEnd = Utc(2026, 11, 27, 21);
        var spilling = AddLesson(blockStart.AddHours(-1), duration: TimeSpan.FromHours(2)); // 08:00–10:00Z
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        var wednesdays = AddLesson(Utc(2026, 9, 2, 14), "FREQ=WEEKLY;BYDAY=WE");
        AddException(wednesdays, Utc(2026, 11, 25, 14), OccurrenceExceptionAction.Cancelled);
        AddLesson(blockStart.AddHours(-2)); // 07:00–08:00Z: blok başlamadan biter
        var handler = new CreateTimeOffBlockCommandHandler(_timeOff, _store, _lock, Calendar, new SequentialIds(), Clock);

        var result = await handler.Handle(
            new CreateTimeOffBlockCommand(Teacher, TimeOffType.Holiday, "Tatil", blockStart, blockEnd, false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        var listed = result.Value!.ConflictingLessons.ToArray();
        Assert.Equal(new[] { spilling.Id, series.Id }, listed.Select(item => item.Id));
        Assert.Equal(Utc(2026, 11, 24, 14), listed[1].StartAtUtc);
        Assert.Equal(Utc(2026, 11, 24, 14), listed[1].OccurrenceStartAtUtc);
        Assert.Equal(new[] { Teacher }, _lock.Teachers);
    }

    // ---- Kilit: okuma + yazma + kaydetme aynı kritik bölgede ----

    [Fact]
    public async Task Every_writing_path_reads_and_saves_inside_the_teachers_lock()
    {
        var series = AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");
        var single = AddLesson(Utc(2026, 11, 20, 8));

        Assert.True((await Create(Utc(2026, 11, 19, 8))).IsSuccess);
        Assert.True((await Update(single, Utc(2026, 11, 20, 10), null)).IsSuccess);
        Assert.True((await RescheduleSingle(series, NovemberTuesday, NovemberTuesday.AddHours(3))).IsSuccess);
        var cancel = new CancelLessonScheduleCommandHandler(_store, _lock, Calendar, Clock, new SequentialIds());
        Assert.True((await cancel.Handle(new CancelLessonScheduleCommand(
            series.Id, CancellationReason.Other, false, null, OccurrenceScope.Single, NovemberTuesday.AddDays(7)), CancellationToken.None)).IsSuccess);

        Assert.Equal(new[] { Teacher, Teacher, Teacher, Teacher }, _lock.Teachers);
        Assert.Equal(0, _store.ReadsOutsideLock);
        Assert.Equal(0, _store.SavesOutsideLock);
        Assert.Equal(4, _store.SaveCount);
    }

    [Fact]
    public async Task Rejected_write_is_not_saved()
    {
        AddLesson(SeptemberTuesday, "FREQ=WEEKLY;BYDAY=TU");

        AssertError(await Create(NovemberTuesday), "scheduling.teacher_conflict");

        Assert.Equal(0, _store.SaveCount);
    }

    // ---- Yapılandırma ----

    [Theory]
    [InlineData(null, 12)]
    [InlineData("", 12)]
    [InlineData("20", 20)]
    [InlineData(" 1 ", 1)]
    [InlineData("104", 104)]
    public void Horizon_weeks_are_read_from_configuration(string? configured, int expectedWeeks)
        => Assert.Equal(expectedWeeks, SchedulingConflictSettings.Parse(configured).HorizonWeeks);

    [Theory]
    [InlineData("0")]
    [InlineData("105")]
    [InlineData("-3")]
    [InlineData("on iki")]
    public void Invalid_horizon_stops_startup(string configured)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SchedulingConflictSettings.Parse(configured));
        Assert.Contains("Scheduling:ConflictHorizonWeeks", error.Message);
    }

    // ---- Yardımcılar ----

    private Task<Result<LessonScheduleResponse>> Create(
        DateTime startUtc, string? rule = null, bool allowTimeOffOverride = false, SchedulingConflictSettings? settings = null)
    {
        var handler = new CreateLessonScheduleCommandHandler(
            _store, _timeOff, _lock, Calendar, settings ?? SchedulingConflictSettings.Default, new SequentialIds(), Clock);
        return handler.Handle(
            new CreateLessonScheduleCommand(
                Teacher, Guid.NewGuid(), "Matematik", ScheduledLessonFormat.Online, startUtc, startUtc + OneHour,
                "Europe/Istanbul", rule, 60, null, null, null, allowTimeOffOverride),
            CancellationToken.None);
    }

    private Task<Result<LessonScheduleResponse>> Update(LessonSchedule lesson, DateTime startUtc, string? rule, string? notes = null)
    {
        var handler = new UpdateLessonScheduleCommandHandler(_store, _timeOff, _lock, Calendar, SchedulingConflictSettings.Default, Clock);
        return handler.Handle(
            new UpdateLessonScheduleCommand(
                lesson.Id, lesson.Subject, ScheduledLessonFormat.Online, startUtc, startUtc + (lesson.EndAtUtc - lesson.StartAtUtc),
                lesson.TimeZone, rule, 60, null, null, notes),
            CancellationToken.None);
    }

    private Task<Result<LessonScheduleResponse>> RescheduleSingle(
        LessonSchedule series, DateTime occurrenceStartUtc, DateTime newStartUtc, bool allowTimeOffOverride = false)
    {
        var handler = new RescheduleLessonScheduleCommandHandler(
            _store, _timeOff, _lock, Calendar, SchedulingConflictSettings.Default, Clock, new SequentialIds());
        return handler.Handle(
            new RescheduleLessonScheduleCommand(
                series.Id, newStartUtc, newStartUtc + OneHour, null, OccurrenceScope.Single, occurrenceStartUtc, allowTimeOffOverride),
            CancellationToken.None);
    }

    private LessonSchedule AddLesson(DateTime startUtc, string? rule = null, TimeSpan? duration = null, Guid? teacher = null)
    {
        var lesson = new LessonSchedule(
            Guid.NewGuid(), teacher ?? Teacher, Guid.NewGuid(), "Matematik", ScheduledLessonFormat.Online,
            startUtc, startUtc + (duration ?? OneHour), "Europe/Istanbul", rule, LessonScheduleStatus.Planned,
            60, null, null, null, startUtc.AddDays(-1));
        _store.Lessons.Add(lesson);
        return lesson;
    }

    private void AddException(
        LessonSchedule series, DateTime originalStartUtc, OccurrenceExceptionAction action,
        DateTime? overrideStartUtc = null, DateTime? overrideEndUtc = null)
        => _store.Exceptions.Add(new LessonOccurrenceException(
            Guid.NewGuid(), series.Id, originalStartUtc, action, overrideStartUtc, overrideEndUtc, null, Clock.UtcNow));

    private void AddTimeOff(DateTime startUtc, DateTime endUtc)
        => _timeOff.Blocks.Add(new TimeOffBlock(Guid.NewGuid(), Teacher, TimeOffType.Holiday, "Tatil", startUtc, endUtc, false, Clock.UtcNow));

    private static void AssertError<T>(Result<T> result, string code)
    {
        Assert.True(result.IsFailure, "Başarısızlık bekleniyordu, başarılı döndü.");
        Assert.Equal(code, result.Error.Code);
    }

    private static DateTime Utc(int year, int month, int day, int hour)
        => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private sealed class SequentialIds : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    /// <summary>Kilidin tutulduğu anı bildirir: depo okuma/kaydetmeyi kilidin dışında yakalar.</summary>
    private sealed class RecordingScheduleLock : ITeacherScheduleLock
    {
        public bool Held { get; private set; }

        public List<Guid> Teachers { get; } = new();

        public async Task<TResult> RunExclusiveAsync<TResult>(Guid teacherUserId, Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
            where TResult : Result
        {
            Teachers.Add(teacherUserId);
            Held = true;
            try
            {
                return await work(cancellationToken);
            }
            finally
            {
                Held = false;
            }
        }
    }

    /// <summary>
    /// Bellek içi ders deposu; sorgu yüklemleri <c>LessonScheduleRepository</c> ile aynıdır. <c>GetByIdAsync</c> kilitten
    /// önce çağrılabilir (öğretmen kimliğini öğrenmek için) — diğer okumalar ve kaydetme kilit içinde olmalı.
    /// </summary>
    private sealed class ScheduleStore(RecordingScheduleLock scheduleLock) : ILessonScheduleRepository
    {
        public List<LessonSchedule> Lessons { get; } = new();

        public List<LessonOccurrenceException> Exceptions { get; } = new();

        public int ReadsOutsideLock { get; private set; }

        public int SavesOutsideLock { get; private set; }

        public int SaveCount { get; private set; }

        private void Read()
        {
            if (!scheduleLock.Held)
            {
                ReadsOutsideLock++;
            }
        }

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
            => Task.FromResult(Lessons.FirstOrDefault(lesson => lesson.Id == lessonId));

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
        {
            Read();
            return Task.FromResult<IReadOnlyCollection<LessonSchedule>>(Lessons
                .Where(lesson => lesson.TeacherUserId == teacherUserId
                    && lesson.Status != LessonScheduleStatus.Cancelled
                    && (lesson.StartAtUtc <= untilUtc
                        || Exceptions.Any(x => x.SeriesLessonScheduleId == lesson.Id
                            && x.Action == OccurrenceExceptionAction.Rescheduled
                            && x.OverrideStartAtUtc <= untilUtc)))
                .ToArray());
        }

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
        {
            Read();
            return Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Exceptions
                .Where(x => Lessons.Any(lesson => lesson.Id == x.SeriesLessonScheduleId && lesson.TeacherUserId == teacherUserId))
                .ToArray());
        }

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
        {
            Read();
            return Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Exceptions
                .Where(x => x.SeriesLessonScheduleId == seriesLessonScheduleId)
                .ToArray());
        }

        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
        {
            Read();
            var existing = Exceptions.FirstOrDefault(x => x.SeriesLessonScheduleId == occurrenceException.SeriesLessonScheduleId
                && x.OriginalStartAtUtc == occurrenceException.OriginalStartAtUtc);
            if (existing is null)
            {
                Exceptions.Add(occurrenceException);
            }
            else
            {
                existing.Overwrite(occurrenceException.Action, occurrenceException.OverrideStartAtUtc,
                    occurrenceException.OverrideEndAtUtc, occurrenceException.Note);
            }

            return Task.CompletedTask;
        }

        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken)
        {
            Lessons.Add(lessonSchedule);
            return Task.CompletedTask;
        }

        public void Remove(LessonSchedule lessonSchedule) => Lessons.Remove(lessonSchedule);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (!scheduleLock.Held)
            {
                SavesOutsideLock++;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}

/// <summary>Kilit yokmuş gibi işi doğrudan çalıştırır (handler kurucusu kilit isteyen diğer testler için).</summary>
internal sealed class PassThroughScheduleLock : ITeacherScheduleLock
{
    public Task<TResult> RunExclusiveAsync<TResult>(Guid teacherUserId, Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
        where TResult : Result
        => work(cancellationToken);
}

/// <summary>Tatil bloklarını bellekte tutar; sorgu gerçek depo gibi aralık kesişimine bakar.</summary>
internal sealed class InMemoryTimeOffRepository : ITimeOffBlockRepository
{
    public List<TimeOffBlock> Blocks { get; } = new();

    public Task AddAsync(TimeOffBlock block, CancellationToken cancellationToken)
    {
        Blocks.Add(block);
        return Task.CompletedTask;
    }

    public Task<TimeOffBlock?> GetByIdAsync(Guid timeOffId, CancellationToken cancellationToken)
        => Task.FromResult(Blocks.FirstOrDefault(block => block.Id == timeOffId));

    public Task<IReadOnlyCollection<TimeOffBlock>> ListForTeacherAsync(Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<TimeOffBlock>>(Blocks
            .Where(block => block.TeacherUserId == teacherUserId && block.StartAtUtc < endAtUtc && block.EndAtUtc > startAtUtc)
            .ToArray());

    public void Remove(TimeOffBlock block) => Blocks.Remove(block);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
