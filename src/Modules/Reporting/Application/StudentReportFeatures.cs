using System.Globalization;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Modules.Reporting.Application;

// ---- Rapor verisi ve üretici sözleşmesi ----

/// <summary>PDF raporunda tek ders satırı (delta bir önceki aya göre hâkimiyet farkıdır).</summary>
/// <param name="Subject">Ders adı.</param>
/// <param name="MasteryScore">Hâkimiyet skoru (0–100).</param>
/// <param name="AverageNet">Ortalama net oranı (0–1).</param>
/// <param name="TestCount">Dönemdeki test sayısı.</param>
/// <param name="Delta">Bir önceki aya göre skor farkı.</param>
public sealed record SubjectPerformanceRow(
    string Subject, int MasteryScore, decimal AverageNet, int TestCount, int Delta);

/// <summary>M14-4: PDF'e basılacak rapor içeriği. Üretici bu kayıttan başka veri kaynağı okumaz.</summary>
public sealed record StudentReportData(
    string StudentName,
    string TeacherName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int StudyMinutes,
    int LessonCount,
    int AssignmentCompletionPercent,
    IReadOnlyList<SubjectPerformanceRow> Subjects,
    string? Note);

/// <summary>
/// PDF üretimi Infrastructure'dadır (QuestPDF); Application yalnız bu sözleşmeyi bilir, böylece
/// handler birim testinde gerçek PDF üretmeden koşabilir.
/// </summary>
public interface IStudentReportPdfGenerator
{
    byte[] Generate(StudentReportData data);
}

/// <summary>Rapor özet kutusundaki tek satır (etiket + değer).</summary>
/// <param name="Label">Etiket.</param>
/// <param name="Value">Biçimlenmiş değer.</param>
public sealed record ReportSummaryItem(string Label, string Value);

/// <summary>
/// M14-4: Rapor metninin <b>saf</b> biçimlendirmesi. PDF çıktısının içeriğini okuyucu bağımlılığı
/// (PdfPig vb.) eklemeden test edebilmek için düzen ile metin birbirinden ayrıldı.
/// </summary>
public static class StudentReportComposer
{
    public const string DocumentTitle = "EğitimÜssü — Öğrenci Gelişim Raporu";

    /// <summary>Süreyi "12 sa 30 dk" biçimine çevirir; bir saatin altı yalnız dakika gösterir.</summary>
    public static string FormatDuration(int minutes)
    {
        var safe = Math.Max(0, minutes);
        var hours = safe / 60;
        var rest = safe % 60;
        return hours == 0 ? $"{rest} dk" : $"{hours} sa {rest} dk";
    }

    /// <summary>0–1 aralığındaki net oranını yüzde olarak yazar.</summary>
    public static string FormatNet(decimal ratio)
        => $"%{(int)Math.Round(Math.Clamp(ratio, 0m, 1m) * 100m, MidpointRounding.AwayFromZero)}";

    /// <summary>Ödev tamamlama yüzdesi; hiç ödev yoksa oran tanımsızdır ve 0 döner.</summary>
    public static int CompletionPercent(int total, int completed)
        => total <= 0 ? 0 : (int)Math.Round(completed * 100d / total, MidpointRounding.AwayFromZero);

    public static IReadOnlyList<string> HeaderLines(StudentReportData data) =>
    [
        $"Öğrenci: {data.StudentName}",
        $"Öğretmen: {data.TeacherName}",
        $"Dönem: {Format(data.PeriodStart)} – {Format(data.PeriodEnd)}"
    ];

    public static IReadOnlyList<ReportSummaryItem> SummaryLines(StudentReportData data) =>
    [
        new("Toplam çalışma", FormatDuration(data.StudyMinutes)),
        new("Tamamlanan ders", data.LessonCount.ToString(CultureInfo.InvariantCulture)),
        new("Ödev tamamlama", $"%{data.AssignmentCompletionPercent}")
    ];

