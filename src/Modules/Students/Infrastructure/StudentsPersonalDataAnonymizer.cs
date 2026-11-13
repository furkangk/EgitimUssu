using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

/// <summary>
/// E-07: Öğrenci profilini kimliksizleştirir.
/// <para>
/// ⚠️ Yalnız <b>silinen kişiye ait</b> profiller anonimleştirilir. Silinen kullanıcı bir öğretmense,
/// onun açtığı öğrenci profilleri <b>başka</b> veri sahiplerine aittir ve dokunulmaz — o öğrencilerin
/// verisini silmek, silme talebini vermeyen kişilerin verisini silmek olurdu.
/// </para>
/// </summary>
public sealed class StudentsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly StudentsDbContext _dbContext;
    private readonly IClock _clock;

    public StudentsPersonalDataAnonymizer(StudentsDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public string ModuleName => "Students";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var profiles = await _dbContext.StudentProfiles
            .Where(profile => profile.UserId == subject.UserId || studentIds.Contains(profile.Id))
            .ToArrayAsync(cancellationToken);

        // Davet kayıtları: silinen kişi öğrenci (davetin konusu), öğretmen (daveti açan) ya da
        // veli (daveti kullanan) olabilir; her üçünde de çocuğun adı bu satırda durur.
        var invites = await _dbContext.StudentParentInvites
            .Where(invite => studentIds.Contains(invite.StudentId)
                || invite.TeacherUserId == subject.UserId
                || invite.ClaimedByParentUserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var now = _clock.UtcNow;
        var affected = profiles.Count(profile => profile.AnonymizePersonalData(now))
            + invites.Count(invite => invite.AnonymizePersonalData());

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
