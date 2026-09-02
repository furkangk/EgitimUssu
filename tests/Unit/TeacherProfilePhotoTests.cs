using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04 Task 3 (M02-2/D-05): Öğretmen profil fotoğrafı yükleme.
/// Kritik davranışlar: yalnız profil sahibi (veya Admin) yükler, sınır/tür sunucuda doğrulanır,
/// eski fotoğraf silinir, yanıt **anahtar değil** indirme rotası taşır.
/// </summary>
public sealed class TeacherProfilePhotoTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SetProfilePhoto_Should_Return_Previous_Key()
    {
        var profile = CreateProfile(Guid.NewGuid());

        Assert.Null(profile.SetProfilePhoto("teachers/1/a.png", Now));
        Assert.Equal("teachers/1/a.png", profile.ProfilePhotoKey);

        var previous = profile.SetProfilePhoto("teachers/1/b.png", Now.AddMinutes(1));

        Assert.Equal("teachers/1/a.png", previous);
        Assert.Equal("teachers/1/b.png", profile.ProfilePhotoKey);
        Assert.Equal(Now.AddMinutes(1), profile.UpdatedOnUtc);
    }

    [Fact]
    public async Task Upload_Should_Reject_Unsupported_Type()
    {
        var userId = Guid.NewGuid();
        var storage = new RecordingFileStorage();
        var handler = CreateHandler(userId, storage);

        var result = await handler.Handle(Command(userId, "virus.exe", 16), default);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.unsupported_type", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Upload_Should_Reject_Oversized_File()
    {
        var userId = Guid.NewGuid();
        var storage = new RecordingFileStorage();
        var handler = CreateHandler(userId, storage);

        var result = await handler.Handle(Command(userId, "foto.png", FileValidation.MaxSizeBytes + 1), default);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.file_too_large", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Upload_Should_Store_Under_Teacher_Key_Prefix()
    {
        var userId = Guid.NewGuid();
        var storage = new RecordingFileStorage();
        var handler = CreateHandler(userId, storage);

        var result = await handler.Handle(Command(userId, "foto.png", 16), default);

        Assert.True(result.IsSuccess);
        Assert.Equal($"teachers/{userId}", storage.Saved.Single().KeyPrefix);
        Assert.Equal($"/api/teachers/profiles/{userId}/photo", result.Value!.PhotoUrl);
    }

    [Fact]
    public async Task Upload_Should_Delete_Previous_Photo()
    {
        var userId = Guid.NewGuid();
        var storage = new RecordingFileStorage();
        var repository = new FakeTeacherProfileRepository(CreateProfile(userId));
        var handler = new UploadTeacherPhotoCommandHandler(repository, storage, new FixedClock(Now));

        await handler.Handle(Command(userId, "ilk.png", 16), default);
        var firstKey = repository.Profile.ProfilePhotoKey;
        await handler.Handle(Command(userId, "ikinci.png", 16), default);

        Assert.Equal(new[] { firstKey }, storage.Deleted);
    }

    [Fact]
    public async Task Upload_To_Unknown_Profile_Should_Not_Touch_Storage()
    {
        var storage = new RecordingFileStorage();
        var handler = CreateHandler(Guid.NewGuid(), storage);

        var result = await handler.Handle(Command(Guid.NewGuid(), "foto.png", 16), default);

        Assert.True(result.IsFailure);
        Assert.Equal("teachers.profile_not_found", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Theory]
    [InlineData(true, false, true)]   // profil sahibi öğretmen
    [InlineData(false, true, true)]   // Admin
    [InlineData(false, false, false)] // başka öğretmen
    public async Task Authorizer_Should_Allow_Only_Owner_Or_Admin(bool isSelf, bool isAdmin, bool expected)
    {
        var userId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(
            isSelf ? userId : Guid.NewGuid(),
            isAdmin ? ["Admin"] : ["Teacher"]);
        var authorizer = new TeacherProfileCommandAuthorizer(currentUser);

        var result = await authorizer.Authorize(Command(userId, "foto.png", 16), default);

        Assert.Equal(expected, result.IsSuccess);
    }

    [Fact]
    public async Task Anonymous_Should_Be_Denied()
    {
        var authorizer = new TeacherProfileCommandAuthorizer(new FakeCurrentUser(null, []));

        var result = await authorizer.Authorize(Command(Guid.NewGuid(), "foto.png", 16), default);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Photo_Query_Should_Fail_When_No_Photo()
    {
        var userId = Guid.NewGuid();
        var handler = new GetTeacherPhotoQueryHandler(new FakeTeacherProfileRepository(CreateProfile(userId)));

        var result = await handler.Handle(new GetTeacherPhotoQuery(userId), default);

        Assert.True(result.IsFailure);
        Assert.Equal("teachers.photo_not_found", result.Error.Code);
    }

    private static UploadTeacherPhotoCommand Command(Guid userId, string fileName, long sizeBytes)
        => new(
            userId,
            fileName,
            fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "application/octet-stream",
            sizeBytes,
            new MemoryStream(new byte[Math.Min(sizeBytes, 64)]));

    private static UploadTeacherPhotoCommandHandler CreateHandler(Guid profileUserId, IFileStorage storage)
        => new(new FakeTeacherProfileRepository(CreateProfile(profileUserId)), storage, new FixedClock(Now));

    private static TeacherProfile CreateProfile(Guid userId)
        => new(
            Guid.NewGuid(),
            userId,
            "Ayse Yilmaz",
            "Matematik",
            "Istanbul",
            "Kadikoy",
            null,
            null,
            TeacherLessonFormat.Online,
            5,
            "Lisans",
            500m,
            "TRY",
            false,
            null,
            Now.AddDays(-1));

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUser(Guid? userId, string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId?.ToString();

        public string? Email => null;

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsAuthenticated { get; } = userId.HasValue;
    }

    private sealed class RecordingFileStorage : IFileStorage
    {
        public List<(string KeyPrefix, string FileName)> Saved { get; } = [];

        public List<string?> Deleted { get; } = [];

        public Task<StoredFile> SaveAsync(
            string keyPrefix,
            string fileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            Saved.Add((keyPrefix, fileName));
            return Task.FromResult(
                new StoredFile($"{keyPrefix}/{Saved.Count}{Path.GetExtension(fileName)}", fileName, contentType, content.Length));
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Deleted.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTeacherProfileRepository(TeacherProfile profile) : ITeacherProfileRepository
    {
        public TeacherProfile Profile { get; } = profile;

        public Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(Profile.UserId == userId ? Profile : null);

        public Task AddAsync(TeacherProfile newProfile, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