    /// <summary>İndirilen dosyanın adı; dönem sınırları ada yazılır ki aynı klasörde ayırt edilsin.</summary>
    public static string FileName(StudentReportData data)
        => $"gelisim-raporu-{data.PeriodStart:yyyy-MM-dd}_{data.PeriodEnd:yyyy-MM-dd}.pdf";

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

// ---- Premium kapısı (K-09: tek kaynak IEntitlementDirectory) ----

/// <summary>
/// M14-4 premium kapısı: PDF raporu <b>isteği yapanın</b> üyelik hakkıdır.
/// <para>
/// P08'de geçici bir kural vardı ("taraflardan biri Premium ise geçer"), çünkü o günün
/// <c>IMembershipDirectory</c>'si yalnız öğrenci profiline bakıyor ve öğretmene daima Free
/// dönüyordu (Öğrenilenler #113). P09/K-09 ile üyelik role bakmaksızın kullanıcı başına
/// tutulduğu için kural sadeleşti: <b>istek sahibinin</b> hakkı belirler, Admin daima geçer.
/// </para>
/// </summary>
public sealed class ReportEntitlementResolver
{
    private readonly ICurrentUser _currentUser;
    private readonly IEntitlementDirectory _entitlements;

    public ReportEntitlementResolver(ICurrentUser currentUser, IEntitlementDirectory entitlements)
    {
        _currentUser = currentUser;
        _entitlements = entitlements;
    }

    public async Task<bool> AllowsAsync(PremiumFeature feature, CancellationToken cancellationToken)
    {
        if (_currentUser.Roles.Contains("Admin"))
        {
            return true;
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return false;
        }

        var entitlement = await _entitlements.GetAsync(userId, cancellationToken);

        return feature == PremiumFeature.PdfReport
            ? entitlement.PdfReports
            : MembershipGate.Allows(entitlement.Tier, feature);
    }
}

// ---- Komut / sorgu ----

/// <summary>M14-4: Öğrenci için PDF rapor üretir ve <c>IFileStorage</c>'a yazar.</summary>
public sealed record GenerateStudentReportCommand(Guid StudentId, DateOnly? From, DateOnly? To, string? Note)
    : ICommand<Result<StudentReportResponse>>, IStudentScopedReportRequest;

public sealed record StudentReportResponse(
    Guid ReportId,
    Guid StudentId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string FileName,
    int SizeBytes,
    DateTime CreatedOnUtc);

/// <summary>M14-4: İndirme öncesi raporun anahtarını ve sahibini çözer (yetki authorizer'da).</summary>
public sealed record GetStudentReportFileQuery(Guid ReportId) : IQuery<Result<StudentReportFileResponse>>;

public sealed record StudentReportFileResponse(
    Guid ReportId, Guid StudentId, string StorageKey, string FileName, string ContentType);

public interface IStudentReportRepository
{
    Task AddAsync(StudentReport report, CancellationToken cancellationToken);

