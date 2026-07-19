namespace EgitimUssu.Shared.Contracts;

public sealed record ParentNotificationPrefs(
    bool MissedAssignment, bool WeeklyProgressSummary, bool LessonReminders, bool TestResults, bool Payments);

public sealed record ParentNotificationTarget(Guid ParentUserId, MembershipTier Tier, ParentNotificationPrefs Prefs);

public interface IParentNotificationDirectory
{
    // Bir öğrencinin ONAYLI velileri + üyelik + tercihleri. Notifications teslim kararı için okur.
    Task<IReadOnlyCollection<ParentNotificationTarget>> GetApprovedParentsForStudentAsync(Guid studentId, CancellationToken cancellationToken);

    // Haftalık özet için: tüm onaylı (parent,student) çiftleri (üyelik + tercih ile).
    Task<IReadOnlyCollection<ParentStudentNotificationTarget>> ListAllApprovedTargetsAsync(CancellationToken cancellationToken);
}

// Haftalık özet taraması için öğrenci + veli hedefi çifti.
public sealed record ParentStudentNotificationTarget(Guid StudentId, ParentNotificationTarget Target);
