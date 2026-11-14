using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

internal sealed class StudentParentInviteRepository : IStudentParentInviteRepository
{
    private readonly StudentsDbContext _dbContext;

    public StudentParentInviteRepository(StudentsDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(StudentParentInvite invite, CancellationToken cancellationToken)
        => _dbContext.StudentParentInvites.AddAsync(invite, cancellationToken).AsTask();

    public Task<StudentParentInvite?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken)
        => _dbContext.StudentParentInvites.FirstOrDefaultAsync(i => i.InviteCode == inviteCode, cancellationToken);

    public Task<StudentParentInvite?> GetByIdAsync(Guid inviteId, CancellationToken cancellationToken)
        => _dbContext.StudentParentInvites.FindAsync(new object?[] { inviteId }, cancellationToken).AsTask();

    public async Task<IReadOnlyCollection<StudentParentInvite>> ListPendingByStudentAsync(Guid studentId, CancellationToken cancellationToken)
        => await _dbContext.StudentParentInvites
            .Where(i => i.StudentId == studentId && i.Status == ParentInviteStatus.Pending)
            .ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
