using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.LessonSessions.Infrastructure;

/// <summary>
/// E-07: Ders oturumlarındaki serbest metinleri ve bağlantıları siler; süre/tarih/durum korunur.
/// <para>
/// Oturum kaydı ücretlendirmenin dayanağıdır: kaç ders yapıldığı sorusu, ödeme kaydıyla birlikte
/// yasal saklama süresince cevaplanabilir olmalıdır. Silinen, öğretmenin yazdığı notlar ve
/// toplantı/kayıt bağlantılarıdır.
/// </para>
/// </summary>
public sealed class LessonSessionsPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly LessonSessionsDbContext _dbContext;

    public LessonSessionsPersonalDataAnonymizer(LessonSessionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => "LessonSessions";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var sessions = await _dbContext.LessonSessions
            .Where(session => studentIds.Contains(session.StudentId)
                || session.TeacherUserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        var affected = sessions.Count(session => session.AnonymizePersonalData());
        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
