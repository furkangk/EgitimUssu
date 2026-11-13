using System.Security.Claims;
using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.API.Host.Seeding;

/// <summary>Bir tohumlama turunun sonucu. <see cref="Created"/> sıfırsa hiçbir yeni kayıt açılmamıştır.</summary>
public sealed record DemoDataSeedSummary(
    int Teachers,
    int Students,
    int Parents,
    int Lessons,
    int Sessions,
    int Assignments,
    int Payments,
    int StudySessions,
    bool Skipped = false,
    string? SkipReason = null)
{
    /// <summary>Hiçbir şey oluşturulmamış tur.</summary>
    public static DemoDataSeedSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Guard tarafından engellenmiş tur.</summary>
    public static DemoDataSeedSummary Blocked(string reason) =>
        Empty with { Skipped = true, SkipReason = reason };

    /// <summary>Bu turda açılan toplam kayıt sayısı.</summary>
    public int Created =>
        Teachers + Students + Parents + Lessons + Sessions + Assignments + Payments + StudySessions;
}

/// <summary>
/// E-06: Demo verisinin hangi ortamda tohumlanabileceğine karar verir.
/// <para>
/// Saf bir fonksiyondur — kararın kendisi, onu uygulayan altyapıdan bağımsız test edilebilsin diye.
/// Kural: <b>üretimde asla</b>, bayrak açık olsa bile. Yanlışlıkla açılmış bir ortam değişkeni
/// üretim veritabanına demo öğretmen yazamamalı; öyle bir kaydı geri almak elle temizlik demektir.
/// </para>
/// </summary>
public static class DemoDataSeedGuard
{
    /// <summary>Tohumlama engelliyse nedenini döndürür; serbestse <c>null</c>.</summary>
    /// <param name="environmentName">Çalışma ortamının adı (<c>ASPNETCORE_ENVIRONMENT</c>).</param>
    /// <param name="enabled"><c>Seeding:Enabled</c> bayrağı.</param>
    public static string? BlockReason(string environmentName, bool enabled)
    {
        if (string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            return "Ortam Production — demo veri tohumlaması üretimde hiçbir koşulda çalışmaz.";
        }

        return enabled ? null : "Seeding:Enabled = false.";
    }
}

/// <summary>
/// E-06: Staging için tanınabilir bir demo veri seti kurar — 2 öğretmen, 5 öğrenci (2'si kendi
/// hesabıyla), 2 veli, 3 haftalık ders geçmişi, ödevler, ödemeler ve çalışma seansları.
/// <para>
/// <b>Doğrudan DbContext'e yazmaz.</b> Her kayıt, o kaydı gerçek hayatta açacak kullanıcının
/// kimliğiyle ilgili modül komutundan geçer: öğretmen profilini öğretmen açar, veli bağını veli
/// ister ve öğrenci onaylar. Kazanç iki yönlü — iş kuralları ve yetki kuralları atlanmaz, ve
/// tohumlanan veri <b>gerçek kullanıcıların üretebileceği</b> veriyle birebir aynı biçimde olur.
/// Doğrudan tablo yazımı, yarın bir domain kuralı değiştiğinde sessizce geçersiz veri bırakırdı.
/// </para>
/// <para>
/// <b>İdempotenttir.</b> Staging her deploy'da yeniden başlar; her adım önce "bu kayıt zaten var
/// mı" diye sorar ve yalnız eksikleri açar. Var olan hiçbir kayda dokunmaz — demo ortamında elle
/// yapılan bir düzenleme sonraki açılışta geri alınmaz.
/// </para>
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Demo veriyi tohumlar. Guard engelliyse hiçbir sorgu çalıştırmaz.</summary>
    /// <param name="serviceProvider">Kök servis sağlayıcı.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public static async Task<DemoDataSeedSummary> SeedDemoDataAsync(
        this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var environment = services.GetRequiredService<IHostEnvironment>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        var enabled = configuration.GetValue("Seeding:Enabled", defaultValue: false);
        var blockReason = DemoDataSeedGuard.BlockReason(environment.EnvironmentName, enabled);
        if (blockReason is not null)
        {
            // Bayrak açıkken engellenmek "sessiz başarısızlık" olurdu — beklenti ile gerçek ayrıştı, söyle.
            if (enabled)
            {
                logger.LogWarning("Demo veri tohumlaması atlandı: {Reason}", blockReason);
            }

            return DemoDataSeedSummary.Blocked(blockReason);
        }

        var session = new SeedSession(services, cancellationToken);
        var summary = await new DemoDataSeedRun(session, services.GetRequiredService<IClock>()).ExecuteAsync();

        logger.LogInformation(
            "Demo veri tohumlaması bitti ({Environment}): {Created} yeni kayıt "
            + "(öğretmen {Teachers}, öğrenci {Students}, veli {Parents}, ders {Lessons}, "
            + "oturum {Sessions}, ödev {Assignments}, ödeme {Payments}, çalışma {StudySessions}).",
            environment.EnvironmentName,
            summary.Created,
            summary.Teachers,
            summary.Students,
            summary.Parents,
            summary.Lessons,
            summary.Sessions,
            summary.Assignments,
            summary.Payments,
            summary.StudySessions);

        return summary;
    }
}

