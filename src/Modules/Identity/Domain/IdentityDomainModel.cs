using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Domain;

public sealed class UserAccount : AggregateRoot<Guid>
{
    private UserAccount()
    {
    }

    public UserAccount(
        Guid id,
        string email,
        string normalizedEmail,
        string passwordHash,
        string firstName,
        string lastName,
        string? phoneNumber,
        UserAccountStatus status,
        bool isEmailConfirmed,
        bool isProfileVerified,
        DateTime createdOnUtc)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Status = status;
        IsEmailConfirmed = isEmailConfirmed;
        IsProfileVerified = isProfileVerified;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new UserRegisteredDomainEvent(Id, Email, createdOnUtc));
    }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public UserAccountStatus Status { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    public bool IsProfileVerified { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public List<UserRoleMembership> RoleMemberships { get; private set; } = [];

    public List<RefreshTokenSession> RefreshSessions { get; private set; } = [];

    public List<UserSecurityToken> SecurityTokens { get; private set; } = [];

    public void ConfirmEmail(DateTime now)
    {
        IsEmailConfirmed = true;
        UpdatedOnUtc = now;
    }

    public void UpdatePassword(string passwordHash, DateTime now)
    {
        PasswordHash = passwordHash;
        UpdatedOnUtc = now;
    }
}

public sealed class UserRoleMembership : Entity<Guid>
{
    private UserRoleMembership()
    {
    }

    public UserRoleMembership(Guid id, Guid userAccountId, UserRole role, DateTime assignedOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        Role = role;
        AssignedOnUtc = assignedOnUtc;
    }

    public Guid UserAccountId { get; private set; }

    public UserRole Role { get; private set; }

    public DateTime AssignedOnUtc { get; private set; }
}

public sealed class RefreshTokenSession : Entity<Guid>
{
    private RefreshTokenSession()
    {
    }

    public RefreshTokenSession(
        Guid id,
        Guid userAccountId,
        string refreshTokenHash,
        string? deviceName,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        RefreshTokenHash = refreshTokenHash;
        DeviceName = deviceName;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid UserAccountId { get; private set; }

    public string RefreshTokenHash { get; private set; } = string.Empty;

    public string? DeviceName { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? RevokedOnUtc { get; private set; }

    public bool IsActive(DateTime now) => RevokedOnUtc is null && ExpiresOnUtc > now;

    public void Revoke(DateTime revokedOnUtc)
    {
        if (RevokedOnUtc is null)
        {
            RevokedOnUtc = revokedOnUtc;
        }
    }
}

public sealed class UserSecurityToken : Entity<Guid>
{
    private UserSecurityToken()
    {
    }

    public UserSecurityToken(
        Guid id,
        Guid userAccountId,
        SecurityTokenPurpose purpose,
        string tokenHash,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        Purpose = purpose;
        TokenHash = tokenHash;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid UserAccountId { get; private set; }

    public SecurityTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? UsedOnUtc { get; private set; }

    public bool IsUsable(DateTime now) => UsedOnUtc is null && ExpiresOnUtc > now;

    public void MarkUsed(DateTime usedOnUtc)
    {
        if (UsedOnUtc is null)
        {
            UsedOnUtc = usedOnUtc;
        }
    }
}

public enum SecurityTokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2
}

public enum UserAccountStatus
{
    PendingActivation = 1,
    Active = 2,
    Suspended = 3,
    Closed = 4
}

public enum UserRole
{
    Admin = 1,
    Teacher = 2,
    Student = 3,
    Parent = 4
}

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email, DateTime RegisteredOnUtc) : DomainEvent;
