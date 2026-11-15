using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

public sealed class StudentAssignmentStatusReader(AssignmentsDbContext db) : IStudentAssignmentStatusReader
{
    public async Task<StudentAssignmentStatus> ReadAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken)
    {
        var counts = await db.Assignments.Where(x => studentIds.Contains(x.StudentId) && x.Status != AssignmentStatus.Cancelled)
            .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(cancellationToken);
        var total = counts.Sum(x => x.Count);
        var open = counts.Where(x => x.Status is AssignmentStatus.Pending or AssignmentStatus.ReturnedForRevision).Sum(x => x.Count);
        return new(total, open, total - open);
    }
}
