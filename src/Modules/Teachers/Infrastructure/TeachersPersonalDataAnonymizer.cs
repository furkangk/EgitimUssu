using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

/// <summary>
/// E-07: Öğretmen profilini kimliksizleştirir, aramadan çıkarır ve yüklediği dosyaları siler.
/// <para>
/// Dosyalar veritabanı satırından <b>önce</b> silinir: anahtar düşürüldükten sonra dosyayı silmenin
/// yolu kalmaz ve depolamada sahipsiz bir kişisel veri (profil fotoğrafı) kalırdı.
/// </para>
/// </summary>
public sealed class TeachersPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly TeachersDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly IClock _clock;

    public TeachersPersonalDataAnonymizer(TeachersDbContext dbContext, IFileStorage fileStorage, IClock clock)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _clock = clock;
    }

    public string ModuleName => "Teachers";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var profiles = await _dbContext.TeacherProfiles
            .Where(profile => profile.UserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        if (profiles.Length == 0)
        {
            return 0;
        }

        var profileIds = profiles.Select(profile => profile.Id).ToArray();
        var certificates = await _dbContext.TeacherCertificates
            .Where(certificate => profileIds.Contains(certificate.TeacherProfileId))
            .ToArrayAsync(cancellationToken);

        foreach (var key in profiles.Select(profile => profile.ProfilePhotoKey)
                     .Concat(certificates.Select(certificate => certificate.FileUrl))
                     .Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            await _fileStorage.DeleteAsync(key!, cancellationToken);
        }

        var now = _clock.UtcNow;
        var affected = profiles.Count(profile => profile.AnonymizePersonalData(now))
            + certificates.Count(certificate => certificate.AnonymizePersonalData());

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
