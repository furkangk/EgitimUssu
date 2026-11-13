using System.Globalization;
using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// M02-3/M02-4: Öğretmen arama/listeleme filtreleri, sayfalama sınırı ve pasifleştirme.
/// Filtreleme mantığı <see cref="TeacherSearchFilter"/> içinde saf fonksiyon olarak durur;
/// repository (EF) ile birim testi aynı kuralı paylaşır.
/// </summary>
public sealed class TeacherSearchTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherProfile Profile(
        string fullName = "Ahmet",
        string subject = "Matematik",
        string city = "Istanbul",
        string district = "Kadıköy",
        TeacherLessonFormat format = TeacherLessonFormat.Online,
        decimal rate = 400m,
        bool isVerified = false)
        => new(Guid.NewGuid(), Guid.NewGuid(), fullName, subject, city, district,
            null, null, format, 5, "Lisans", rate, "TRY", isVerified, null, Now);

    // --- M02-4: pasifleştirme ---

    [Fact]
    public void New_Profile_Should_Be_Active()
    {
        Assert.True(Profile().IsActive);
    }

    [Fact]
    public void Deactivate_Then_Activate_Should_Toggle_Flag_And_Timestamp()
    {
        var profile = Profile();

        profile.Deactivate(Now.AddDays(1));
        Assert.False(profile.IsActive);
        Assert.Equal(Now.AddDays(1), profile.UpdatedOnUtc);

        profile.Activate(Now.AddDays(2));
        Assert.True(profile.IsActive);
        Assert.Equal(Now.AddDays(2), profile.UpdatedOnUtc);
    }

    [Fact]
    public void Deactivate_Should_Be_Idempotent()
    {
        var profile = Profile();
        profile.Deactivate(Now.AddDays(1));

        profile.Deactivate(Now.AddDays(5));

        Assert.False(profile.IsActive);
        // Zaten pasifse zaman damgası kirlenmez.
        Assert.Equal(Now.AddDays(1), profile.UpdatedOnUtc);
    }

    // --- M02-3: filtreler ---

    /// <remarks>
    /// ⚠️ Bu test <b>LINQ-to-objects</b> üzerinde koşar: karşılaştırmanın sol tarafındaki
    /// <c>ToLower()</c> ambient kültürü kullanır (üretimde ise Postgres'in <c>lower()</c>'ı çalışır).
    /// Bu yüzden kültür sabitlenir ve veri ASCII tutulur; <b>Türkçe I/İ katlaması</b>
    /// <c>RealTeacherSearchIntegrationTests</c> ile gerçek Postgres'e karşı doğrulanır.
    /// </remarks>
    [Fact]
    public void Search_Should_Filter_By_City_And_Subject()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var match = Profile(city: "Istanbul", subject: "Matematik");
            var wrongCity = Profile(city: "Ankara", subject: "Matematik");
            var wrongSubject = Profile(city: "Istanbul", subject: "Fizik");

            var result = Apply(new[] { match, wrongCity, wrongSubject }, City: "Istanbul", Subject: "Matematik");

            Assert.Single(result);
            Assert.Equal(match.UserId, result[0].UserId);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// M02-3 regresyon: arama girdisi Postgres'in <c>lower()</c> davranışıyla aynı sonucu vermeli.
    /// Türkçe I varyantları (<c>'I'</c> ve <c>'İ'</c>) düz <c>'i'</c>'ye katlanır — aksi hâlde
    /// tr-TR kültüründe "Istanbul" araması boş dönüyordu (P06 Task 10 canlı doğrulaması).
    /// </summary>
    [Theory]
    [InlineData("Istanbul", "istanbul")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("ISTANBUL", "istanbul")]
    [InlineData(" İzmir ", "izmir")]
    [InlineData("Matematik", "matematik")]
    public void NormalizeForSearch_Should_Match_Postgres_Lower(string typed, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            Assert.Equal(expected, TeacherSearchFilter.NormalizeForSearch(typed));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Search_Should_Match_Subject_In_Secondary_Subjects()
    {
        var profile = Profile(subject: "Matematik");
        profile.MergeSubjects(new[] { new TeacherSubject(Guid.NewGuid(), profile.Id, "Geometri") });

        var result = Apply(new[] { profile }, Subject: "Geometri");

        Assert.Single(result);
    }

    [Fact]
    public void Search_Should_Filter_By_Rate_Range()
    {
        var cheap = Profile(rate: 200m);
        var mid = Profile(rate: 400m);
        var expensive = Profile(rate: 900m);

        var result = Apply(new[] { cheap, mid, expensive }, MinRate: 300m, MaxRate: 500m);

        Assert.Single(result);
        Assert.Equal(400m, result[0].HourlyRateAmount);
    }

    [Fact]
    public void Search_Should_Filter_By_Lesson_Format()
    {
        var online = Profile(format: TeacherLessonFormat.Online);
        var inPerson = Profile(format: TeacherLessonFormat.InPerson);

        var result = Apply(new[] { online, inPerson }, LessonFormat: TeacherLessonFormat.InPerson);

        Assert.Single(result);
        Assert.Equal(nameof(TeacherLessonFormat.InPerson), result[0].LessonFormat);
    }

    [Fact]
    public void Search_Should_Filter_Only_Verified()
    {
        var verified = Profile(isVerified: true);
        var notVerified = Profile(isVerified: false);

        var result = Apply(new[] { verified, notVerified }, OnlyVerified: true);

        Assert.Single(result);
        Assert.True(result[0].IsVerified);
    }

    [Fact]
    public void Search_Should_Exclude_Inactive_Profiles()
    {
        var active = Profile();
        var passive = Profile();
        passive.Deactivate(Now);

        var result = Apply(new[] { active, passive });

        Assert.Single(result);
        Assert.Equal(active.UserId, result[0].UserId);
    }

    // --- sayfalama ---

    [Fact]
    public async Task Take_Should_Be_Clamped_To_Fifty()
    {
        var profiles = Enumerable.Range(0, 60).Select(_ => Profile()).ToArray();
        var repository = new FakeSearchRepository(profiles);
        var handler = new SearchTeacherProfilesQueryHandler(repository);

        var result = await handler.Handle(
            new SearchTeacherProfilesQuery(null, null, null, null, null, null, null, Skip: 0, Take: 500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(SearchTeacherProfilesQueryHandler.MaxTake, result.Value.Items.Count);
        Assert.Equal(60, result.Value.TotalCount);
        Assert.Equal(SearchTeacherProfilesQueryHandler.MaxTake, repository.LastTake);
    }

    [Fact]
    public async Task Zero_Take_Should_Fall_Back_To_Default()
    {
        var repository = new FakeSearchRepository(Profile());
        var handler = new SearchTeacherProfilesQueryHandler(repository);

        await handler.Handle(
            new SearchTeacherProfilesQuery(null, null, null, null, null, null, null, Skip: -5, Take: 0),
            CancellationToken.None);

        Assert.Equal(20, repository.LastTake);
        // Negatif skip sıfıra çekilir (istemci hatası 500'e dönüşmesin).
        Assert.Equal(0, repository.LastSkip);
    }

    // --- yetki ---

    [Fact]
    public async Task Search_Should_Require_Authentication()
    {
        var authorizer = new TeacherSearchQueryAuthorizer(new FakeCurrentUser(null));

        var result = await authorizer.Authorize(
            new SearchTeacherProfilesQuery(null, null, null, null, null, null, null, 0, 20),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Deactivate_Should_Be_Forbidden_For_Other_Teacher()
    {
        var authorizer = new TeacherActivationCommandAuthorizer(
            new FakeCurrentUser(Guid.NewGuid().ToString(), "Teacher"));

        var result = await authorizer.Authorize(
            new SetTeacherActivationCommand(Guid.NewGuid(), IsActive: false), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Deactivate_Should_Be_Allowed_For_Owner()
    {
        var userId = Guid.NewGuid();
        var authorizer = new TeacherActivationCommandAuthorizer(
            new FakeCurrentUser(userId.ToString(), "Teacher"));

        var result = await authorizer.Authorize(
            new SetTeacherActivationCommand(userId, IsActive: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Activation_Handler_Should_Toggle_And_Save()
    {
        var profile = Profile();
        var repository = new FakeActivationRepository(profile);
        var handler = new SetTeacherActivationCommandHandler(repository, new FixedClock(Now));

        var result = await handler.Handle(
            new SetTeacherActivationCommand(profile.UserId, IsActive: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(profile.IsActive);
        Assert.Equal(1, repository.SaveCount);
    }

    private static IReadOnlyList<TeacherSummaryResponse> Apply(
        IEnumerable<TeacherProfile> profiles,
        string? City = null,
        string? District = null,
        string? Subject = null,
        TeacherLessonFormat? LessonFormat = null,
        decimal? MinRate = null,
        decimal? MaxRate = null,
        bool? OnlyVerified = null)
    {
        var query = new SearchTeacherProfilesQuery(City, District, Subject, LessonFormat, MinRate, MaxRate, OnlyVerified, 0, 50);
        return TeacherSearchFilter.Apply(profiles.AsQueryable(), query)
            .ToList()
            .Select(TeacherSearchFilter.ToSummary)
            .ToList();
    }

    private sealed class FakeSearchRepository : ITeacherProfileSearchRepository
    {
        private readonly List<TeacherProfile> _profiles;

        public FakeSearchRepository(params TeacherProfile[] profiles) => _profiles = [.. profiles];

        public int LastSkip { get; private set; }

        public int LastTake { get; private set; }

        public Task<(IReadOnlyList<TeacherProfile> Items, int TotalCount)> SearchAsync(
            SearchTeacherProfilesQuery query, int skip, int take, CancellationToken cancellationToken)
        {
            LastSkip = skip;
            LastTake = take;
            var filtered = TeacherSearchFilter.Apply(_profiles.AsQueryable(), query).ToList();
            return Task.FromResult<(IReadOnlyList<TeacherProfile>, int)>(
                (filtered.Skip(skip).Take(take).ToList(), filtered.Count));
        }
    }

    private sealed class FakeActivationRepository : ITeacherProfileRepository
    {
        private readonly List<TeacherProfile> _profiles;

        public FakeActivationRepository(params TeacherProfile[] profiles) => _profiles = [.. profiles];

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
