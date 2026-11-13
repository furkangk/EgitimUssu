using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

/// <summary>
/// E-07: Veli profilini ve veli–çocuk bağındaki kişisel alanları kimliksizleştirir; bu modülün
/// tuttuğu <b>türev</b> kayıtları (çocuk gelişim özeti, tanınan öğrenci) siler — onlar başka
/// modüllerden beslenen kopyalardır, saklanmalarının bir amacı kalmaz.
/// </summary>
public sealed class ParentsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly ParentsDbContext _dbContext;
    private readonly IClock _clock;

    public ParentsPersonalDataAnonymizer(ParentsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public string ModuleName => "Parents";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var profiles = await _dbContext.ParentProfiles
            .Where(profile => profile.UserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var links = await _dbContext.ParentChildLinks
            .Where(link => link.ParentUserId == subject.UserId || studentIds.Contains(link.StudentId))
            .ToArrayAsync(cancellationToken);

        var snapshots = await _dbContext.ChildProgressSnapshots
            .Where(snapshot => studentIds.Contains(snapshot.StudentId))
            .ToArrayAsync(cancellationToken);

        var known = await _dbContext.KnownStudents
            .Where(student => student.UserId == subject.UserId || studentIds.Contains(student.StudentId))
            .ToArrayAsync(cancellationToken);

        var now = _clock.UtcNow;
        var affected = profiles.Count(profile => profile.AnonymizePersonalData(now))
            + links.Count(link => link.AnonymizePersonalData(now))
            + snapshots.Length
            + known.Length;

        _dbContext.ChildProgressSnapshots.RemoveRange(snapshots);
        _dbContext.KnownStudents.RemoveRange(known);

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
