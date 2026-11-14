using System.Text.Json;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

/// <summary>
/// P15 / N-28: İki öğrenci profili birleştiğinde (Ö-C claim/merge) veli tarafını kanonik profile taşır.
/// Eskiden Parents bu olayı tüketmiyordu: veli bağları, öğrenci→kullanıcı eşlemesi ve veli paneli özeti eski
/// kimlikte kalıyor, birleşme sonrası veli paneli boşalıyordu.
/// <list type="bullet">
/// <item><b>Veli bağları</b> (benzersiz indeks yok — çakışma sessizdir): aynı velinin iki profildeki yaşayan
/// (<c>Pending</c>/<c>Approved</c>) bağlarından biri kalır — onaylı olan, eşitlikte kanoniktekine; diğerleri
/// <c>Revoked</c> olur (satır geçmiş olarak kalır). Birden çok onaylı birincil veli kalacaksa en erken bağlanan
/// birincil kalır, diğerleri <b>taşımadan önce</b> düşürülür (V-C; P15 T4'ün kısmi benzersiz indeksi taşıma ifadesini
/// reddederdi). Sonra bütün bağlar kanonik kimliğe taşınır.</item>
/// <item><b>Öğrenci→kullanıcı eşlemesi</b> (<c>known_students</c>, benzersiz): kanonik satır doğru kullanıcıyı
/// taşır, kaynak satır silinir; kanonik satır henüz yoksa kaynak taşınır (kanoniğin oluşturma olayı düzeltir).</item>
/// <item><b>Veli paneli özeti</b> (<c>child_progress_snapshots</c>, benzersiz): kanonikte yoksa taşınır, varsa
/// sayaçlar toplanır ve kaynak silinir.</item>
/// </list>
/// Doğrudan cross-module DB erişimi yok — yalnız integration event. Replay koruması ortak inbox üzerinden.
/// </summary>
internal sealed class StudentProfilesMergedParentsHandler : IdempotentIntegrationEventHandler
{
    public StudentProfilesMergedParentsHandler(ParentsDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private ParentsDbContext ParentsDb => (ParentsDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Students"
            && integrationEvent.Name == "StudentProfilesMergedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<StudentProfilesMergedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return false;
        }

        var from = payload.FromStudentId;
        var to = payload.ToStudentId;
        var now = Clock.UtcNow;

        await MergeLinksAsync(from, to, now, cancellationToken);
        await MergeKnownStudentAsync(from, to, cancellationToken);
        await MergeSnapshotAsync(from, to, now, cancellationToken);
        return true;
    }

    private async Task MergeLinksAsync(Guid from, Guid to, DateTime now, CancellationToken cancellationToken)
    {
        var links = await ParentsDb.ParentChildLinks.AsNoTracking()
            .Where(link => link.StudentId == from || link.StudentId == to)
            .Select(link => new
            {
                link.Id,
                link.ParentUserId,
                link.StudentId,
                link.Status,
                link.IsPrimaryContact,
                link.LinkedOnUtc,
                link.RequestedOnUtc
            })
            .ToListAsync(cancellationToken);

        var live = links
            .Where(link => link.Status is ParentChildLinkStatus.Pending or ParentChildLinkStatus.Approved)
            .ToList();

        var survivors = live
            .GroupBy(link => link.ParentUserId)
            .Select(group => group
                .OrderByDescending(link => link.Status == ParentChildLinkStatus.Approved)
                .ThenByDescending(link => link.StudentId == to)
                .ThenBy(link => link.RequestedOnUtc)
                .First())
            .ToList();

        var survivorIds = survivors.Select(link => link.Id).ToHashSet();
        var loserIds = live.Where(link => !survivorIds.Contains(link.Id)).Select(link => link.Id).ToList();
        if (loserIds.Count > 0)
        {
            await ParentsDb.ParentChildLinks
                .Where(link => loserIds.Contains(link.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(link => link.Status, ParentChildLinkStatus.Revoked)
                        .SetProperty(link => link.IsPrimaryContact, false)
                        .SetProperty(link => link.UpdatedOnUtc, now),
                    cancellationToken);
        }

        // Fazla birincil veli TAŞIMADAN ÖNCE düşürülür: iki profilin onaylı birincilleri aynı anda kanonik
        // kimliğe gelirse "öğrenci başına tek onaylı birincil" kısmi benzersiz indeksi (P15 T4) taşıma ifadesini
        // reddeder ve mesaj outbox'ta sonsuza dek yeniden denenir. En erken bağlanan birincil kalır.
        var demotedPrimaries = survivors
            .Where(link => link.Status == ParentChildLinkStatus.Approved && link.IsPrimaryContact)
            .OrderBy(link => link.LinkedOnUtc ?? link.RequestedOnUtc)
            .Skip(1)
            .Select(link => link.Id)
            .ToList();
        if (demotedPrimaries.Count > 0)
        {
            await ParentsDb.ParentChildLinks
                .Where(link => demotedPrimaries.Contains(link.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(link => link.IsPrimaryContact, false)
                        .SetProperty(link => link.UpdatedOnUtc, now),
                    cancellationToken);
        }

        await ParentsDb.ParentChildLinks
            .Where(link => link.StudentId == from)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(link => link.StudentId, to)
                    .SetProperty(link => link.UpdatedOnUtc, now),
                cancellationToken);
    }

    private async Task MergeKnownStudentAsync(Guid from, Guid to, CancellationToken cancellationToken)
    {
        if (await ParentsDb.KnownStudents.AnyAsync(student => student.StudentId == to, cancellationToken))
        {
            await ParentsDb.KnownStudents
                .Where(student => student.StudentId == from)
                .ExecuteDeleteAsync(cancellationToken);
            return;
        }

        await ParentsDb.KnownStudents
            .Where(student => student.StudentId == from)
            .ExecuteUpdateAsync(setters => setters.SetProperty(student => student.StudentId, to), cancellationToken);
    }

    private async Task MergeSnapshotAsync(Guid from, Guid to, DateTime now, CancellationToken cancellationToken)
    {
        var merged = await ParentsDb.ChildProgressSnapshots
            .FirstOrDefaultAsync(snapshot => snapshot.StudentId == from, cancellationToken);
        if (merged is null)
        {
            return;
        }

        var canonical = await ParentsDb.ChildProgressSnapshots
            .FirstOrDefaultAsync(snapshot => snapshot.StudentId == to, cancellationToken);
        if (canonical is null)
        {
            ParentsDb.Entry(merged).State = EntityState.Detached;
            await ParentsDb.ChildProgressSnapshots
                .Where(snapshot => snapshot.StudentId == from)
                .ExecuteUpdateAsync(setters => setters.SetProperty(snapshot => snapshot.StudentId, to), cancellationToken);
            return;
        }

        // Kanonik satırın güncellemesi ve kaynağın silinmesi tabanın SaveChanges'inde, inbox kaydıyla birlikte.
        canonical.Absorb(merged, now);
        ParentsDb.ChildProgressSnapshots.Remove(merged);
    }
}
