using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit.TestDoubles;

/// <summary>
/// P15 T2: <see cref="ILessonScheduleDirectory"/>'nin verilen planlı derslerle cevap veren sahtesi.
/// Listede olmayan kimlik (ya da öğretmensiz self ders) <c>null</c> döner — gerçek sözleşmeyle aynı.
/// </summary>
/// <param name="schedules">Dizindeki planlı dersler.</param>
public sealed class FakeLessonScheduleDirectory(params LessonScheduleDetails[] schedules) : ILessonScheduleDirectory
{
    /// <inheritdoc />
    public Task<LessonScheduleDetails?> GetAsync(Guid lessonScheduleId, CancellationToken cancellationToken)
        => Task.FromResult(schedules.FirstOrDefault(schedule => schedule.LessonScheduleId == lessonScheduleId));

    /// <inheritdoc />
    public Task<IReadOnlyCollection<TeacherBusyInterval>> ListTeacherBusyIntervalsAsync(
        Guid teacherUserId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<TeacherBusyInterval>>([]);
}
