using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Teachers.Infrastructure;

/// <summary>
/// M14-5: Teachers, öğretmenin haftalık uygunluk şablonunun otoritesidir. Reporting (boş zaman analizi)
/// bu sözleşmeyle okur — Teachers'ın DbContext'ine doğrudan erişmeden (anti-corruption).
/// Uygunluk dilimleri profile bağlıdır; sorgu öğretmen <b>kullanıcı</b> kimliğinden profile iner.
/// </summary>
internal sealed class TeacherAvailabilityDirectory : ITeacherAvailabilityDirectory
{
    private readonly TeachersDbContext _dbContext;

    public TeacherAvailabilityDirectory(TeachersDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<TeacherWeeklySlot>> ListWeeklySlotsAsync(
        Guid teacherUserId, CancellationToken cancellationToken)
        => await _dbContext.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == teacherUserId)
            .SelectMany(profile => profile.AvailabilitySlots)
            .Select(slot => new TeacherWeeklySlot(slot.DayOfWeek, slot.StartTime, slot.EndTime))
            .ToArrayAsync(cancellationToken);
}
