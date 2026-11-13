using EgitimUssu.Modules.Settings.Domain;
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
            .Select(s => new { s.ShareStudyDataWithParent, s.ShareStudyDataWithTeacher, s.PrivacyLevel })
            .FirstOrDefaultAsync(cancellationToken);

        // Kayıt yoksa paylaşım açık ve seviye Standard varsayılır (öğrenci henüz kısıtlamadı).
        return row is null
            ? new StudentPrivacy(true, true, StudyPrivacyLevel.Standard)
            : new StudentPrivacy(row.ShareStudyDataWithParent, row.ShareStudyDataWithTeacher, ToContract(row.PrivacyLevel));
    }

    // M15-4: Settings.Domain enum'u → Shared.Contracts enum'u (modül sınırı).
    private static StudyPrivacyLevel ToContract(PrivacyLevel level)
        => level switch
        {
            PrivacyLevel.Limited => StudyPrivacyLevel.Limited,
            PrivacyLevel.Hidden => StudyPrivacyLevel.Hidden,
            _ => StudyPrivacyLevel.Standard
        };
}
