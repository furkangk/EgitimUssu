using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Reporting.Infrastructure;

/// <summary>
/// E-07: Üretilmiş raporları (PDF dosyası dahil) ve raporlama projeksiyonlarını siler.
/// <para>
/// Bu modülün tuttuğu her şey <b>türev</b> veridir — kaynak modüllerdeki olaylardan yeniden
/// kurulabilir. Bu yüzden anonimleştirilmez, silinir. PDF'ler ayrıca depolamadan kaldırılır:
/// rapor dosyasının içinde öğrencinin adı yazılıdır.
/// </para>
/// </summary>
public sealed class ReportingPersonalDataAnonymizer : IPersonalDataAnonymizer
{
    private readonly ReportingDbContext _dbContext;
    private readonly IFileStorage _fileStorage;

    public ReportingPersonalDataAnonymizer(ReportingDbContext dbContext, IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
    }

    public string ModuleName => "Reporting";

    public async Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var studentIds = subject.StudentIds.ToArray();

        var reports = await _dbContext.StudentReports
            .Where(report => studentIds.Contains(report.StudentId) || report.CreatedByUserId == subject.UserId)
            .ToArrayAsync(cancellationToken);

        foreach (var report in reports.Where(report => !string.IsNullOrWhiteSpace(report.StorageKey)))
        {
            await _fileStorage.DeleteAsync(report.StorageKey, cancellationToken);
        }

        var studyProjections = await _dbContext.StudentStudyProjections
            .Where(projection => studentIds.Contains(projection.StudentId)).ToArrayAsync(cancellationToken);
        var performanceProjections = await _dbContext.StudentPerformanceProjections
            .Where(projection => studentIds.Contains(projection.StudentId)).ToArrayAsync(cancellationToken);
        var dashboards = await _dbContext.TeacherDashboardProjections
            .Where(projection => projection.TeacherUserId == subject.UserId).ToArrayAsync(cancellationToken);
        var monthlies = await _dbContext.TeacherMonthlyProjections
            .Where(projection => projection.TeacherUserId == subject.UserId).ToArrayAsync(cancellationToken);

        _dbContext.StudentReports.RemoveRange(reports);
        _dbContext.StudentStudyProjections.RemoveRange(studyProjections);
        _dbContext.StudentPerformanceProjections.RemoveRange(performanceProjections);
        _dbContext.TeacherDashboardProjections.RemoveRange(dashboards);
        _dbContext.TeacherMonthlyProjections.RemoveRange(monthlies);

        var affected = reports.Length + studyProjections.Length + performanceProjections.Length
            + dashboards.Length + monthlies.Length;

        if (affected > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return affected;
    }
}