    Task<StudentReport?> GetByIdAsync(Guid reportId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

// ---- Rapor verisinin toplanması ----

/// <summary>
/// M14-4: Rapor içeriğini modülün projeksiyonlarından ve <c>Shared/Contracts</c> okuma
/// sözleşmelerinden toplar. Gizlilik seviyesi burada <b>veriyi kısar</b>: <see cref="ReportVisibility.Limited"/>
/// yalnız toplamları bırakır, ders bazlı kırılımı düşürür (M15-4, Öğrenilenler #106).
/// </summary>
public sealed class StudentReportAssembler
{
    private readonly IReportingProjectionReader _reader;
    private readonly IStudentDirectory _students;
    private readonly ITeacherDirectory _teachers;
    private readonly IStudentAssignmentDigestDirectory _assignments;
    private readonly ICurrentUser _currentUser;
    private readonly ILocalCalendar _calendar;

    public StudentReportAssembler(
        IReportingProjectionReader reader,
        IStudentDirectory students,
        ITeacherDirectory teachers,
        IStudentAssignmentDigestDirectory assignments,
        ICurrentUser currentUser,
        ILocalCalendar calendar)
    {
        _reader = reader;
        _students = students;
        _teachers = teachers;
        _assignments = assignments;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public async Task<StudentReportData> BuildAsync(
        Guid studentId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string? note,
        ReportVisibility visibility,
        CancellationToken cancellationToken)
    {
        // P16 T5 / N-50: dönem yerel günlerdir; UTC penceresi [ilk günün 00:00 TR, son günün ertesi 00:00 TR).
        var fromUtc = _calendar.DayRangeUtc(periodStart).StartUtc;
        var toExclusiveUtc = _calendar.DayRangeUtc(periodEnd).EndUtc;

        var weeks = await _reader.ListStudentStudyAsync(
            studentId,
            ReportingPeriods.WeekOf(periodStart),
            ReportingPeriods.WeekOf(periodEnd),
            cancellationToken);

        var lessonCount = await _reader.CountStudentCompletedLessonsAsync(
            studentId,
            (periodStart.Year, periodStart.Month),
            (periodEnd.Year, periodEnd.Month),
            cancellationToken);

        var ownerUserId = await _students.GetOwnerUserIdAsync(studentId, cancellationToken);
        var assignments = ownerUserId is { } studentUserId
            ? await _assignments.GetCompletionAsync(studentUserId, fromUtc, toExclusiveUtc, cancellationToken)
            : new AssignmentCompletionSummary(0, 0);

        var subjects = visibility == ReportVisibility.Full
            ? await BuildSubjectsAsync(studentId, periodStart, periodEnd, cancellationToken)
            : [];

        var studentName = await _students.GetDisplayNameAsync(studentId, cancellationToken);
        var teacherName = Guid.TryParse(_currentUser.UserId, out var userId)
            ? await _teachers.GetDisplayNameAsync(userId, cancellationToken)
            : null;

        return new StudentReportData(
            string.IsNullOrWhiteSpace(studentName) ? "Öğrenci" : studentName,
            string.IsNullOrWhiteSpace(teacherName) ? "—" : teacherName,
            periodStart,
            periodEnd,
            weeks.Sum(week => week.StudyMinutes),
            lessonCount,
            StudentReportComposer.CompletionPercent(assignments.TotalCount, assignments.CompletedCount),
            subjects,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    private async Task<IReadOnlyList<SubjectPerformanceRow>> BuildSubjectsAsync(
        Guid studentId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var rows = await _reader.ListStudentPerformanceAsync(
            studentId,
            null,
            (periodStart.Year, periodStart.Month),
            (periodEnd.Year, periodEnd.Month),
            cancellationToken);

        // Delta saklanmaz, sorgu anında komşu aydan türetilir (Öğrenilenler #102) — sorgu ucuyla aynı kural.
        return rows
            .GroupBy(row => row.Subject)
            .OrderBy(series => series.Key, StringComparer.Ordinal)
            .SelectMany(series =>
            {
                int? previousScore = null;
                return series
                    .OrderBy(row => row.Year)
                    .ThenBy(row => row.Month)
                    .Select(row =>
                    {
                        var delta = ReportingDelta.FromScores(previousScore, row.MasteryScore);
                        previousScore = row.MasteryScore;
                        return new SubjectPerformanceRow(
                            row.Subject, row.MasteryScore, row.AverageNet, row.TestCount, delta);
                    })
                    .ToArray();
            })
            .ToArray();
    }
}

// ---- Handler'lar ----

/// <summary>
/// M14-4: Raporu üretir, depoya yazar ve kaydı açar. <b>Sıra önemlidir</b> (P04 teslim deseni):
/// önce aralık doğrulaması, premium kapısı ve gizlilik kapısı; depoya yazma en sonda — reddedilen
/// bir istek depoda yetim dosya bırakmaz.
/// </summary>
public sealed class GenerateStudentReportCommandHandler
    : ICommandHandler<GenerateStudentReportCommand, Result<StudentReportResponse>>
{
    /// <summary>Rapor penceresi üst sınırı; bir yıllık dönem raporun okunabilir kalmasının sınırı.</summary>
    private const int MaxRangeDays = 366;

    private const string ContentType = "application/pdf";

    private readonly StudentReportPrivacyGate _privacy;
    private readonly ReportEntitlementResolver _entitlement;
    private readonly StudentReportAssembler _assembler;
    private readonly IStudentReportPdfGenerator _generator;
    private readonly IFileStorage _fileStorage;
    private readonly IStudentReportRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;
    private readonly IIdGenerator _idGenerator;

    public GenerateStudentReportCommandHandler(
        StudentReportPrivacyGate privacy,
        ReportEntitlementResolver entitlement,
        StudentReportAssembler assembler,
        IStudentReportPdfGenerator generator,
        IFileStorage fileStorage,
        IStudentReportRepository repository,
        ICurrentUser currentUser,
        IClock clock,
        ILocalCalendar calendar,
        IIdGenerator idGenerator)
    {
        _privacy = privacy;
        _entitlement = entitlement;
        _assembler = assembler;
        _generator = generator;
        _fileStorage = fileStorage;
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _calendar = calendar;
        _idGenerator = idGenerator;
    }

    public async Task<Result<StudentReportResponse>> Handle(
        GenerateStudentReportCommand command, CancellationToken cancellationToken)
    {
        var today = _calendar.Today();   // P16 T5: varsayılan dönemin "bugün"ü Türkiye günü
        var periodEnd = command.To ?? today;
        var periodStart = command.From ?? periodEnd.AddMonths(-1);

        if (periodEnd < periodStart || periodEnd.DayNumber - periodStart.DayNumber > MaxRangeDays)
        {
            return Result<StudentReportResponse>.Failure(ReportingErrors.InvalidRange);
        }

        if (!await _entitlement.AllowsAsync(PremiumFeature.PdfReport, cancellationToken))
        {
            return Result<StudentReportResponse>.Failure(ReportingErrors.PremiumRequired);
        }

        var visibility = await _privacy.ResolveAsync(command.StudentId, cancellationToken);
        if (visibility == ReportVisibility.Hidden)
        {
            // Sorgu uçlarında gizlilik boş sonuç üretir; burada boş bir PDF üretmek yanıltıcı olurdu —
            // istek açıkça reddedilir (Öğrenilenler #106'nın belge karşılığı).
            return Result<StudentReportResponse>.Failure(ReportingErrors.ReportNotShared);
        }

        var data = await _assembler.BuildAsync(
            command.StudentId, periodStart, periodEnd, command.Note, visibility, cancellationToken);

        var bytes = _generator.Generate(data);
        var fileName = StudentReportComposer.FileName(data);

        using var content = new MemoryStream(bytes, writable: false);
        var stored = await _fileStorage.SaveAsync(
            $"reporting/{command.StudentId}", fileName, ContentType, content, cancellationToken);

        var now = _clock.UtcNow;
        var report = new StudentReport(
            _idGenerator.New(),
            command.StudentId,
            Guid.TryParse(_currentUser.UserId, out var userId) ? userId : Guid.Empty,
            periodStart,
            periodEnd,
            stored.Key,
            stored.FileName,
            stored.ContentType,
            bytes.Length,
            now);

        await _repository.AddAsync(report, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudentReportResponse>.Success(new StudentReportResponse(
            report.Id, report.StudentId, report.PeriodStart, report.PeriodEnd,
            report.FileName, report.SizeBytes, report.CreatedOnUtc));
    }
}

/// <summary>M14-4: İndirme için rapor kaydını çözer; yetki <c>StudentReportFileAuthorizer</c>'dadır.</summary>
public sealed class GetStudentReportFileQueryHandler
    : IQueryHandler<GetStudentReportFileQuery, Result<StudentReportFileResponse>>
{
    private readonly IStudentReportRepository _repository;

    public GetStudentReportFileQueryHandler(IStudentReportRepository repository) => _repository = repository;

    public async Task<Result<StudentReportFileResponse>> Handle(
        GetStudentReportFileQuery query, CancellationToken cancellationToken)
    {
        var report = await _repository.GetByIdAsync(query.ReportId, cancellationToken);
        if (report is null)
        {
            return Result<StudentReportFileResponse>.Failure(ReportingErrors.ReportNotFound);
        }

        return Result<StudentReportFileResponse>.Success(new StudentReportFileResponse(
            report.Id, report.StudentId, report.StorageKey, report.FileName, report.ContentType));
    }
}
