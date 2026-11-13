using EgitimUssu.Modules.Assignments.Domain;

namespace EgitimUssu.Tests.Unit;

/// <summary>M06-4: Ödev puanlaması — 0–100 aralığı ve yalnız onaylanmış ödevde geçerlilik.</summary>
public sealed class AssignmentGradeTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Assignment New(AssignmentStatus status)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Ödev", null, Now.AddDays(1), status, null, Now, null);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Grade_Should_Reject_Score_Outside_Range(int score)
    {
        var assignment = New(AssignmentStatus.Approved);

        var result = assignment.Grade(score, null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignments.invalid_score", result.Error.Code);
        Assert.Null(assignment.Score);
        Assert.DoesNotContain(assignment.DomainEvents, domainEvent => domainEvent is AssignmentGradedDomainEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Grade_Should_Accept_Range_Bounds(int score)
    {
        var assignment = New(AssignmentStatus.Approved);

        var result = assignment.Grade(score, "  eline sağlık  ", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(score, assignment.Score);
        Assert.Equal(Now, assignment.GradedOnUtc);
        Assert.Equal("eline sağlık", assignment.TeacherFeedback);
        Assert.Single(assignment.DomainEvents.OfType<AssignmentGradedDomainEvent>());
    }

    [Theory]
    [InlineData(AssignmentStatus.Pending)]
    [InlineData(AssignmentStatus.InProgress)]
    [InlineData(AssignmentStatus.Completed)]
    [InlineData(AssignmentStatus.Cancelled)]
    [InlineData(AssignmentStatus.ReturnedForRevision)]
    public void Grade_Should_Require_Approved_Status(AssignmentStatus status)
    {
        var assignment = New(status);

        var result = assignment.Grade(80, null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignments.grade_requires_approval", result.Error.Code);
        Assert.Null(assignment.Score);
    }

    [Fact]
    public void Grade_Should_Keep_Existing_Feedback_When_Not_Supplied()
    {
        var assignment = New(AssignmentStatus.Completed);
        assignment.Approve("ilk geri bildirim", Now);

        var result = assignment.Grade(75, null, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("ilk geri bildirim", assignment.TeacherFeedback);
        Assert.Equal(75, assignment.Score);
    }
}
