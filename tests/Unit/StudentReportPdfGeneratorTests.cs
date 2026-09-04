using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;
using EgitimUssu.Tests.Unit.TestDoubles;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P08 Task 4 / M14-4 — PDF öğrenci raporu.
/// <para>
/// Üç katman ayrı ayrı test edilir: (a) <see cref="StudentReportComposer"/> saf biçimlendirme,
/// (b) <see cref="StudentReportPdfGenerator"/> gerçek QuestPDF çıktısı (bayt düzeyi doğrulama),
/// (c) komut/sorgu handler'ları — premium kapısı, gizlilik kapısı, depolama sırası ve yetkili indirme.
/// </para>
/// <para>
/// PDF metnini okumak için ayrı bir okuyucu bağımlılığı (PdfPig) eklenmedi; metin doğrulaması
/// composer üzerinden yapılır, bayt katmanında yalnız <c>%PDF-</c> başlığı ve anlamlı boyut aranır.
/// </para>
/// </summary>
public sealed class StudentReportPdfGeneratorTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 6, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StudentId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid StudentUserId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid TeacherUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ParentUserId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid ReportId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

    private static StudentReportData SampleData(IReadOnlyList<SubjectPerformanceRow>? subjects = null) => new(
        "Ayşe Yılmaz",
        "Mehmet Öğretmen",
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 31),
        StudyMinutes: 750,
        LessonCount: 8,
        AssignmentCompletionPercent: 80,
        subjects ?? [new SubjectPerformanceRow("Matematik", 72, 0.65m, 4, 5)],
        "Düzenli çalışıyor.");

    // --- (a) Saf biçimlendirme -----------------------------------------------------------------

    [Fact]
    public void Duration_Should_Be_Formatted_As_Hours_And_Minutes()
    {
        Assert.Equal("12 sa 30 dk", StudentReportComposer.FormatDuration(750));
        Assert.Equal("45 dk", StudentReportComposer.FormatDuration(45));
        Assert.Equal("0 dk", StudentReportComposer.FormatDuration(0));
    }

    [Fact]
    public void Completion_Percent_Should_Round_And_Be_Zero_Without_Assignments()
    {
        Assert.Equal(0, StudentReportComposer.CompletionPercent(0, 0));
        Assert.Equal(67, StudentReportComposer.CompletionPercent(3, 2));
        Assert.Equal(100, StudentReportComposer.CompletionPercent(4, 4));
    }

    [Fact]
    public void Header_Lines_Should_Contain_Student_Teacher_And_Period()
    {
        var lines = StudentReportComposer.HeaderLines(SampleData());

        Assert.Contains(lines, line => line.Contains("Ayşe Yılmaz", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Mehmet Öğretmen", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("01.08.2026", StringComparison.Ordinal)
            && line.Contains("31.08.2026", StringComparison.Ordinal));
    }

    [Fact]
    public void Summary_Lines_Should_Carry_Study_Lesson_And_Assignment_Totals()
    {
        var lines = StudentReportComposer.SummaryLines(SampleData());

        Assert.Contains(lines, line => line.Value.Contains("12 sa 30 dk", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Value.Contains("8", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Value.Contains("%80", StringComparison.Ordinal));
    }

    [Fact]
    public void Net_Ratio_Should_Be_Rendered_As_Percent()
    {
        Assert.Equal("%65", StudentReportComposer.FormatNet(0.65m));
        Assert.Equal("%0", StudentReportComposer.FormatNet(0m));
    }

    [Fact]
    public void File_Name_Should_Contain_Period_Bounds()
    {
        var name = StudentReportComposer.FileName(SampleData());

        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
        Assert.Contains("2026-08-01", name, StringComparison.Ordinal);
        Assert.Contains("2026-08-31", name, StringComparison.Ordinal);
    }

    // --- Premium kapısı (K-09: istek sahibinin hakkı) ------------------------------------------

    [Fact]
    public async Task Admin_Should_Always_Pass_The_Pdf_Gate()
    {
        var resolver = new ReportEntitlementResolver(
            new FakeCurrentUser(TeacherUserId, "Admin"),
            new FakeEntitlementDirectory(MembershipTier.Free));

        Assert.True(await resolver.AllowsAsync(PremiumFeature.PdfReport, CancellationToken.None));
    }

    [Fact]
    public async Task Premium_Requester_Should_Pass_The_Pdf_Gate()
    {
        var resolver = new ReportEntitlementResolver(
            new FakeCurrentUser(TeacherUserId, "Teacher"),
            new FakeEntitlementDirectory(MembershipTier.Premium));

        Assert.True(await resolver.AllowsAsync(PremiumFeature.PdfReport, CancellationToken.None));
    }

    [Fact]
    public async Task Free_Requester_Should_Not_Pass_The_Pdf_Gate()
    {
        // P08'in geçici kuralı ("taraflardan biri Premium ise geçer") K-09 ile kalktı:
        // öğretmen üyeliği artık gerçekten var, kapı istek sahibinin hakkına bakar (Öğrenilenler #113).
        var resolver = new ReportEntitlementResolver(
            new FakeCurrentUser(TeacherUserId, "Teacher"),
            new FakeEntitlementDirectory(MembershipTier.Free));

        Assert.False(await resolver.AllowsAsync(PremiumFeature.PdfReport, CancellationToken.None));
    }

    // --- (b) Gerçek PDF üretimi ----------------------------------------------------------------

    [Fact]
    public void Generated_Pdf_Should_Start_With_Pdf_Header_And_Have_Content()
    {
        var bytes = new StudentReportPdfGenerator().Generate(SampleData());

        Assert.True(bytes.Length > 1024, $"PDF beklenenden küçük: {bytes.Length} bayt");
        Assert.Equal("%PDF-"u8.ToArray(), bytes.Take(5).ToArray());
    }

    [Fact]
    public void Generated_Pdf_Should_Not_Throw_Without_Subject_Rows()
    {
        var bytes = new StudentReportPdfGenerator().Generate(SampleData(subjects: []) with { Note = null });

        Assert.True(bytes.Length > 1024);
    }

    // --- (c) Komut handler'ı -------------------------------------------------------------------

    [Fact]
    public async Task Generate_Should_Store_File_And_Persist_Report_Row()
    {
        var storage = new FakeFileStorage();
        var repository = new FakeStudentReportRepository();
        var handler = CreateHandler(storage, repository);

        var result = await handler.Handle(
            new GenerateStudentReportCommand(StudentId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "Not"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(ReportId, result.Value!.ReportId);
        Assert.Single(storage.Saved);
        Assert.StartsWith($"reporting/{StudentId}/", storage.Saved[0].Key, StringComparison.Ordinal);
        var stored = Assert.Single(repository.Added);
        Assert.Equal(StudentId, stored.StudentId);
        Assert.Equal(storage.Saved[0].Key, stored.StorageKey);
        Assert.Equal(TeacherUserId, stored.CreatedByUserId);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Generate_Should_Fail_With_Premium_Required_When_Both_Sides_Are_Free()
    {
        var storage = new FakeFileStorage();
        var repository = new FakeStudentReportRepository();
        var handler = CreateHandler(storage, repository, requesterTier: MembershipTier.Free);

        var result = await handler.Handle(
            new GenerateStudentReportCommand(StudentId, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("reporting.premium_required", result.Error.Code);
        // Kapı reddettiyse depoya hiçbir şey yazılmaz (Assignments teslim deseniyle aynı sıra).
        Assert.Empty(storage.Saved);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Generate_Should_Fail_When_Student_Hides_Study_Data()
    {
        var storage = new FakeFileStorage();
        var repository = new FakeStudentReportRepository();
        var handler = CreateHandler(
            storage, repository, privacy: new StudentPrivacy(true, false, StudyPrivacyLevel.Standard));

        var result = await handler.Handle(
            new GenerateStudentReportCommand(StudentId, null, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("reporting.report_not_shared", result.Error.Code);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Generate_Should_Hide_Subject_Rows_When_Visibility_Is_Limited()
    {
        var generator = new RecordingPdfGenerator();
        var handler = CreateHandler(
            new FakeFileStorage(),
            new FakeStudentReportRepository(),
            privacy: new StudentPrivacy(true, true, StudyPrivacyLevel.Limited),
            generator: generator);

        var result = await handler.Handle(
            new GenerateStudentReportCommand(StudentId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Empty(generator.Last!.Subjects);
        // Toplam süre Limited'da paylaşılır (M15-4): kırılım gizlenir, toplam değil.
        Assert.Equal(120, generator.Last.StudyMinutes);
    }

    [Fact]
    public async Task Generate_Should_Reject_Inverted_Range()
    {
        var handler = CreateHandler(new FakeFileStorage(), new FakeStudentReportRepository());

        var result = await handler.Handle(
            new GenerateStudentReportCommand(StudentId, new DateOnly(2026, 8, 31), new DateOnly(2026, 8, 1), null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("reporting.invalid_range", result.Error.Code);
    }

    [Fact]
    public async Task Generate_Should_Fill_Report_From_Projections_And_Directories()
    {
        var generator = new RecordingPdfGenerator();
        await CreateHandler(new FakeFileStorage(), new FakeStudentReportRepository(), generator: generator)
            .Handle(
                new GenerateStudentReportCommand(StudentId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "Not"),
                CancellationToken.None);

        var data = generator.Last!;
        Assert.Equal("Ayşe Yılmaz", data.StudentName);
        Assert.Equal("Mehmet Öğretmen", data.TeacherName);
        Assert.Equal(120, data.StudyMinutes);
        Assert.Equal(3, data.LessonCount);
        Assert.Equal(50, data.AssignmentCompletionPercent);
        Assert.Equal("Matematik", Assert.Single(data.Subjects).Subject);
        Assert.Equal("Not", data.Note);
    }

    // --- (c) İndirme sorgusu + yetki -----------------------------------------------------------

    [Fact]
    public async Task Report_File_Query_Should_Return_Storage_Key()
    {
        var repository = new FakeStudentReportRepository();
        repository.Seed(NewReport());
        var handler = new GetStudentReportFileQueryHandler(repository);

        var result = await handler.Handle(new GetStudentReportFileQuery(ReportId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("reporting/key.pdf", result.Value!.StorageKey);
        Assert.Equal(StudentId, result.Value.StudentId);
    }

    [Fact]
    public async Task Report_File_Query_Should_Return_Not_Found_For_Unknown_Report()
    {
        var handler = new GetStudentReportFileQueryHandler(new FakeStudentReportRepository());

        var result = await handler.Handle(new GetStudentReportFileQuery(ReportId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("reporting.report_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Unlinked_Teacher_Should_Not_Download_Another_Students_Report()
    {
        var repository = new FakeStudentReportRepository();
        repository.Seed(NewReport());
        var authorizer = new StudentReportFileAuthorizer(
            repository,
            new StudentReportAccessChecker(
                new FakeCurrentUser(Guid.NewGuid(), "Teacher"),
                new FakeStudentDirectory(),
                new FakeParentAccessDirectory()));

        var result = await authorizer.Authorize(new GetStudentReportFileQuery(ReportId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("shared.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Linked_Teacher_Should_Download_Report()
    {
        var repository = new FakeStudentReportRepository();
        repository.Seed(NewReport());
        var authorizer = new StudentReportFileAuthorizer(
            repository,
            new StudentReportAccessChecker(
                new FakeCurrentUser(TeacherUserId, "Teacher"),
                new FakeStudentDirectory(),
                new FakeParentAccessDirectory()));

        var result = await authorizer.Authorize(new GetStudentReportFileQuery(ReportId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Approved_Parent_Should_Download_Report()
    {
        var repository = new FakeStudentReportRepository();
        repository.Seed(NewReport());
        var authorizer = new StudentReportFileAuthorizer(
            repository,
            new StudentReportAccessChecker(
                new FakeCurrentUser(ParentUserId, "Parent"),
                new FakeStudentDirectory(),
                new FakeParentAccessDirectory()));

        var result = await authorizer.Authorize(new GetStudentReportFileQuery(ReportId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- Yardımcılar ---------------------------------------------------------------------------

    private static StudentReport NewReport() => new(
        ReportId, StudentId, TeacherUserId,
        new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
        "reporting/key.pdf", "gelisim-raporu.pdf", "application/pdf", 2048, Now);

    private static GenerateStudentReportCommandHandler CreateHandler(
        FakeFileStorage storage,
        FakeStudentReportRepository repository,
        MembershipTier requesterTier = MembershipTier.Premium,
        StudentPrivacy? privacy = null,
        IStudentReportPdfGenerator? generator = null)
    {
        var currentUser = new FakeCurrentUser(TeacherUserId, "Teacher");
        var students = new FakeStudentDirectory();
        var privacyDirectory = new FakePrivacyDirectory(
            privacy ?? new StudentPrivacy(true, true, StudyPrivacyLevel.Standard));

        return new GenerateStudentReportCommandHandler(
            new StudentReportPrivacyGate(currentUser, students, privacyDirectory),
            new ReportEntitlementResolver(currentUser, new FakeEntitlementDirectory(requesterTier)),
            new StudentReportAssembler(
                new FakeProjectionReader(),
                students,
                new FakeTeacherDirectory(),
                new FakeAssignmentDigestDirectory(),
                currentUser),
            generator ?? new RecordingPdfGenerator(),
            storage,
            repository,
            currentUser,
            new FixedClock(Now),
            new FixedIdGenerator(ReportId));
    }

    private sealed class RecordingPdfGenerator : IStudentReportPdfGenerator
    {
        public StudentReportData? Last { get; private set; }

        public byte[] Generate(StudentReportData data)
        {
            Last = data;
            return "%PDF-fake"u8.ToArray();
        }
    }

    private sealed class FakeFileStorage : IFileStorage
    {
        public List<StoredFile> Saved { get; } = [];

        public Task<StoredFile> SaveAsync(
            string keyPrefix, string fileName, string contentType, Stream content, CancellationToken cancellationToken)
        {
            var stored = new StoredFile($"{keyPrefix}/{Guid.NewGuid():N}.pdf", fileName, contentType, content.Length);
            Saved.Add(stored);
            return Task.FromResult(stored);
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult<Stream?>(new MemoryStream());

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeStudentReportRepository : IStudentReportRepository
    {
        private readonly Dictionary<Guid, StudentReport> _rows = [];

        public List<StudentReport> Added { get; } = [];

        public bool Saved { get; private set; }

        public void Seed(StudentReport report) => _rows[report.Id] = report;

        public Task AddAsync(StudentReport report, CancellationToken cancellationToken)
        {
            Added.Add(report);
            _rows[report.Id] = report;
            return Task.CompletedTask;
        }

        public Task<StudentReport?> GetByIdAsync(Guid reportId, CancellationToken cancellationToken)
            => Task.FromResult(_rows.GetValueOrDefault(reportId));

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectionReader : IReportingProjectionReader
    {
        public Task<TeacherMonthlyProjection?> GetTeacherMonthlyAsync(
            Guid teacherUserId, int year, int month, CancellationToken cancellationToken)
            => Task.FromResult<TeacherMonthlyProjection?>(null);

        public Task<IReadOnlyList<StudentStudyProjection>> ListStudentStudyAsync(
            Guid studentId, (int Year, int Week)? from, (int Year, int Week)? to, CancellationToken cancellationToken)
        {
            var week = new StudentStudyProjection(studentId, 2026, 32);
            week.TrackSession(Guid.NewGuid(), 120);
            return Task.FromResult<IReadOnlyList<StudentStudyProjection>>([week]);
        }

        public Task<IReadOnlyList<StudentPerformanceProjection>> ListStudentPerformanceAsync(
            Guid studentId, string? subject, (int Year, int Month)? from, (int Year, int Month)? to, CancellationToken cancellationToken)
        {
            var row = new StudentPerformanceProjection(studentId, "Matematik", 2026, 8);
            row.TrackTestNet(Guid.NewGuid(), 0.65m);
            row.TrackTopicMastery("Türev", 72m);
            return Task.FromResult<IReadOnlyList<StudentPerformanceProjection>>([row]);
        }

        public Task<int> CountStudentCompletedLessonsAsync(
            Guid studentId, (int Year, int Month) from, (int Year, int Month) to, CancellationToken cancellationToken)
            => Task.FromResult(3);
    }

    private sealed class FakeStudentDirectory : IStudentDirectory
    {
        public Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<Guid?>(StudentUserId);

        public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(
            Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<AgreedLessonRate?>(null);

        public Task<bool> IsLinkedTeacherOfStudentAsync(
            Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(teacherUserId == TeacherUserId && studentId == StudentId);

        public Task<string?> GetDisplayNameAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<string?>("Ayşe Yılmaz");
    }

    private sealed class FakeTeacherDirectory : ITeacherDirectory
    {
        public Task<string?> GetDisplayNameAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult<string?>(teacherUserId == TeacherUserId ? "Mehmet Öğretmen" : null);
    }

    private sealed class FakeAssignmentDigestDirectory : IStudentAssignmentDigestDirectory
    {
        public Task<AssignmentCompletionSummary> GetCompletionAsync(
            Guid studentUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
            => Task.FromResult(new AssignmentCompletionSummary(4, 2));
    }

    private sealed class FakeParentAccessDirectory : IParentAccessDirectory
    {
        public Task<bool> IsApprovedParentOfStudentAsync(
            Guid parentUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(parentUserId == ParentUserId && studentId == StudentId);

        public Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(
            Guid parentUserId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guid>>([StudentId]);
    }

    private sealed class FakePrivacyDirectory(StudentPrivacy privacy) : IStudentPrivacyDirectory
    {
        public Task<StudentPrivacy> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(privacy);
    }

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();

        public string? Email => "test@example.com";

        public IReadOnlyCollection<string> Roles { get; } = roles;

        public bool IsAuthenticated => true;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedIdGenerator(Guid id) : IIdGenerator
    {
        public Guid New() => id;
    }
}