/// <summary>
/// Tohumlama turunu yürüten iç akış. Sıra bağımlılığı gerçektir: veli bağı öğrenci olmadan,
/// ders oturumu ders planı olmadan, ödev oturum olmadan açılamaz.
/// </summary>
internal sealed class DemoDataSeedRun(SeedSession session, IClock clock)
{
    private static readonly TeacherSeed[] TeacherSeeds =
    [
        new("demo.ogretmen1@egitimussu.local", "Elif", "Kaya", "Matematik", "İstanbul", "Kadıköy", 450m,
            "LGS ve TYT matematik", 9, DayOfWeek.Monday),
        new("demo.ogretmen2@egitimussu.local", "Mert", "Aydın", "Fizik", "Ankara", "Çankaya", 400m,
            "TYT/AYT fizik", 6, DayOfWeek.Wednesday)
    ];

    // Kendi hesabı olan öğrenciler mobil girişi ve çalışma kronometresini denemeyi mümkün kılar;
    // öğretmen-yönetimli öğrenciler ise hesapsız öğrenci akışını temsil eder. İkisi de gerçek.
    private static readonly StudentSeed[] StudentSeeds =
    [
        new("Zeynep Demir", "8. Sınıf", TargetExam.LGS, TeacherIndex: 0, "demo.ogrenci1@egitimussu.local"),
        new("Can Yıldız", "11. Sınıf", TargetExam.TYT, TeacherIndex: 1, "demo.ogrenci2@egitimussu.local"),
        new("Ada Şahin", "7. Sınıf", TargetExam.LGS, TeacherIndex: 0, SelfEmail: null),
        new("Deniz Koç", "12. Sınıf", TargetExam.AYT, TeacherIndex: 0, SelfEmail: null),
        new("Ege Aslan", "9. Sınıf", TargetExam.School, TeacherIndex: 1, SelfEmail: null)
    ];

    // Veli, kendi hesabı olan çocuğa bağlanır: bağı onaylayacak yetkili yalnız öğrencinin kendisi
    // ya da Admin'dir (veli kendi bağını onaylayamaz), demo setinde Admin hesabı yok.
    private static readonly ParentSeed[] ParentSeeds =
    [
        new("demo.veli1@egitimussu.local", "Selin", "Demir", StudentIndex: 0, "Anne"),
        new("demo.veli2@egitimussu.local", "Kaan", "Yıldız", StudentIndex: 1, "Baba")
    ];

    private const int HistoryWeeks = 3;

