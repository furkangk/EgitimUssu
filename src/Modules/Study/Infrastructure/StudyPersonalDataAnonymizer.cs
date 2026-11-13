using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>
/// E-07: Bireysel çalışma verisini <b>siler</b> — anonimleştirmez.
/// <para>
/// Gerekçe saklama politikasındandır: çalışma kayıtlarının işlenme amacı üyelikle sınırlıdır
/// (<c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §2.2). Ders ve ödeme kayıtlarının aksine
/// bunları saklamayı gerektiren bir ticari ya da yasal sebep yoktur; amaç bitince veri gider.
/// </para>
/// </summary>
public sealed class StudyPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly StudyDbContext _dbContext;

    public StudyPersonalDataAnonymizer(StudyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => "Study";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();
        var affected = 0;

        affected += await RemoveAsync(_dbContext.StudyNotes, note => studentIds.Contains(note.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudySessions, session => studentIds.Contains(session.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.TestResults, result => studentIds.Contains(result.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.MockExams, exam => studentIds.Contains(exam.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudyGoals, goal => studentIds.Contains(goal.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudyStreaks, streak => studentIds.Contains(streak.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudentAchievements, achievement => studentIds.Contains(achievement.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudyTopics, topic => studentIds.Contains(topic.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudentTopicCatalogs, topic => studentIds.Contains(topic.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudentSubjectCatalogs, catalog => studentIds.Contains(catalog.StudentId), cancellationToken);
        affected += await RemoveAsync(_dbContext.StudyStudents, student => student.UserId == subject.UserId, cancellationToken);

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }

    private async Task<int> RemoveAsync<TEntity>(
        DbSet<TEntity> set,
        System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var rows = await set.Where(predicate).ToArrayAsync(cancellationToken);
        set.RemoveRange(rows);
        return rows.Length;
    }
}
