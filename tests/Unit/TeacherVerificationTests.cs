using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// M02-1: Öğretmen doğrulama rozetini **yalnız Admin** verebilir; öğretmen kendini doğrulayamaz.
/// </summary>
public sealed class TeacherVerificationTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherProfile NewProfile(Guid? userId = null)
        => new(Guid.NewGuid(), userId ?? Guid.NewGuid(), "Ahmet", "Matematik", "İstanbul", "Kadıköy",
            null, null, TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", false, null, Now);

    [Fact]
    public void SetVerification_Should_Raise_Event_And_Update_Flag()
    {
        var profile = NewProfile();
        profile.ClearDomainEvents();

        profile.SetVerification(true, Now);

        Assert.True(profile.IsVerified);
        Assert.Equal(Now, profile.UpdatedOnUtc);
        var raised = Assert.Single(profile.DomainEvents.OfType<TeacherVerifiedDomainEvent>());
        Assert.True(raised.IsVerified);
        Assert.Equal(profile.UserId, raised.UserId);
    }

    [Fact]
    public void SetVerification_Should_Not_Raise_Event_When_Value_Unchanged()
    {
        var profile = NewProfile();
        profile.SetVerification(true, Now);
        profile.ClearDomainEvents();

        profile.SetVerification(true, Now.AddDays(1));

        Assert.Empty(profile.DomainEvents.OfType<TeacherVerifiedDomainEvent>());
        // Değişiklik yoksa zaman damgası da kirlenmez.
        Assert.Equal(Now, profile.UpdatedOnUtc);
    }

    [Fact]
    public void SetVerification_Should_Allow_Revoking()
    {
        var profile = NewProfile();
        profile.SetVerification(true, Now);
        profile.ClearDomainEvents();

        profile.SetVerification(false, Now.AddDays(1));

        Assert.False(profile.IsVerified);
        var raised = Assert.Single(profile.DomainEvents.OfType<TeacherVerifiedDomainEvent>());
        Assert.False(raised.IsVerified);
    }

    [Fact]
    public async Task Non_Admin_Should_Be_Forbidden()
    {
        var authorizer = new SetTeacherVerificationCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid().ToString(), "Teacher"));

        var result = await authorizer.Authorize(
            new SetTeacherVerificationCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Teacher_Should_Not_Verify_Self()
    {
        var userId = Guid.NewGuid();
        var authorizer = new SetTeacherVerificationCommandAuthorizer(
            new FakeCurrentUser(userId.ToString(), "Teacher"));

        var result = await authorizer.Authorize(
            new SetTeacherVerificationCommand(userId, true), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Admin_Should_Be_Allowed()
    {
        var authorizer = new SetTeacherVerificationCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid().ToString(), "Admin"));

        var result = await authorizer.Authorize(
            new SetTeacherVerificationCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handler_Should_Update_Profile_And_Return_Response()
    {
        var profile = NewProfile();
        var repository = new FakeTeacherProfileRepository(profile);
        var handler = new SetTeacherVerificationCommandHandler(repository, new FixedClock(Now));

        var result = await handler.Handle(
            new SetTeacherVerificationCommand(profile.UserId, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsVerified);
        Assert.True(profile.IsVerified);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Handler_Should_Fail_When_Profile_Missing()
    {
        var repository = new FakeTeacherProfileRepository();
        var handler = new SetTeacherVerificationCommandHandler(repository, new FixedClock(Now));

        var result = await handler.Handle(
            new SetTeacherVerificationCommand(Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("teachers.profile_not_found", result.Error.Code);
    }

    private sealed class FakeTeacherProfileRepository : ITeacherProfileRepository
    {
        private readonly List<TeacherProfile> _profiles;

        public FakeTeacherProfileRepository(params TeacherProfile[] profiles) => _profiles = [.. profiles];

        public int SaveCount { get; private set; }

        public Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(_profiles.FirstOrDefault(p => p.UserId == userId));

        public Task AddAsync(TeacherProfile profile, CancellationToken cancellationToken)
        {
            _profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(string? userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => UserId is not null;
    }
}
