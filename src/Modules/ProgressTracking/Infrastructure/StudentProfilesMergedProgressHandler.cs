using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

/// <summary>
/// P15 / N-28: İki öğrenci profili birleştiğinde (Ö-C claim/merge) gelişim verisini kanonik profile taşır.
/// Eskiden ProgressTracking bu olayı tüketmiyordu; birleşen profilin konu hâkimiyeti, hedefleri ve dönem
/// fotoğrafları eski kimlikte kalıyordu.
/// <para>
/// <c>topic_masteries (StudentId, Subject, Topic)</c> ve <c>progress_snapshots (StudentId, Subject, Period,
/// PeriodStart)</c> benzersizdir: kör yeniden atama kanonikte aynı anahtar varsa indeks ihlali verir ve mesaj
/// outbox'ta sonsuza dek yeniden denenir. Kural: <b>çakışmayan satır taşınır, çakışanda kanonik kazanır</b>,
/// kaynakta kalan satır silinir. Hâkimiyet türetilmiş veridir (çalışma/test/ders olaylarından); öğretmenin açtığı
/// hesapsız profilin hâkimiyeti yalnız ders katılımından gelir, öğrencinin kendi profilininki ise kendi
/// çalışmasından — çakışmada öğrencinin kendi ölçümü korunur. <c>topic_goals</c> benzersiz değildir, hepsi taşınır.
/// </para>
/// Doğrudan cross-module DB erişimi yok — yalnız integration event. Replay koruması ortak inbox üzerinden.
/// </summary>
internal sealed class StudentProfilesMergedProgressHandler : IdempotentIntegrationEventHandler
{
    public StudentProfilesMergedProgressHandler(ProgressTrackingDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private ProgressTrackingDbContext ProgressDb => (ProgressTrackingDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfilesMergedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            throw new PoisonMessageException("Olay yükü null olamaz.");
        }

        var from = payload.FromStudentId;
        var to = payload.ToStudentId;

        await ProgressDb.TopicGoals
            .Where(goal => goal.StudentId == from)
            .ExecuteUpdateAsync(setters => setters.SetProperty(goal => goal.StudentId, to), cancellationToken);

        await ProgressDb.TopicMasteries
            .Where(source => source.StudentId == from
                && !ProgressDb.TopicMasteries.Any(target => target.StudentId == to
                    && target.Subject == source.Subject
                    && target.Topic == source.Topic))
            .ExecuteUpdateAsync(setters => setters.SetProperty(mastery => mastery.StudentId, to), cancellationToken);
        await ProgressDb.TopicMasteries
            .Where(mastery => mastery.StudentId == from)
            .ExecuteDeleteAsync(cancellationToken);

        await ProgressDb.ProgressSnapshots
            .Where(source => source.StudentId == from
                && !ProgressDb.ProgressSnapshots.Any(target => target.StudentId == to
                    && target.Subject == source.Subject
                    && target.Period == source.Period
                    && target.PeriodStart == source.PeriodStart))
            .ExecuteUpdateAsync(setters => setters.SetProperty(snapshot => snapshot.StudentId, to), cancellationToken);
        await ProgressDb.ProgressSnapshots
            .Where(snapshot => snapshot.StudentId == from)
            .ExecuteDeleteAsync(cancellationToken);

        return true;
    }
}
