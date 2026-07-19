namespace EgitimUssu.Shared.Contracts;

public sealed record ParentInviteInfo(Guid InviteId, Guid StudentId, string? ChildDisplayName);

// Students uygular; Parents tüketir. Veli, öğretmenin ürettiği kodu girerek çocuğuna bağlanır.
public interface IParentInviteDirectory
{
    Task<ParentInviteInfo?> ResolveAsync(string inviteCode, CancellationToken cancellationToken);
    Task MarkClaimedAsync(Guid inviteId, Guid parentUserId, CancellationToken cancellationToken);
}
