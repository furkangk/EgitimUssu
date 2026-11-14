using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P17 T1 / N-01 (K-20) — tekrarlı derste iptal/erteleme kapsamı zorunlu. Eskiden kapsamsız istek varsayılan <c>All</c>
/// sayılıyordu: haftalık dersin "bu haftası" iptal edilince bütün seri (geçmiş haftalar dahil) iptal oluyor, öğrencinin
/// erteleme talebi kabul edilince bütün seri kayıyordu. Anahtar (<c>occurrenceStartAtUtc</c>) serinin <b>orijinal</b>
/// oluşumu olmalı (master §4) — görünen (ertelenmiş) saat ya da seride olmayan bir an reddedilir (Öğrenilenler #223 g).
/// Haftalık Salı 17:00 TR (14:00Z) serisi, çapa 1 Eylül 2026; "şimdi" 14 Kasım 2026.
/// </summary>
public sealed class OccurrenceScopeRequiredTests
{
    private static readonly DateTime Anchor = new(2026, 9, 1, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime November17 = new(2026, 11, 17, 14, 0, 0, DateTimeKind.Utc);
    private static readonly FixedClock Clock = new(new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc));
    private static readonly LocalCalendar Calendar = new("Europe/Istanbul", Clock);

    private readonly Store _store = new();

    // ---- İptal ----