    private readonly List<Guid> _teacherUserIds = [];
    private readonly List<SeededStudent> _students = [];

    private int _teachers;
    private int _studentCount;
    private int _parents;
    private int _lessons;
    private int _sessions;
    private int _assignments;
    private int _payments;
    private int _studySessions;

    public async Task<DemoDataSeedSummary> ExecuteAsync()
    {
        await SeedTeachersAsync();
        await SeedStudentsAsync();
        // Veli bağının onayı Parents'ın öğrenciyi tanımasına bağlı; o bilgi olayla geliyor.
        await session.DrainOutboxAsync();
        await SeedParentsAsync();
        await SeedLessonHistoryAsync();
        await SeedPaymentsAsync();
        await SeedStudySessionsAsync();
        // Ders/ödev/ödeme olaylarının tüketicileri (bildirim, projeksiyon) de işlensin: staging'i
        // yarım işlenmiş bir kuyrukla değil, oturmuş bir durumla açıyoruz.
        await session.DrainOutboxAsync();

        return new DemoDataSeedSummary(
            _teachers, _studentCount, _parents, _lessons, _sessions, _assignments, _payments, _studySessions);
    }

    private async Task SeedTeachersAsync()
    {
        foreach (var seed in TeacherSeeds)
        {
            var (userId, _) = await session.EnsureUserAsync(
                seed.Email, seed.FirstName, seed.LastName, UserRole.Teacher);
            _teacherUserIds.Add(userId);

            session.ActAs(userId, seed.Email, "Teacher");

            var existing = await session.QueryOrDefaultAsync(new GetTeacherProfileByUserIdQuery(userId));
            if (existing is not null)
            {
                continue;
            }

            await session.SendAsync(new CreateTeacherProfileCommand(
                userId,
                seed.FullName,
                seed.Subject,
                seed.City,
                seed.District,
                $"{seed.Headline} alanında {seed.ExperienceYears} yıllık deneyim. (Demo hesabı)",
                seed.Headline,
                TeacherLessonFormat.Hybrid,
                seed.ExperienceYears,
                "Lisans",
                seed.HourlyRate,
                DemoSeed.Currency,
                [
                    new TeacherAvailabilityRequest(
                        seed.AvailableDay, new TimeOnly(16, 0), new TimeOnly(20, 0),
                        IsOnlineAvailable: true, IsInPersonAvailable: true)
                ],
                [seed.Subject],
                []));

            _teachers++;
        }
    }

    private async Task SeedStudentsAsync()
    {
        foreach (var seed in StudentSeeds)
        {
            var teacherUserId = _teacherUserIds[seed.TeacherIndex];
            var subject = TeacherSeeds[seed.TeacherIndex].Subject;

            var seeded = seed.SelfEmail is null
                ? await EnsureTeacherManagedStudentAsync(seed, teacherUserId, subject)
                : await EnsureSelfRegisteredStudentAsync(seed, teacherUserId, subject);

            _students.Add(seeded);
        }
    }

    private async Task<SeededStudent> EnsureTeacherManagedStudentAsync(
        StudentSeed seed, Guid teacherUserId, string subject)
    {
        session.ActAs(teacherUserId, TeacherSeeds[seed.TeacherIndex].Email, "Teacher");

        var existing = await session.QueryOrDefaultAsync(new ListStudentsByTeacherQuery(teacherUserId));
        var match = existing?.FirstOrDefault(student =>
            string.Equals(student.FullName, seed.FullName, StringComparison.Ordinal));

        if (match is not null)
        {
            return new SeededStudent(match.Id, null, seed, teacherUserId, subject);
        }

        var created = await session.SendAsync(new CreateStudentProfileCommand(
            UserId: null,
            CreatedByTeacherUserId: teacherUserId,
            ParentUserId: null,
            seed.FullName,
            seed.GradeLevel,
            ContactEmail: null,
            ContactPhone: null,
            GoalSummary: $"{seed.TargetExam} hazırlığı (demo)",
            LevelNotes: null,
            StudentOrigin.TeacherManaged,
            [new StudentSubjectRequest(subject, seed.TargetExam.ToString())],
            seed.TargetExam));

        _studentCount++;
        return new SeededStudent(created.Id, null, seed, teacherUserId, subject);
    }

