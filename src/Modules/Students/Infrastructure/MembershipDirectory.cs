using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

// ⚠️ GEÇİCİ KÖPRÜ (P12'de silinecek): K-09'dan sonra üyeliğin otoritesi Students değil Membership'tir.
// Bu sınıf yalnız eski StudentProfile.MembershipTier alanını EntitlementDirectory'ye taşır;
// kapılar artık IEntitlementDirectory okur, bu sözleşmeyi değil.
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