    [Fact]
    public async Task Cancelling_a_recurring_lesson_without_scope_is_rejected_and_the_series_is_untouched()
    {
        var series = AddSeries();

        var result = await Cancel(series, scope: null, occurrence: null);

        AssertError(result, "scheduling.scope_required");
        Assert.Equal(LessonScheduleStatus.Planned, series.Status);
        Assert.Empty(_store.Exceptions);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task Cancelling_one_occurrence_leaves_the_series_and_other_weeks_alone()
    {
        var series = AddSeries();

        var result = await Cancel(series, OccurrenceScope.Single, November17);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(LessonScheduleStatus.Planned, series.Status);
        var exception = Assert.Single(_store.Exceptions);
        Assert.Equal((November17, OccurrenceExceptionAction.Cancelled), (exception.OriginalStartAtUtc, exception.Action));
    }

    [Fact]
    public async Task Cancelling_the_whole_series_needs_no_occurrence_key()
    {
        var series = AddSeries();

        var result = await Cancel(series, OccurrenceScope.All, occurrence: null);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(LessonScheduleStatus.Cancelled, series.Status);
    }

    [Fact]
    public async Task Single_lesson_ignores_scope()
    {
        var lesson = AddSeries(rule: null);

        var result = await Cancel(lesson, scope: null, occurrence: null);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(LessonScheduleStatus.Cancelled, lesson.Status);
    }

    [Theory]
    [InlineData(OccurrenceScope.Single)]
    [InlineData(OccurrenceScope.ThisAndFuture)]
    public async Task Occurrence_key_must_be_an_original_occurrence_of_the_series(OccurrenceScope scope)
    {
        var series = AddSeries();

        // Çarşamba: seride böyle bir oluşum yok.
        AssertError(await Cancel(series, scope, November17.AddDays(1)), "scheduling.occurrence_not_found");
        // Aynı Salı, yanlış saat.
        AssertError(await Cancel(series, scope, November17.AddMinutes(30)), "scheduling.occurrence_not_found");
        // Seri başlamadan önceki Salı.
        AssertError(await Cancel(series, scope, Anchor.AddDays(-7)), "scheduling.occurrence_not_found");

        Assert.Equal(LessonScheduleStatus.Planned, series.Status);
        Assert.DoesNotContain("UNTIL", series.RecurrenceRule);
        Assert.Empty(_store.Exceptions);
    }

    [Fact]
    public async Task Displayed_time_of_a_rescheduled_occurrence_is_not_a_key()
    {
        // Oluşum 17 Kasım Salı'dan 18 Kasım Çarşamba'ya ertelenmiş; istemci görünen (Çarşamba) saati gönderirse yeni
        // anahtarla görünmez bir satır yazılırdı (#223 g). Orijinal anahtar çalışır.
        var series = AddSeries();
        var wednesday = November17.AddDays(1);
        _store.Exceptions.Add(new LessonOccurrenceException(
            Guid.NewGuid(), series.Id, November17, OccurrenceExceptionAction.Rescheduled, wednesday, wednesday.AddHours(1), null, Clock.UtcNow));

        AssertError(await Cancel(series, OccurrenceScope.Single, wednesday), "scheduling.occurrence_not_found");

        Assert.True((await Cancel(series, OccurrenceScope.Single, November17)).IsSuccess);
        var row = Assert.Single(_store.Exceptions);
        Assert.Equal((November17, OccurrenceExceptionAction.Cancelled), (row.OriginalStartAtUtc, row.Action));
    }

    // ---- Erteleme ----

    [Fact]
    public async Task Rescheduling_a_recurring_lesson_without_scope_is_rejected()
    {
        var series = AddSeries();

        var result = await Reschedule(series, scope: null, occurrence: null, newStart: November17.AddHours(2));

        AssertError(result, "scheduling.scope_required");
        Assert.Equal(Anchor, series.StartAtUtc);
    }

    [Fact]
    public async Task Rescheduling_needs_an_original_occurrence_key()
    {
        var series = AddSeries();

        AssertError(
            await Reschedule(series, OccurrenceScope.Single, November17.AddDays(2), November17.AddHours(2)),
            "scheduling.occurrence_not_found");
        Assert.True((await Reschedule(series, OccurrenceScope.Single, November17, November17.AddHours(2))).IsSuccess);
        Assert.Equal(Anchor, series.StartAtUtc);
    }

    // ---- Öğrencinin erteleme talebi ----

    [Fact]
    public async Task Change_request_on_a_recurring_lesson_must_name_a_real_occurrence()
    {
        var series = AddSeries();
        var requests = new RequestStore();

        AssertError(await CreateRequest(requests, series, occurrence: null), "scheduling.invalid_request");
        AssertError(await CreateRequest(requests, series, November17.AddDays(1)), "scheduling.occurrence_not_found");

        var created = await CreateRequest(requests, series, November17);
        Assert.True(created.IsSuccess, created.Error.Code);
        Assert.Equal(November17, created.Value!.OccurrenceStartAtUtc);
    }

    [Fact]
    public async Task Change_request_on_a_single_lesson_records_the_lesson_start()
    {
        var lesson = AddSeries(rule: null);
        var requests = new RequestStore();

        var created = await CreateRequest(requests, lesson, occurrence: null);

        Assert.True(created.IsSuccess, created.Error.Code);
        Assert.Equal(Anchor, created.Value!.OccurrenceStartAtUtc);
    }

    [Fact]
    public async Task Accepting_a_request_moves_only_its_occurrence()
    {
        var series = AddSeries();
        var request = new LessonChangeRequest(
            Guid.NewGuid(), series.Id, series.StudentId, series.TeacherUserId!.Value, "Hastayım",
            November17.AddDays(2), November17.AddDays(2).AddHours(1), November17, Clock.UtcNow);
        var dispatcher = new RecordingDispatcher();
        var handler = new AcceptLessonChangeRequestCommandHandler(new RequestStore(request), _store, dispatcher, Clock);

        var result = await handler.Handle(new AcceptLessonChangeRequestCommand(request.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        var reschedule = Assert.IsType<RescheduleLessonScheduleCommand>(Assert.Single(dispatcher.Commands));
        Assert.Equal((OccurrenceScope?)OccurrenceScope.Single, reschedule.Scope);
        Assert.Equal(November17, reschedule.OccurrenceStartAtUtc);
        Assert.Equal(November17.AddDays(2), reschedule.NewStartAtUtc);
    }

    [Fact]
    public async Task Accepting_a_legacy_request_without_occurrence_does_not_move_the_series()
    {
        // Bu alandan önce açılmış tekrarlı ders talebi: hangi haftayı kastettiği bilinmiyor — seri kaydırılmaz.
        var series = AddSeries();
        var request = new LessonChangeRequest(
            Guid.NewGuid(), series.Id, series.StudentId, series.TeacherUserId!.Value, "Eski talep",
            November17.AddDays(2), November17.AddDays(2).AddHours(1), null, Clock.UtcNow);
        var dispatcher = new RecordingDispatcher();
        var handler = new AcceptLessonChangeRequestCommandHandler(new RequestStore(request), _store, dispatcher, Clock);

        var result = await handler.Handle(new AcceptLessonChangeRequestCommand(request.Id), CancellationToken.None);

        AssertError(result, "scheduling.request_occurrence_unknown");
        Assert.Empty(dispatcher.Commands);
        Assert.Equal(LessonChangeRequestStatus.Pending, request.Status);
    }

    // ---- Yardımcılar ----

    private LessonSchedule AddSeries(string? rule = "FREQ=WEEKLY;BYDAY=TU")
    {
        var lesson = new LessonSchedule(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Matematik", ScheduledLessonFormat.Online,
            Anchor, Anchor.AddHours(1), "Europe/Istanbul", rule, LessonScheduleStatus.Planned, 60, null, null, null, Anchor.AddDays(-1));
        lesson.ClearDomainEvents();
        _store.Lessons.Add(lesson);
        return lesson;
    }

    private Task<Result<LessonScheduleResponse>> Cancel(LessonSchedule lesson, OccurrenceScope? scope, DateTime? occurrence)
        => new CancelLessonScheduleCommandHandler(_store, new PassThroughScheduleLock(), Calendar, Clock, new Ids())
            .Handle(new CancelLessonScheduleCommand(lesson.Id, CancellationReason.Other, false, null, scope, occurrence), CancellationToken.None);

    private Task<Result<LessonScheduleResponse>> Reschedule(LessonSchedule lesson, OccurrenceScope? scope, DateTime? occurrence, DateTime newStart)
        => new RescheduleLessonScheduleCommandHandler(
                _store, new InMemoryTimeOffRepository(), new PassThroughScheduleLock(), Calendar, SchedulingConflictSettings.Default, Clock, new Ids())
            .Handle(new RescheduleLessonScheduleCommand(lesson.Id, newStart, newStart.AddHours(1), null, scope, occurrence), CancellationToken.None);

    private Task<Result<LessonChangeRequestResponse>> CreateRequest(RequestStore requests, LessonSchedule lesson, DateTime? occurrence)
        => new CreateLessonChangeRequestCommandHandler(requests, _store, Calendar, new Ids(), Clock)
            .Handle(new CreateLessonChangeRequestCommand(
                lesson.StudentId, lesson.Id, "Hastayım", November17.AddDays(2), November17.AddDays(2).AddHours(1), occurrence),
                CancellationToken.None);

    private static void AssertError<T>(Result<T> result, string code)
    {
        Assert.True(result.IsFailure, "Başarısızlık bekleniyordu, başarılı döndü.");
        Assert.Equal(code, result.Error.Code);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class Ids : IIdGenerator
    {
        public Guid New() => Guid.NewGuid();
    }

    private sealed class RecordingDispatcher : ICommandDispatcher
    {
        public List<object> Commands { get; } = new();

        public Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
            where TResponse : Result
        {
            Commands.Add(command);
            var success = typeof(TResponse).GetMethod("Success", [typeof(LessonScheduleResponse)])!
                .Invoke(null, [null]);
            return Task.FromResult((TResponse)success!);
        }
    }

    private sealed class RequestStore(params LessonChangeRequest[] requests) : ILessonChangeRequestRepository
    {
        private readonly List<LessonChangeRequest> _requests = [.. requests];

        public Task AddAsync(LessonChangeRequest request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<LessonChangeRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken)
            => Task.FromResult(_requests.FirstOrDefault(request => request.Id == requestId));

        public Task<IReadOnlyCollection<LessonChangeRequest>> ListForTeacherAsync(Guid teacherUserId, bool onlyPending, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonChangeRequest>>(_requests.ToArray());

        public Task<IReadOnlyCollection<LessonChangeRequest>> ListForStudentAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonChangeRequest>>(_requests.ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Store : ILessonScheduleRepository
    {
        public List<LessonSchedule> Lessons { get; } = new();

        public List<LessonOccurrenceException> Exceptions { get; } = new();

        public int SaveCount { get; private set; }

        public Task<LessonSchedule?> GetByIdAsync(Guid lessonId, CancellationToken cancellationToken)
            => Task.FromResult(Lessons.FirstOrDefault(lesson => lesson.Id == lessonId));

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForTeacherUntilAsync(Guid teacherUserId, DateTime untilUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonSchedule>>(Lessons
                .Where(lesson => lesson.TeacherUserId == teacherUserId && lesson.Status != LessonScheduleStatus.Cancelled && lesson.StartAtUtc <= untilUtc)
                .ToArray());

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForTeacherAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Exceptions
                .Where(x => Lessons.Any(lesson => lesson.Id == x.SeriesLessonScheduleId && lesson.TeacherUserId == teacherUserId))
                .ToArray());

        public Task<IReadOnlyCollection<LessonOccurrenceException>> ListExceptionsForSeriesAsync(Guid seriesLessonScheduleId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<LessonOccurrenceException>>(Exceptions.Where(x => x.SeriesLessonScheduleId == seriesLessonScheduleId).ToArray());

        public Task UpsertExceptionAsync(LessonOccurrenceException occurrenceException, CancellationToken cancellationToken)
        {
            var existing = Exceptions.FirstOrDefault(x => x.SeriesLessonScheduleId == occurrenceException.SeriesLessonScheduleId
                && x.OriginalStartAtUtc == occurrenceException.OriginalStartAtUtc);
            if (existing is null)
            {
                Exceptions.Add(occurrenceException);
            }
            else
            {
                existing.Overwrite(occurrenceException.Action, occurrenceException.OverrideStartAtUtc, occurrenceException.OverrideEndAtUtc, occurrenceException.Note);
            }

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(LessonSchedule lessonSchedule, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Remove(LessonSchedule lessonSchedule) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListForStudentAsync(Guid studentId, DateTime startAtUtc, DateTime endAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LessonSchedule>> ListActiveForStudentUntilAsync(Guid studentId, DateTime untilUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