    private async Task<SeededStudent> EnsureSelfRegisteredStudentAsync(
        StudentSeed seed, Guid teacherUserId, string subject)
    {
        var (firstName, lastName) = SplitName(seed.FullName);
        var (studentUserId, _) = await session.EnsureUserAsync(
            seed.SelfEmail!, firstName, lastName, UserRole.Student);

        session.ActAs(studentUserId, seed.SelfEmail!, "Student");

        var existing = await session.QueryOrDefaultAsync(new GetStudentProfileByUserIdQuery(studentUserId));
        if (existing is not null)
        {
            return new SeededStudent(existing.Id, studentUserId, seed, teacherUserId, subject);
        }

        var created = await session.SendAsync(new CreateStudentProfileCommand(
            UserId: studentUserId,
            CreatedByTeacherUserId: null,
            ParentUserId: null,
            seed.FullName,
            seed.GradeLevel,
            ContactEmail: seed.SelfEmail,
            ContactPhone: null,
            GoalSummary: $"{seed.TargetExam} hazırlığı (demo)",
            LevelNotes: null,
            StudentOrigin.SelfRegistered,
            [new StudentSubjectRequest(subject, seed.TargetExam.ToString())],
            seed.TargetExam));

        _studentCount++;
        return new SeededStudent(created.Id, studentUserId, seed, teacherUserId, subject);
    }

    private async Task SeedParentsAsync()
    {
        foreach (var seed in ParentSeeds)
        {
            var child = _students[seed.StudentIndex];
            var (parentUserId, _) = await session.EnsureUserAsync(
                seed.Email, seed.FirstName, seed.LastName, UserRole.Parent);

            session.ActAs(parentUserId, seed.Email, "Parent");

            var profile = await session.QueryOrDefaultAsync(new GetParentProfileQuery(parentUserId));
            if (profile is null)
            {
                await session.SendAsync(new CreateParentProfileCommand(
                    parentUserId, $"{seed.FirstName} {seed.LastName}", ContactPhone: null, seed.Email));
                _parents++;
            }

            var children = await session.QueryOrDefaultAsync(new ListChildrenQuery(parentUserId));
            if (children?.Any(link => link.StudentId == child.StudentId) == true)
            {
                continue;
            }

            var link = await session.SendAsync(new RequestChildLinkCommand(
                parentUserId,
                child.StudentId,
                seed.Relationship,
                child.Seed.FullName,
                InviteCode: null,
                IsPrimaryContact: true));

            // Bağı veli değil öğrenci onaylar (self-approve engeli) — demo veli hep kendi hesabı
            // olan bir çocuğa bağlandığı için onaylayacak kimlik her zaman var.
            session.ActAs(child.UserId!.Value, child.Seed.SelfEmail!, "Student");
            await session.SendAsync(new ApproveChildLinkCommand(link.Id, child.UserId!.Value));
        }
    }

