using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Parents.Infrastructure;

/// <summary>
/// M03-1: Students'tan gelen "mevcut veli kullanıcısını öğrenciye bağla" talebini karşılar.
/// Bağ **Parents'ta** yaşadığı için kayıt burada açılır; Students yalnız sonucu okur.
/// Bağ <c>Pending</c> başlar — onay, mevcut <c>children/{linkId}/approve</c> akışındadır.
/// </summary>
internal sealed class ParentChildLinkRequestService : IParentChildLinkRequestService
{
    private readonly ParentsDbContext _dbContext;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public ParentChildLinkRequestService(ParentsDbContext dbContext, IIdGenerator idGenerator, IClock clock)
    {
        _dbContext = dbContext;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<ParentLinkRequestOutcome> RequestPendingLinkAsync(
        Guid parentUserId,
        Guid studentId,
        string? childDisplayName,
        CancellationToken cancellationToken)
    {
        // "Veli daima kayıtlı kullanıcıdır": veli profili yoksa bağ açılmaz.
        var parentExists = await _dbContext.ParentProfiles
            .AnyAsync(profile => profile.UserId == parentUserId, cancellationToken);
        if (!parentExists)
        {
            return ParentLinkRequestOutcome.ParentNotRegistered;
        }

        var hasOpenLink = await _dbContext.ParentChildLinks.AnyAsync(
            link => link.ParentUserId == parentUserId
                && link.StudentId == studentId
                && (link.Status == ParentChildLinkStatus.Pending || link.Status == ParentChildLinkStatus.Approved),
            cancellationToken);
        if (hasOpenLink)
        {
            return ParentLinkRequestOutcome.AlreadyLinked;
        }

        // İlk veli birincil olur (V-C birincil tekilliği); zaten onaylı bir birincil varsa bu bağ birincil olmaz.
        var hasPrimary = await _dbContext.ParentChildLinks.AnyAsync(
            link => link.StudentId == studentId
                && link.Status == ParentChildLinkStatus.Approved
                && link.IsPrimaryContact,
            cancellationToken);

        var link = new ParentChildLink(
            _idGenerator.New(),
            parentUserId,
            studentId,
            childDisplayName,
            null,
            null,
            !hasPrimary,
            _clock.UtcNow);

        await _dbContext.ParentChildLinks.AddAsync(link, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ParentLinkRequestOutcome.Created;
    }
}
