using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit.TestDoubles;

/// <summary>
/// P15 Task 4: Veli↔öğrenci onaylı bağı için sahte dizin. Öğrenci profili okuma yetkisi artık profildeki
/// <c>ParentUserId</c>'ye değil bu sözleşmeye bakar; testler onaylı çiftleri <see cref="Approved"/>'a yazar.
/// </summary>
public sealed class FakeParentAccessDirectory : IParentAccessDirectory
{
    /// <summary>Onaylı (veli kullanıcısı, öğrenci profili) çiftleri.</summary>
    public HashSet<(Guid ParentUserId, Guid StudentId)> Approved { get; } = [];

    /// <summary><see cref="IsApprovedParentOfStudentAsync"/> kaç kez çağrıldı (ucuz kontrol yolu ölçümü).</summary>
    public int IsApprovedCallCount { get; private set; }

    /// <inheritdoc />
    public Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken)
    {
        IsApprovedCallCount++;
        return Task.FromResult(Approved.Contains((parentUserId, studentId)));
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(Guid parentUserId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>>(
            Approved.Where(pair => pair.ParentUserId == parentUserId).Select(pair => pair.StudentId).ToArray());
}
