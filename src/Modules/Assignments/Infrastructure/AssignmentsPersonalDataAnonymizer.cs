using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

/// <summary>
/// E-07: Ödev, ders notu ve ders kaynaklarındaki serbest metinleri siler; yüklenen dosyaları
/// depolamadan kaldırır.
/// <para>
/// Kayıt satırları kalır (ders geçmişinin iskeleti), içerik gider. Dosyalar veritabanı
/// anahtarından <b>önce</b> silinir; sıra tersse anahtar kaybolur ve dosya depolamada sahipsiz kalır.
/// </para>
/// </summary>
public sealed class AssignmentsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly AssignmentsDbContext _dbContext;
    private readonly IFileStorage _fileStorage;

    public AssignmentsPersonalDataAnonymizer(AssignmentsDbContext dbContext, IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public string ModuleName => "Assignments";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var assignments = await _dbContext.Assignments
            .Where(assignment => studentIds.Contains(assignment.StudentId)
                || assignment.TeacherUserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var notes = await _dbContext.LessonNotes
            .Where(note => studentIds.Contains(note.StudentId) || note.TeacherUserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var resources = await _dbContext.LessonResources
            .Where(resource => resource.TeacherUserId == subject.UserId
                || (resource.StudentId != null && studentIds.Contains(resource.StudentId.Value)))
            .ToArrayAsync(cancellationToken);

        foreach (var key in assignments.Select(assignment => assignment.AttachmentStorageKey)
                     .Concat(resources.Select(resource => resource.StorageKey))
                     .Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            await _fileStorage.DeleteAsync(key!, cancellationToken);
        }

        var affected = assignments.Count(assignment => assignment.AnonymizePersonalData())
            + notes.Count(note => note.AnonymizePersonalData())
            + resources.Count(resource => resource.AnonymizePersonalData());

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