    private async Task SeedLessonHistoryAsync()
    {
        var firstLessonStart = WeekAlignedStart(clock.UtcNow, weeksBack: HistoryWeeks);

        // Her öğrenciye kendi saat dilimi verilir: aynı öğretmenin iki öğrencisi aynı saate
        // düşerse Scheduling `scheduling.teacher_conflict` ile haklı olarak reddeder.
        for (var studentIndex = 0; studentIndex < _students.Count; studentIndex++)
        {
            var student = _students[studentIndex];
            var teacherEmail = TeacherSeeds[student.Seed.TeacherIndex].Email;
            session.ActAs(student.TeacherUserId, teacherEmail, "Teacher");

            var existing = await session.QueryOrDefaultAsync(new ListLessonSchedulesForTeacherQuery(
                student.TeacherUserId,
                firstLessonStart.AddDays(-1),
                clock.UtcNow.AddDays(1)));

            for (var week = 0; week < HistoryWeeks; week++)
            {
                var startAtUtc = firstLessonStart.AddDays(7 * week).AddHours(studentIndex);
                var endAtUtc = startAtUtc.AddHours(1);

                var alreadyPlanned = existing?.Any(lesson =>
                    lesson.StudentId == student.StudentId && lesson.StartAtUtc == startAtUtc) == true;

                if (alreadyPlanned)
                {
                    continue;
                }

                var lesson = await session.SendAsync(new CreateLessonScheduleCommand(
                    student.TeacherUserId,
                    student.StudentId,
                    student.Subject,
                    ScheduledLessonFormat.Online,
                    startAtUtc,
                    endAtUtc,
                    DemoSeed.TimeZone,
                    RecurrenceRule: null,
                    ReminderOffsetMinutes: 60,
                    LocationLabel: null,
                    MeetingUrl: "https://meet.example.com/demo",
                    Notes: "Demo ders kaydı"));
                _lessons++;

                await CompleteLessonAsync(lesson.Id, student, startAtUtc, endAtUtc, week);
            }
        }
    }

    private async Task CompleteLessonAsync(
        Guid lessonScheduleId, SeededStudent student, DateTime startAtUtc, DateTime endAtUtc, int week)
    {
        var lessonSession = await session.SendAsync(new CreateSessionFromScheduleCommand(lessonScheduleId));
        _sessions++;

        await session.SendAsync(new StartLessonSessionCommand(lessonSession.Id));

        var topic = $"{student.Subject} — {week + 1}. hafta konu tekrarı";
        await session.SendAsync(new CompleteLessonSessionCommand(
            lessonSession.Id,
            startAtUtc,
            endAtUtc,
            StudentAttendanceStatus.Attended,
            topic,
            CoveredContent: "Demo içerik: konu anlatımı ve örnek çözümü.",
            TeacherNotes: "Demo notu: öğrenci derse hazırlıklı geldi.",
            IsChargeable: true));

        await session.SendAsync(new CreateLessonSessionFollowUpCommand(
            lessonSession.Id,
            $"{topic} işlendi.",
            CoveredTopics: topic,
            Recommendations: "Verilen ödevi teslim tarihine kadar tamamla.",
            [
                new AssignmentDraft(
                    $"{student.Subject} {week + 1}. hafta ödevi",
                    "Demo ödev: konu tekrarı ve 20 soruluk test.",
                    endAtUtc.AddDays(6),
                    AttachmentUrl: null)
            ],
            LessonNoteVisibility.StudentAndParent));
        _assignments++;
    }

    private async Task SeedPaymentsAsync()
    {
        foreach (var student in _students)
        {
            var teacher = TeacherSeeds[student.Seed.TeacherIndex];
            session.ActAs(student.TeacherUserId, teacher.Email, "Teacher");

            var records = await session.QueryOrDefaultAsync(
                new ListPaymentRecordsForTeacherQuery(student.TeacherUserId, OutstandingOnly: false));

            var description = $"{student.Seed.FullName} — aylık ders paketi (demo)";
            if (records?.Any(record => string.Equals(record.Description, description, StringComparison.Ordinal)) == true)
            {
                continue;
            }

            // Biri bilerek gecikmiş bırakılır: "geciken ödeme" ekranı boş bir listeyle test edilemez.
            var isOverdue = student.Seed.TeacherIndex == 1;
            var expected = teacher.HourlyRate * HistoryWeeks;
            var dueDateUtc = clock.UtcNow.Date.AddDays(isOverdue ? -5 : 10);

            await session.SendAsync(new CreatePaymentRecordCommand(
                student.TeacherUserId,
                student.StudentId,
                RelatedLessonSessionId: null,
                BillingItemType.MonthlyPackage,
                description,
                DemoSeed.Currency,
                expected,
                isOverdue ? 0m : expected,
                dueDateUtc,
                isOverdue ? null : dueDateUtc.AddDays(-3),
                isOverdue ? PaymentStatus.Overdue : PaymentStatus.Paid,
                dueDateUtc.AddDays(-30),
                dueDateUtc,
                Notes: "Demo ödeme kaydı"));
            _payments++;
        }
    }

