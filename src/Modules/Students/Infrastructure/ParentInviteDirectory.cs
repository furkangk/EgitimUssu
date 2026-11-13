using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

/// <summary>
/// A-05: Davet tüketimi <b>atomik</b>. Eski <c>MarkClaimedAsync</c> "önce oku, sonra işaretle"
/// yapıyordu ve Parents tarafında bağ kaydedildikten <i>sonra</i> çağrılıyordu; ikinci yazım
/// düştüğünde davet <c>Pending</c> kalıp tekrar kullanılabiliyordu.
/// </summary>
internal sealed class ParentInviteDirectory : IParentInviteDirectory
{
    private readonly StudentsDbContext _dbContext;
    private readonly IClock _clock;

    public ParentInviteDirectory(StudentsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var normalized = SecureCode.Normalize(inviteCode);
        var invite = await _dbContext.StudentParentInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.InviteCode == normalized && item.Status == ParentInviteStatus.Pending,
                cancellationToken);

        return invite is null
            ? null
            : new ParentInviteInfo(invite.Id, invite.StudentId, invite.ChildDisplayName, invite.TeacherUserId, invite.ExpiresOnUtc);
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(Guid inviteId, Guid parentUserId, CancellationToken cancellationToken)
    {
        // Koşullu güncelleme: durum hâlâ Pending ise tüket. İki eşzamanlı istek aynı kodu
        // kullanmaya çalışırsa yalnız biri 1 satır günceller; diğeri 0 alır ve reddedilir.
        // İlişkisel olmayan sağlayıcıda (test/dev InMemory) `ExecuteUpdate` yoktur; orada
        // oku-değiştir-kaydet yoluna düşülür (tek süreç, yarış yok).
        if (!_dbContext.Database.IsRelational())
        {
            var tracked = await _dbContext.StudentParentInvites
                .FirstOrDefaultAsync(item => item.Id == inviteId, cancellationToken);
            if (tracked is null || tracked.Status != ParentInviteStatus.Pending)
            {
                return false;
            }

            tracked.Claim(parentUserId, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var now = _clock.UtcNow;
        var affected = await _dbContext.StudentParentInvites
            .Where(item => item.Id == inviteId && item.Status == ParentInviteStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, ParentInviteStatus.Claimed)
                    .SetProperty(item => item.ClaimedByParentUserId, parentUserId)
                    .SetProperty(item => item.ClaimedOnUtc, now),
                cancellationToken);

        return affected == 1;
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid inviteId, CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
        {
            var tracked = await _dbContext.StudentParentInvites
                .FirstOrDefaultAsync(item => item.Id == inviteId, cancellationToken);
            tracked?.ReleaseClaim(_clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await _dbContext.StudentParentInvites
            .Where(item => item.Id == inviteId && item.Status == ParentInviteStatus.Claimed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, ParentInviteStatus.Pending)
                    .SetProperty(item => item.ClaimedByParentUserId, (Guid?)null)
                    .SetProperty(item => item.ClaimedOnUtc, (DateTime?)null),
                cancellationToken);
    }
}
