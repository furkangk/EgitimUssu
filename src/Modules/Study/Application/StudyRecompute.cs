using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

/// <summary>
/// Bir (öğrenci, ders, konu) için <see cref="StudyTopic"/> rollup'ını o öğrencinin tamamlanmış
/// seanslarından yeniden türetir. Seans düzenleme/silme sonrası çağrılır; yoksa oluşturur, seans
/// kalmadıysa siler. Net/rollup tutarlılığını korur; streak zincirine dokunmaz (v1 YAGNI).
/// </summary>
public static class StudyRecompute
{
    public static async Task RebuildTopicAsync(
        IStudyRepository repository,
        IIdGenerator idGenerator,
        Guid studentId,
        string subject,
        string? topic,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var sessions = await repository.ListCompletedSessionsByTopicAsync(studentId, subject, topic, cancellationToken);
        var existing = await repository.GetTopicAsync(studentId, subject, topic, cancellationToken);

        if (sessions.Count == 0)
        {
            if (existing is not null)
            {
                repository.RemoveTopic(existing);
            }

            return;
        }

        var total = sessions.Sum(s => s.EffectiveMinutes);
        var count = sessions.Count;
        var first = sessions.Min(s => s.EndedAtUtc ?? s.StartedAtUtc);
        var last = sessions.Max(s => s.EndedAtUtc ?? s.StartedAtUtc);

        if (existing is null)
        {
            var rebuilt = new StudyTopic(idGenerator.New(), studentId, subject, topic, total, last);
            rebuilt.Overwrite(total, count, first, last);
            await repository.AddTopicAsync(rebuilt, cancellationToken);
        }
        else
        {
            existing.Overwrite(total, count, first, last);
        }
    }
}
