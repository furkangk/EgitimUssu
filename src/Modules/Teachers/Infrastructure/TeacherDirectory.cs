using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

// M14-4: Teachers, öğretmen profilinin otoritesidir. ITeacherDirectory sözleşmesiyle Reporting
// (PDF rapor başlığı) öğretmen adını TeachersDbContext'e dokunmadan okur.
internal sealed class TeacherDirectory : ITeacherDirectory
{
    private readonly TeachersDbContext _dbContext;

    public TeacherDirectory(TeachersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetDisplayNameAsync(Guid teacherUserId, CancellationToken cancellationToken)
    {
        return await _dbContext.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == teacherUserId)
            .Select(profile => profile.FullName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
