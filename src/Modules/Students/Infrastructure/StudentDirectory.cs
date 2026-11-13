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
    private readonly ITeacherStudentAccess _access;

    public StudentDirectory(StudentsDbContext dbContext, ITeacherStudentAccess access)
    {
        _dbContext = dbContext;
        _access = access;
    }

    public async Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // E-07: Birleştirilmiş (IsMerged) profiller de döner — kişisel verisi hâlâ orada durur.
        return await _dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.Id)
            .ToArrayAsync(cancellationToken);
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

    public async Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
    {
        // M10-2 → P15 T2: kapsamın tek kaynağı ITeacherStudentAccess. Yalnız özel ders bağı (Private) sayılır;
        // P22'nin sınıf kapsamı (Class) bu eski soruya "evet" demez.
        return await _access.GetScopeAsync(teacherUserId, studentId, cancellationToken) == TeacherAccessScope.Private;
    }

    public async Task<string?> GetDisplayNameAsync(Guid studentId, CancellationToken cancellationToken)
    {
        // M14-4: Rapor başlığı için ad. Profil yoksa null döner; Reporting yer tutucu yazar.
        return await _dbContext.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == studentId)
            .Select(profile => profile.FullName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
