using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

internal sealed class StudentPrivacyDirectory : IStudentPrivacyDirectory
{
    private readonly SettingsDbContext _dbContext;

    public StudentPrivacyDirectory(SettingsDbContext dbContext) => _dbContext = dbContext;

    public async Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => new { s.ShareStudyDataWithParent, s.ShareStudyDataWithTeacher })
            .FirstOrDefaultAsync(cancellationToken);

        // Kayıt yoksa paylaşım açık varsayılır (öğrenci henüz kısıtlamadı).
        return row is null
            ? new StudentPrivacy(true, true)
            : new StudentPrivacy(row.ShareStudyDataWithParent, row.ShareStudyDataWithTeacher);
    }
}
