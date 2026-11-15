using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

/// <summary>Kaydedilmiş seanslar ve henüz kaydedilmemiş mevcut seanstan uygun günleri türetir.</summary>
public static class StudyStreakCalculator
{
    public static IReadOnlyCollection<DateOnly> QualifyingDays(
        IEnumerable<StudySession> sessions, StudySession? current, StudyGoal? goal, ILocalCalendar calendar)
    {
        var all = current is null ? sessions : sessions.Where(s => s.Id != current.Id).Append(current);
        return all.Where(s => s.Status == StudySessionStatus.Completed && s.EndedAtUtc.HasValue)
            .GroupBy(s => calendar.StudyDay(s.EndedAtUtc!.Value))
            .Where(day => StreakRules.DayCounts(day.Sum(s => s.EffectiveMinutes),
                goal?.DailyGoalMinutes ?? 0, goal?.StreakThresholdPercent ?? 60))
            .Select(day => day.Key).Order().ToArray();
    }
}
