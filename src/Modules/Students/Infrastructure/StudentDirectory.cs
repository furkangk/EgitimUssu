using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

// Y-öğrenci-sahiplik: Students, öğrenci↔kullanıcı bağının otoritesidir.
// IStudentDirectory sözleşmesini uygulayarak diğer modüllerin (ör. Scheduling)
// bu bağı DbContext'e doğrudan erişmeden, güvenli biçimde okumasını sağlar.
internal sealed class StudentDirectory : IStudentDirectory
{
    private readonly StudentsDbContext _dbContext;

    public StudentDirectory(StudentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return await _dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
