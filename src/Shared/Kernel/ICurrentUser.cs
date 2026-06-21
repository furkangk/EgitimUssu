namespace EgitimUssu.Shared.Kernel;

public interface ICurrentUser
{
    string? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }
}
