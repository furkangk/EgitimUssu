using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

/// <summary>
/// P15 T2 (K-13): <see cref="ITeacherStudentAccess"/>'in Students implementasyonu. Students öğretmen–öğrenci
/// bağının otoritesidir; diğer modüller bu cevabı <c>Shared/Contracts</c> üzerinden okur.
/// </summary>
internal sealed class TeacherStudentAccess : ITeacherStudentAccess
{
    private readonly StudentsDbContext _dbContext;

    public TeacherStudentAccess(StudentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TeacherAccessScope> GetScopeAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
    {
        // Arşivli bağ hiçbir durumda erişim vermez: öğretmen "bu öğrenciyle işim bitti" demiştir.
        var statuses = await _dbContext.TeacherStudentLinks
            .AsNoTracking()
            .Where(link => link.TeacherUserId == teacherUserId && link.StudentId == studentId && !link.IsArchived)
            .Select(link => link.Status)
            .ToListAsync(cancellationToken);

        if (statuses.Contains(TeacherStudentLinkStatus.Linked))
        {
            return TeacherAccessScope.Private;
        }

        // Manual/InviteSent bağ öğrencinin onayı değildir; ancak öğretmenin KENDİ açtığı profilde erişim verir.
        // Bitmiş bağ (Rejected/Disconnected) — plan formülü yalnız "arşivli değil" diyordu, ama eski
        // IsLinkedTeacherOfStudentAsync de erişim vermiyordu; fail-closed korunur.
        var isLivingUnconfirmedLink = statuses.Any(status =>
            status is TeacherStudentLinkStatus.Manual or TeacherStudentLinkStatus.InviteSent);
        if (!isLivingUnconfirmedLink)
        {
            return TeacherAccessScope.None;
        }

        var ownsProfile = await _dbContext.StudentProfiles
            .AsNoTracking()
            .AnyAsync(
                profile => profile.Id == studentId
                    && profile.Origin == StudentOrigin.TeacherManaged
                    && profile.CreatedByTeacherUserId == teacherUserId,
                cancellationToken);

        return ownsProfile ? TeacherAccessScope.Private : TeacherAccessScope.None;
    }
}
