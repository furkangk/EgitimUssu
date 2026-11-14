using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 5 / E-07: KVKK hesap silme.
/// <para>
/// Üç kural pazarlık dışıdır: (1) talep <b>30 gün</b> beklemeden uygulanmaz — silme geri alınamaz,
/// kullanıcıya fikir değiştirme hakkı bırakılır; (2) anonimleştirmeden sonra hesaba <b>giriş
/// yapılamaz</b>; (3) <b>ticari kayıt silinmez</b>, yalnız kimliksizleşir — ödeme tutarı ve tarihi
/// yasal saklama süresince korunur (<c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §2.3).
/// </para>
/// </summary>
public sealed class AccountDeletionTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    // --- Bekleme süresi -----------------------------------------------------------------

    /// <summary>Talep, planlanan ana gelmeden uygulanabilir sayılmamalı.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(45, true)]
    public void Request_Should_Become_Due_Only_After_The_Waiting_Period(int daysElapsed, bool expectedDue)
    {
        var request = new AccountDeletionRequest(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(expectedDue, request.IsDue(Now.AddDays(daysElapsed)));
    }

    /// <summary>Bekleme süresi politikada 30 gündür; sayı tek yerden okunmalı.</summary>
    [Fact]
    public void Waiting_Period_Should_Be_Thirty_Days()
    {
        var request = new AccountDeletionRequest(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(Now.AddDays(30), request.ScheduledForUtc);
        Assert.Equal(TimeSpan.FromDays(30), AccountDeletionPolicy.WaitingPeriod);
    }

    /// <summary>İptal edilen talep, süresi dolsa bile uygulanmamalı.</summary>
    [Fact]
    public void Cancelled_Request_Should_Never_Become_Due()
    {
        var request = new AccountDeletionRequest(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.True(request.Cancel(Now.AddDays(3)));
        Assert.Equal(AccountDeletionRequestStatus.Cancelled, request.Status);
        Assert.False(request.IsDue(Now.AddDays(90)));
        Assert.False(request.Cancel(Now.AddDays(4)));    // ikinci iptal bir şey değiştirmez
    }

    // --- Anonimleştirme -----------------------------------------------------------------

    /// <summary>
    /// Anonimleştirmeden sonra e-posta ile giriş yapılamaz: hem adres değişir hem hesap kapanır.
    /// </summary>
    [Fact]
    public void Anonymized_Account_Should_Not_Be_Loggable()
    {
        var user = NewUser("ayse@example.com", "0555 111 22 33");
        var surrogate = PersonalDataPlaceholder.DeletedEmail(Guid.NewGuid());

        user.Anonymize(surrogate, "unusable-hash", Now);

        Assert.Equal(UserAccountStatus.Closed, user.Status);
        Assert.DoesNotContain("ayse", user.Email, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ayse", user.NormalizedEmail, StringComparison.OrdinalIgnoreCase);
        Assert.Null(user.PhoneNumber);
        Assert.Equal(PersonalDataPlaceholder.FullName, $"{user.FirstName} {user.LastName}");
        Assert.Empty(user.RefreshSessions);
        Assert.Empty(user.SecurityTokens);
    }

    /// <summary>
    /// 🔴 Vekil e-posta, silinen adresten <b>türetilemez</b>. Hash kullanılsaydı aynı hash bilinen
    /// bir adres listesine uygulanıp eşleştirilebilirdi — bu anonimleştirme değil takma adlaştırma
    /// olurdu ve takma adlı veri hâlâ kişisel veridir.
    /// </summary>
    [Fact]
    public void Surrogate_Email_Should_Not_Be_Derived_From_The_Original()
    {
        const string email = "ayse@example.com";

        var first = PersonalDataPlaceholder.DeletedEmail(Guid.NewGuid());
        var second = PersonalDataPlaceholder.DeletedEmail(Guid.NewGuid());

        // Aynı adres için iki ayrı çağrı farklı vekil üretmeli: deterministik olsaydı türetilebilirdi.
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(email.Split('@')[0], first, StringComparison.OrdinalIgnoreCase);
    }

    // --- Orkestrasyon -------------------------------------------------------------------

    /// <summary>Süresi dolmamış talep için hiçbir modül çağrılmaz.</summary>
    [Fact]
    public async Task Processor_Should_Not_Touch_Modules_Before_The_Waiting_Period()
    {
        await using var context = NewIdentityContext();
        var user = await SeedPendingRequestAsync(context);
        var anonymizer = new RecordingAnonymizer();

        var summary = await NewProcessor(context, Now.AddDays(29), [anonymizer]).RunAsync();

        Assert.Equal(0, summary.CompletedCount);
        Assert.Empty(anonymizer.Calls);
        Assert.Equal(UserAccountStatus.Active, (await context.UserAccounts.FindAsync(user.Id))!.Status);
    }

    /// <summary>Süresi dolan talepte <b>her</b> modül çağrılır ve hesap kapanır.</summary>
    [Fact]
    public async Task Processor_Should_Call_Every_Module_Then_Close_The_Account()
    {
        await using var context = NewIdentityContext();
        var user = await SeedPendingRequestAsync(context);
        var first = new RecordingAnonymizer("Students", affected: 3);
        var second = new RecordingAnonymizer("Study", affected: 2);

        var summary = await NewProcessor(context, Now.AddDays(31), [first, second]).RunAsync();

        Assert.Equal(1, summary.CompletedCount);
        Assert.Equal(new[] { user.Id }, first.Calls);
        Assert.Equal(new[] { user.Id }, second.Calls);
        // 3 (Students) + 2 (Study) + 1 (Identity'nin kendi kaydı) — Identity de sayılır.
        Assert.Equal(6, summary.AnonymizedRecordCount);

        var reloaded = await context.UserAccounts.FindAsync(user.Id);
        Assert.Equal(UserAccountStatus.Closed, reloaded!.Status);
        Assert.Null(reloaded.PhoneNumber);
    }

    /// <summary>
    /// P15 T7 / N-31: Hesap kapanınca dolaşımdaki erişim token'ları da iptal edilir. Oturum satırları
    /// silinse de token ömrünü (60 dk) tamamlayana kadar kapanmış hesap adına istek atabiliyordu.
    /// Başarısız turda hesap açık kaldığı için token'lara dokunulmaz.
    /// </summary>
    [Fact]
    public async Task Closing_The_Account_Revokes_Its_Access_Tokens()
    {
        await using var context = NewIdentityContext();
        var user = await SeedPendingRequestAsync(context);
        var revocation = new SpyTokenRevocation();

        await NewProcessor(context, Now.AddDays(31), [new RecordingAnonymizer()], revocation).RunAsync();

        Assert.Equal([user.Id], revocation.AllRevokedUserIds);
    }

    [Fact]
    public async Task Failed_Deletion_Does_Not_Revoke_Access_Tokens()
    {
        await using var context = NewIdentityContext();
        await SeedPendingRequestAsync(context);
        var revocation = new SpyTokenRevocation();

        await NewProcessor(context, Now.AddDays(31), [new ThrowingAnonymizer()], revocation).RunAsync();

        Assert.Empty(revocation.AllRevokedUserIds);
    }

    /// <summary>İkinci tur hiçbir şey yapmamalı — akış yarıda kalırsa yeniden koşabilmeli.</summary>
    [Fact]
    public async Task Running_Twice_Should_Be_Idempotent()
    {
        await using var context = NewIdentityContext();
        await SeedPendingRequestAsync(context);
        var anonymizer = new RecordingAnonymizer();

        await NewProcessor(context, Now.AddDays(31), [anonymizer]).RunAsync();
        var second = await NewProcessor(context, Now.AddDays(32), [anonymizer]).RunAsync();

        Assert.Equal(0, second.CompletedCount);
        Assert.Single(anonymizer.Calls);
    }

    /// <summary>
    /// Bir modül patlarsa hesap <b>kapanmamalı</b>: yarım anonimleştirilmiş bir hesap, hem veri
    /// kalıntısı bırakır hem de talebi "tamamlandı" gösterip bir daha denenmesini engeller.
    /// </summary>
    [Fact]
    public async Task Failing_Module_Should_Leave_The_Request_Pending()
    {
        await using var context = NewIdentityContext();
        var user = await SeedPendingRequestAsync(context);

        var summary = await NewProcessor(context, Now.AddDays(31), [new ThrowingAnonymizer()]).RunAsync();

        Assert.Equal(0, summary.CompletedCount);
        Assert.Equal(1, summary.FailedCount);
        var reloaded = await context.UserAccounts.FindAsync(user.Id);
        Assert.Equal(UserAccountStatus.Active, reloaded!.Status);
        Assert.Equal(AccountDeletionRequestStatus.Pending, (await context.AccountDeletionRequests.SingleAsync()).Status);
    }

    // --- Ticari kayıt korunur ------------------------------------------------------------

    /// <summary>
    /// Ödeme kaydı <b>silinmez</b>: tutar, para birimi ve vade yasal saklama süresince korunur;
    /// yalnız serbest metin alanları (açıklama/not) kimliksizleşir.
    /// </summary>
    [Fact]
    public async Task Payment_Records_Should_Keep_Their_Amounts_After_Anonymization()
    {
        await using var context = NewPaymentsContext();
        var studentId = Guid.NewGuid();
        var record = new PaymentRecord(
            Guid.NewGuid(), Guid.NewGuid(), studentId, null, BillingItemType.LessonFee,
            "Ayşe Yılmaz - Eylül matematik dersi", "TRY", 750m, 750m, Now.AddDays(-10), Now.AddDays(-9),
            PaymentStatus.Paid, null, null, "Veli elden ödedi", Now.AddDays(-20));
        context.PaymentRecords.Add(record);
        await context.SaveChangesAsync();

        var affected = await new PaymentsPersonalDataAnonymizer(context)
            .AnonymizeAsync(new PersonalDataSubject(Guid.NewGuid(), [studentId]), default);

        var reloaded = await context.PaymentRecords.SingleAsync();
        Assert.Equal(1, affected);
        Assert.Equal(750m, reloaded.ExpectedAmount);      // tutar korunur
        Assert.Equal(750m, reloaded.CollectedAmount);
        Assert.Equal("TRY", reloaded.Currency);
        Assert.Equal(PaymentStatus.Paid, reloaded.Status);
        Assert.DoesNotContain("Ayşe", reloaded.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Null(reloaded.Notes);

        // İkinci tur hiçbir şey değiştirmez.
        Assert.Equal(0, await new PaymentsPersonalDataAnonymizer(context)
            .AnonymizeAsync(new PersonalDataSubject(Guid.NewGuid(), [studentId]), default));
    }

    // --- Yardımcılar ---------------------------------------------------------------------

    private static IdentityDbContext NewIdentityContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new JsonDomainEventMapper());

    private static PaymentsDbContext NewPaymentsContext() =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new JsonDomainEventMapper());

    private static UserAccount NewUser(string email, string? phone)
    {
        var user = new UserAccount(
            Guid.NewGuid(), email, email.ToUpperInvariant(), "hash", "Ayşe", "Yılmaz", phone,
            UserAccountStatus.Active, true, false, Now);
        user.RefreshSessions.Add(new RefreshTokenSession(Guid.NewGuid(), user.Id, "token-hash", "iPhone", Now, Now.AddDays(30)));
        user.SecurityTokens.Add(new UserSecurityToken(Guid.NewGuid(), user.Id, SecurityTokenPurpose.EmailVerification, "t", Now, Now.AddHours(1)));
        return user;
    }

    private static async Task<UserAccount> SeedPendingRequestAsync(IdentityDbContext context)
    {
        var user = NewUser("ayse@example.com", "0555 111 22 33");
        context.UserAccounts.Add(user);
        context.AccountDeletionRequests.Add(new AccountDeletionRequest(Guid.NewGuid(), user.Id, Now));
        await context.SaveChangesAsync();
        return user;
    }

    private static AccountDeletionProcessor NewProcessor(
        IdentityDbContext context,
        DateTime nowUtc,
        IEnumerable<IPersonalDataAnonymizer> anonymizers,
        ITokenRevocationService? revocation = null) =>
        new(context, anonymizers, new NoStudentDirectory(), new StubPasswordHasher(),
            revocation ?? new SpyTokenRevocation(),
            new GuidIdGenerator(), new FixedClock(nowUtc), new NullLogger<AccountDeletionProcessor>());

    private sealed class SpyTokenRevocation : ITokenRevocationService
    {
        public List<Guid> AllRevokedUserIds { get; } = [];

        public Task RevokeAllAccessTokensAsync(Guid userId, CancellationToken cancellationToken)
        {
            AllRevokedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task RevokeSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class RecordingAnonymizer : IPersonalDataAnonymizer
    {
        private readonly int _affected;

        public RecordingAnonymizer(string moduleName = "Fake", int affected = 0)
        {
            ModuleName = moduleName;
            _affected = affected;
        }

        public string ModuleName { get; }

        public List<Guid> Calls { get; } = [];

        public Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
        {
            Calls.Add(subject.UserId);
            return Task.FromResult(_affected);
        }
    }

    private sealed class ThrowingAnonymizer : IPersonalDataAnonymizer
    {
        public string ModuleName => "Broken";

        public Task<int> AnonymizeAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
            => throw new InvalidOperationException("modül anonimleştirmesi başarısız");
    }

    /// <summary>Bu testlerde öğrenci profili yok; dizin boş liste döner.</summary>
    private sealed class NoStudentDirectory : IStudentDirectory
    {
        public Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guid>>([]);

        public Task<Guid?> GetOwnerUserIdAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<Guid?>(null);

        public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<AgreedLessonRate?>(null);

        public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacherUserId, Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<string?> GetDisplayNameAsync(Guid studentId, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    /// <summary>Anonimleştirmede yalnız "kullanılamaz bir özet üret" işi görülür.</summary>
    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string hashedPassword, string providedPassword) => false;
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
