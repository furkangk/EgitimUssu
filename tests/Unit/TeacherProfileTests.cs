using EgitimUssu.Modules.Teachers.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class TeacherProfileTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    private static TeacherProfile New()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Ahmet", "Matematik", "İstanbul", "Kadıköy",
            null, null, TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", false, null, Now);

    [Fact]
    public void Update_ReplacesSubjects()
    {
        var profile = New();
        var subjects = new[]
        {
            new TeacherSubject(Guid.NewGuid(), profile.Id, "Matematik"),
            new TeacherSubject(Guid.NewGuid(), profile.Id, "Fizik")
        };

        profile.Update("Ahmet", "Matematik", "İstanbul", "Kadıköy", null, null,
            TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", null,
            Array.Empty<TeacherAvailabilitySlot>(), subjects, Array.Empty<TeacherCertificate>(), Now);

        Assert.Equal(2, profile.Subjects.Count);
        Assert.Contains(profile.Subjects, s => s.Subject == "Fizik");
    }

    [Fact]
    public void Update_ReplacesCertificates()
    {
        var profile = New();
        var certs = new[] { new TeacherCertificate(Guid.NewGuid(), profile.Id, "ÖABT Başarı", "MEB", 2024, null) };

        profile.Update("Ahmet", "Matematik", "İstanbul", "Kadıköy", null, null,
            TeacherLessonFormat.Online, 5, "Lisans", 400m, "TRY", null,
            Array.Empty<TeacherAvailabilitySlot>(), Array.Empty<TeacherSubject>(), certs, Now);

        Assert.Single(profile.Certificates);
        Assert.Equal("ÖABT Başarı", profile.Certificates[0].Title);
    }
}
