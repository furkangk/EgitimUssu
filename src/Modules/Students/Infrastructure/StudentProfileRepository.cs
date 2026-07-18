using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

internal sealed class StudentProfileRepository : IStudentProfileRepository
{
    private readonly StudentsDbContext _dbContext;

    public StudentProfileRepository(StudentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<StudentProfile?> GetByIdAsync(Guid studentId, CancellationToken cancellationToken)
    {
        return _dbContext.StudentProfiles
            .Include(profile => profile.Subjects)
            .FirstOrDefaultAsync(profile => profile.Id == studentId, cancellationToken);
    }

    public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.StudentProfiles
            .Include(profile => profile.Subjects)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public Task<bool> ExistsByContactEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _dbContext.StudentProfiles.AnyAsync(
            profile => profile.ContactEmail != null && profile.ContactEmail.ToUpper() == normalizedEmail,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<StudentProfile>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
    {
        return await _dbContext.StudentProfiles
            .Include(profile => profile.Subjects)
            .Where(profile => profile.CreatedByTeacherUserId == teacherUserId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StudentProfile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.StudentProfiles
            .Include(profile => profile.Subjects)
            .Where(profile => ids.Contains(profile.Id))
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(StudentProfile profile, CancellationToken cancellationToken)
    {
        return _dbContext.StudentProfiles.AddAsync(profile, cancellationToken).AsTask();
    }

    public async Task ReplaceSubjectsAsync(Guid studentProfileId, IReadOnlyList<StudentSubject> newSubjects, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.StudentSubjects
            .Where(s => s.StudentProfileId == studentProfileId)
            .ToListAsync(cancellationToken);
        _dbContext.StudentSubjects.RemoveRange(existing);
        await _dbContext.StudentSubjects.AddRangeAsync(newSubjects, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
