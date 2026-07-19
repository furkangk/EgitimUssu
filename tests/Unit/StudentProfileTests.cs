using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Shared.Contracts;

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
    public void NewProfile_DefaultsToFreeMembership()
    {
        var profile = NewProfile();
        Assert.Equal(MembershipTier.Free, profile.MembershipTier);
    }

    [Fact]
    public void SetMembershipTier_UpdatesValueAndTimestamp()
    {
        var profile = NewProfile();
        var later = Now.AddMinutes(5);

        profile.SetMembershipTier(MembershipTier.Premium, later);

        Assert.Equal(MembershipTier.Premium, profile.MembershipTier);
        Assert.Equal(later, profile.UpdatedOnUtc);
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

    [Fact]
    public void NewProfile_DefaultsToNullDateOfBirth()
    {
        var profile = NewProfile();
        Assert.Null(profile.DateOfBirth);
    }

    [Fact]
    public void Update_SetsDateOfBirth()
    {
        var profile = NewProfile();
        var dob = new DateTime(2012, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = Now.AddMinutes(5);

        profile.Update("Ali Veli", "8", null, null, null, null, true, later, TargetExam.None, dob);

        Assert.Equal(dob, profile.DateOfBirth);
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
