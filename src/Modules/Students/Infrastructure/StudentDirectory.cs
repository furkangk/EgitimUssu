using EgitimUssu.Modules.Students.Domain;
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

    public async Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
    {
        // B-07: Ücret öğretmen-öğrenci ikilisine özeldir. Ücret girilmemişse (null) tahmini bir tutar
        // üretmeyiz — çağıran (Payments/M07-2) kayıt açmadan geçer.
        return await _dbContext.TeacherStudentLinks
            .AsNoTracking()
            .Where(link => link.TeacherUserId == teacherUserId
                && link.StudentId == studentId
                && link.AgreedRateAmount != null)
            .Select(link => new AgreedLessonRate(link.AgreedRateAmount!.Value, link.Currency))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
    {
        // M10-2: Yaşayan bağ = Manual (öğretmenin kendi açtığı kayıt), InviteSent (davet yolda) veya
        // Linked (öğrenci devraldı). Rejected/Disconnected ve arşivlenmiş kayıt erişim vermez.
        return _dbContext.TeacherStudentLinks
            .AsNoTracking()
            .AnyAsync(
                link => link.TeacherUserId == teacherUserId
                    && link.StudentId == studentId
                    && !link.IsArchived
                    && (link.Status == TeacherStudentLinkStatus.Manual
                        || link.Status == TeacherStudentLinkStatus.InviteSent
                        || link.Status == TeacherStudentLinkStatus.Linked),
                cancellationToken);
    }
}
