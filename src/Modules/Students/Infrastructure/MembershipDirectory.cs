using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

// Ö-D: Students, öğrenci üyelik (Free/Premium) seviyesinin otoritesidir.
// IMembershipDirectory sözleşmesini uygulayarak Study gibi modüllerin Free/Premium
// kapılarını DbContext'e doğrudan erişmeden okumasını sağlar (modül izolasyonu).
internal sealed class MembershipDirectory : IMembershipDirectory
{
    private readonly StudentsDbContext _dbContext;

    public MembershipDirectory(StudentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MembershipTier> GetTierAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tier = await _dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (MembershipTier?)profile.MembershipTier)
            .FirstOrDefaultAsync(cancellationToken);

        return tier ?? MembershipTier.Free;
    }
}
