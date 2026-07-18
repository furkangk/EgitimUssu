using EgitimUssu.Modules.Students.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class StudentProfileTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewProfile_DefaultsToNoneTargetExam()
    {
        var profile = NewProfile();
        Assert.Equal(TargetExam.None, profile.TargetExam);
    }

    [Fact]
    public void SetTargetExam_UpdatesValueAndTimestamp()
    {
        var profile = NewProfile();
        var later = Now.AddMinutes(5);

        profile.SetTargetExam(TargetExam.LGS, later);

        Assert.Equal(TargetExam.LGS, profile.TargetExam);
        Assert.Equal(later, profile.UpdatedOnUtc);
    }

    private static StudentProfile NewProfile()
        => new(
            Guid.NewGuid(),
            userId: Guid.NewGuid(),
            createdByTeacherUserId: null,
            parentUserId: null,
            fullName: "Ali Veli",
            gradeLevel: "8",
            contactEmail: null,
            contactPhone: null,
            goalSummary: null,
            levelNotes: null,
            origin: StudentOrigin.SelfRegistered,
            isActive: true,
            createdOnUtc: Now);
}