    private async Task SeedStudySessionsAsync()
    {
        // Çalışma seansı öğrencinin kendi kaydıdır; yalnız hesabı olan öğrenciler için tohumlanır.
        foreach (var student in _students.Where(student => student.UserId is not null))
        {
            session.ActAs(student.UserId!.Value, student.Seed.SelfEmail!, "Student");

            var fromUtc = clock.UtcNow.Date.AddDays(-HistoryWeeks * 7);
            var existing = await session.QueryOrDefaultAsync(
                new ListStudySessionsQuery(student.StudentId, fromUtc, clock.UtcNow, null));

            if (existing is { Count: > 0 })
            {
                continue;
            }

            for (var day = 1; day <= HistoryWeeks * 2; day++)
            {
                await session.SendAsync(new CreateManualStudySessionCommand(
                    student.StudentId,
                    student.Subject,
                    $"{student.Subject} konu tekrarı",
                    EffectiveMinutes: 30 + (day % 4 * 15),
                    StudiedOnUtc: clock.UtcNow.Date.AddDays(-day * 3).AddHours(19),
                    PersonalNote: "Demo çalışma kaydı"));
                _studySessions++;
            }
        }
    }

    /// <summary>Geçmişteki bir haftanın Pazartesi 15:00 UTC anını verir (tohumlama tekrarlanabilir olsun).</summary>
    private static DateTime WeekAlignedStart(DateTime nowUtc, int weeksBack)
    {
        var monday = nowUtc.Date.AddDays(-(((int)nowUtc.DayOfWeek + 6) % 7));
        return DateTime.SpecifyKind(monday.AddDays(-7 * weeksBack).AddHours(15), DateTimeKind.Utc);
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var separator = fullName.LastIndexOf(' ');
        return separator < 0
            ? (fullName, fullName)
            : (fullName[..separator], fullName[(separator + 1)..]);
    }

    private sealed record TeacherSeed(
        string Email, string FirstName, string LastName, string Subject, string City, string District,
        decimal HourlyRate, string Headline, int ExperienceYears, DayOfWeek AvailableDay)
    {
        public string FullName => $"{FirstName} {LastName}";
    }

    private sealed record StudentSeed(
        string FullName, string GradeLevel, TargetExam TargetExam, int TeacherIndex, string? SelfEmail);

    private sealed record ParentSeed(
        string Email, string FirstName, string LastName, int StudentIndex, string Relationship);

    private sealed record SeededStudent(
        Guid StudentId, Guid? UserId, StudentSeed Seed, Guid TeacherUserId, string Subject);
}

/// <summary>
/// Tohumlama sırasında komut/sorgu gönderen ve <b>kim olarak</b> gönderildiğini belirleyen oturum.
/// <para>
/// Yetkilendiriciler <see cref="ICurrentUser"/>'a bakar, o da <see cref="IHttpContextAccessor"/>
/// üzerinden çalışır. Tohumlama bir HTTP isteği içinde koşmadığı için bağlam burada elle kurulur:
/// her adım, o adımı gerçek hayatta yapacak kullanıcının kimliğine bürünür.
/// </para>
/// </summary>
internal sealed class SeedSession(IServiceProvider services, CancellationToken cancellationToken)
{
    private readonly ICommandDispatcher _commands = services.GetRequiredService<ICommandDispatcher>();
    private readonly IQueryDispatcher _queries = services.GetRequiredService<IQueryDispatcher>();
    private readonly IHttpContextAccessor _httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();

