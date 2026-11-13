using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P15 T2 (K-13, N-08): Öğretmen–öğrenci erişim kapsamı. Kural, sahte depo yerine gerçek sorguya karşı
/// (EF InMemory üzerinde <see cref="StudentsDbContext"/>) doğrulanır — master §1 "sahte depo yasağı".
/// <para>
/// <c>Private</c> ⇔ (bağ <c>Linked</c> ve arşivli değil) <b>veya</b> (profil <c>TeacherManaged</c>,
/// <c>CreatedByTeacherUserId</c> bu öğretmen ve bağı yaşıyor: <c>Manual</c>/<c>InviteSent</c>, arşivli değil).
/// Aksi her durum <c>None</c>; <c>Class</c> P22'ye kadar hiç dönmez.
/// </para>
/// </summary>
public sealed class TeacherStudentAccessTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Teacher = Guid.NewGuid();
    private static readonly Guid OtherTeacher = Guid.NewGuid();

    [Fact]
    public async Task Linked_Link_Gives_Private_Even_For_A_Self_Registered_Profile()
    {
        var student = SelfRegisteredProfile();
        var scope = await ScopeAsync(Teacher, student, Link(Teacher, student.Id, TeacherStudentLinkStatus.Linked));

        Assert.Equal(TeacherAccessScope.Private, scope);
    }

    [Theory]
    [InlineData(TeacherStudentLinkStatus.Manual)]
    [InlineData(TeacherStudentLinkStatus.InviteSent)]
    public async Task Living_Link_On_Own_Teacher_Managed_Profile_Gives_Private(TeacherStudentLinkStatus status)
    {
        var student = TeacherManagedProfile(Teacher);
        var scope = await ScopeAsync(Teacher, student, Link(Teacher, student.Id, status));

        Assert.Equal(TeacherAccessScope.Private, scope);
    }

    [Theory]
    [InlineData(TeacherStudentLinkStatus.Linked)]
    [InlineData(TeacherStudentLinkStatus.Manual)]
    public async Task Archived_Link_Gives_None(TeacherStudentLinkStatus status)
    {
        var student = TeacherManagedProfile(Teacher);
        var link = Link(Teacher, student.Id, status);
        link.Archive(Now);

        var scope = await ScopeAsync(Teacher, student, link);

        Assert.Equal(TeacherAccessScope.None, scope);
    }

    /// <summary>
    /// Plan formülünün ikinci kolu yalnız "arşivli değil" diyordu; bitmiş bağ (Rejected/Disconnected)
    /// bugünkü <c>IsLinkedTeacherOfStudentAsync</c>'te de erişim vermiyordu. Fail-closed korunur.
    /// </summary>
    [Theory]
    [InlineData(TeacherStudentLinkStatus.Rejected)]
    [InlineData(TeacherStudentLinkStatus.Disconnected)]
    public async Task Ended_Link_On_Own_Profile_Gives_None(TeacherStudentLinkStatus status)
    {
        var student = TeacherManagedProfile(Teacher);
        var scope = await ScopeAsync(Teacher, student, Link(Teacher, student.Id, status));

        Assert.Equal(TeacherAccessScope.None, scope);
    }

    [Fact]
    public async Task Own_Profile_Without_Any_Link_Row_Gives_None()
    {
        var student = TeacherManagedProfile(Teacher);
        var scope = await ScopeAsync(Teacher, student);

        Assert.Equal(TeacherAccessScope.None, scope);
    }

    [Fact]
    public async Task Unlinked_Teacher_Gets_None_For_Someone_Elses_Student()
    {
        var student = TeacherManagedProfile(OtherTeacher);
        var scope = await ScopeAsync(Teacher, student, Link(OtherTeacher, student.Id, TeacherStudentLinkStatus.Linked));

        Assert.Equal(TeacherAccessScope.None, scope);
    }

    /// <summary>
    /// Derinlemesine savunma: <c>Manual</c>/<c>InviteSent</c> bağ ancak öğretmenin kendi açtığı profilde
    /// erişim verir. Başkasının (ya da öğrencinin kendi) profiline bir yoldan bağ satırı düşse bile kapsam açılmaz.
    /// </summary>
    [Fact]
    public async Task Manual_Link_On_A_Profile_The_Teacher_Did_Not_Create_Gives_None()
    {
        var selfProfile = SelfRegisteredProfile();
        var othersProfile = TeacherManagedProfile(OtherTeacher);

        Assert.Equal(
            TeacherAccessScope.None,
            await ScopeAsync(Teacher, selfProfile, Link(Teacher, selfProfile.Id, TeacherStudentLinkStatus.Manual)));
        Assert.Equal(
            TeacherAccessScope.None,
            await ScopeAsync(Teacher, othersProfile, Link(Teacher, othersProfile.Id, TeacherStudentLinkStatus.InviteSent)));
    }

    [Fact]
    public async Task Unknown_Student_Gives_None()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        var access = new TeacherStudentAccess(context);

        Assert.Equal(TeacherAccessScope.None, await access.GetScopeAsync(Teacher, Guid.NewGuid(), default));
    }

    /// <summary>Geriye uyumluluk: eski sorgu artık kapsamın tek kaynağına delege eder (Messaging, ProgressTracking, Reporting).</summary>
    [Fact]
    public async Task IsLinkedTeacherOfStudent_Is_True_Exactly_When_Scope_Is_Private()
    {
        var databaseName = Guid.NewGuid().ToString();
        var linked = TeacherManagedProfile(Teacher);
        var rejected = TeacherManagedProfile(Teacher);
        var archived = TeacherManagedProfile(Teacher);
        var archivedLink = Link(Teacher, archived.Id, TeacherStudentLinkStatus.Linked);
        archivedLink.Archive(Now);

        await SeedAsync(
            databaseName,
            [linked, rejected, archived],
            [
                Link(Teacher, linked.Id, TeacherStudentLinkStatus.Manual),
                Link(Teacher, rejected.Id, TeacherStudentLinkStatus.Rejected),
                archivedLink
            ]);

        await using var context = NewContext(databaseName);
        var directory = new StudentDirectory(context, new TeacherStudentAccess(context));

        Assert.True(await directory.IsLinkedTeacherOfStudentAsync(Teacher, linked.Id, default));
        Assert.False(await directory.IsLinkedTeacherOfStudentAsync(Teacher, rejected.Id, default));
        Assert.False(await directory.IsLinkedTeacherOfStudentAsync(Teacher, archived.Id, default));
        Assert.False(await directory.IsLinkedTeacherOfStudentAsync(OtherTeacher, linked.Id, default));
    }

    // --- Yardımcılar ---------------------------------------------------------------------

    private static async Task<TeacherAccessScope> ScopeAsync(
        Guid teacherUserId, StudentProfile student, params TeacherStudentLink[] links)
    {
        var databaseName = Guid.NewGuid().ToString();
        await SeedAsync(databaseName, [student], links);

        await using var context = NewContext(databaseName);
        return await new TeacherStudentAccess(context).GetScopeAsync(teacherUserId, student.Id, default);
    }

    private static async Task SeedAsync(
        string databaseName, IReadOnlyCollection<StudentProfile> profiles, IReadOnlyCollection<TeacherStudentLink> links)
    {
        await using var context = NewContext(databaseName);
        context.StudentProfiles.AddRange(profiles);
        context.TeacherStudentLinks.AddRange(links);
        await context.SaveChangesAsync();
    }

    private static StudentsDbContext NewContext(string databaseName) =>
        new(new DbContextOptionsBuilder<StudentsDbContext>().UseInMemoryDatabase(databaseName).Options,
            new JsonDomainEventMapper());

    private static StudentProfile TeacherManagedProfile(Guid createdBy) =>
        new(Guid.NewGuid(), userId: null, createdByTeacherUserId: createdBy, parentUserId: null,
            "Ada Şahin", "7. Sınıf", null, null, null, null, StudentOrigin.TeacherManaged, true, Now);

    private static StudentProfile SelfRegisteredProfile() =>
        new(Guid.NewGuid(), userId: Guid.NewGuid(), createdByTeacherUserId: null, parentUserId: null,
            "Can Yıldız", "11. Sınıf", null, null, null, null, StudentOrigin.SelfRegistered, true, Now);

    private static TeacherStudentLink Link(Guid teacherUserId, Guid studentId, TeacherStudentLinkStatus status) =>
        new(Guid.NewGuid(), teacherUserId, studentId, status, Now);
}
