using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

// M14-4: Assignments, ödev durumunun otoritesidir. IStudentAssignmentDigestDirectory sözleşmesiyle
// Reporting (PDF rapor özeti) ödev tamamlama oranını AssignmentsDbContext'e dokunmadan okur.
internal sealed class StudentAssignmentDigestDirectory : IStudentAssignmentDigestDirectory
{
    private readonly AssignmentsDbContext _dbContext;

    public StudentAssignmentDigestDirectory(AssignmentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AssignmentCompletionSummary> GetCompletionAsync(
        Guid studentUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        // Dönem ölçütü teslim tarihidir; teslim tarihi girilmemiş ödev oluşturulma tarihine düşer,
        // yoksa hiçbir döneme girmez ve oran sessizce eksik çıkardı.
        var query = _dbContext.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.StudentId == studentUserId)
            .Where(assignment => (assignment.DueDateUtc ?? assignment.CreatedOnUtc) >= fromUtc
                && (assignment.DueDateUtc ?? assignment.CreatedOnUtc) < toUtc);

        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
        {
            return new AssignmentCompletionSummary(0, 0);
        }

        // Onaylanmış ödev de tamamlanmıştır (öğretmen değerlendirmesi durumu Completed'dan Approved'a taşır).
        var completed = await query.CountAsync(
            assignment => assignment.Status == AssignmentStatus.Completed
                || assignment.Status == AssignmentStatus.Approved,
            cancellationToken);

        return new AssignmentCompletionSummary(total, completed);
    }
}