    private const int MaxOutboxRounds = 20;

    /// <summary>Sonraki komut/sorguların hangi kullanıcı adına koşacağını belirler.</summary>
    public void ActAs(Guid userId, string email, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // authenticationType verilmezse ClaimsIdentity.IsAuthenticated false kalır ve her
        // yetkilendirici isteği reddeder.
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Seeding"))
        };
    }

    /// <summary>
    /// Kullanıcıyı garanti eder: önce giriş dener (varsa kimliğini öğrenir), olmazsa kaydeder.
    /// Identity'de e-postayla kullanıcı arama ucu yok; giriş denemesi hem varlığı hem kimliği
    /// tek çağrıda verir.
    /// </summary>
    public async Task<(Guid UserId, bool Created)> EnsureUserAsync(
        string email, string firstName, string lastName, UserRole role)
    {
        ActAsAnonymous();

        var login = await _commands.Dispatch(
            new LoginUserCommand(email, DemoSeed.Password, "seed"), cancellationToken);

        if (login.IsSuccess)
        {
            return (login.Value!.UserId, false);
        }

        var registered = await SendAsync(new RegisterUserCommand(
            email, DemoSeed.Password, firstName, lastName, PhoneNumber: null, [role]));

        return (registered.UserId, true);
    }

    /// <summary>Komutu gönderir; başarısızsa tohumlama durur — yarım bir demo ortamı sessizce bırakılmaz.</summary>
    public async Task<TValue> SendAsync<TValue>(ICommand<Result<TValue>> command)
    {
        var result = await _commands.Dispatch(command, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            throw new InvalidOperationException(
                $"Demo veri tohumlaması '{command.GetType().Name}' adımında durdu: "
                + $"{result.Error.Code} — {result.Error.Message}");
        }

        return result.Value;
    }

    /// <summary>Sorguyu gönderir; kayıt yoksa (ya da erişilemiyorsa) <c>null</c> döner.</summary>
    public async Task<TValue?> QueryOrDefaultAsync<TValue>(IQuery<Result<TValue>> query)
        where TValue : class
    {
        var result = await _queries.Dispatch(query, cancellationToken);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// Bekleyen outbox mesajlarını boşaltır.
    /// <para>
    /// Modüller birbirini <b>olayla</b> tanır: Parents, bir öğrenciyi ancak öğrenci olayı
    /// işlendikten sonra bilir ve bilmeden bağ onayını <i>fail-closed</i> reddeder. Tohumlama tek
    /// turda aktığı için arka plan dispatcher'ının kendi periyodunu beklemek yerine kuyruk burada
    /// boşaltılır — aksi halde adımların başarısı zamanlamaya kalırdı.
    /// </para>
    /// </summary>
    public async Task DrainOutboxAsync()
    {
        var processor = services.GetRequiredService<IOutboxProcessor>();

        // Her tur en çok Outbox:BatchSize mesaj alır; kuyruk boşalana kadar sınırlı sayıda tur çevrilir.
        for (var round = 0; round < MaxOutboxRounds; round++)
        {
            if (await processor.DispatchPendingAsync(cancellationToken) == 0)
            {
                return;
            }
        }
    }

    private void ActAsAnonymous() => _httpContextAccessor.HttpContext = new DefaultHttpContext();
}

/// <summary>
/// Demo verisinin sabitleri. Parola <b>sır değildir</b>: yalnız staging'de var olan hesaplara aittir
/// ve <c>doc/architecture/environments.md</c>'de açıkça yazılıdır — demo hesabına giremeyen bir
/// staging ortamı işe yaramaz.
/// </summary>
internal static class DemoSeed
{
    public const string Password = "Demo1234!";
    public const string Currency = "TRY";
    public const string TimeZone = "Europe/Istanbul";
}
