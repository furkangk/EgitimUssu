using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class AssignmentStateMachineTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ILocalCalendar Calendar = new LocalCalendar("Europe/Istanbul", new Clock());
    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
    private static Assignment New(AssignmentStatus status = AssignmentStatus.Pending, DateTime? due = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Ödev", null, due, status, null, Now.AddDays(-1), null);

    public static IEnumerable<object[]> Transitions()
    {
        foreach (var status in Enum.GetValues<AssignmentStatus>())
        foreach (var action in new[] { "complete", "submit", "approve", "return", "cancel" })
        {
            var submitted = status is AssignmentStatus.InProgress or AssignmentStatus.Completed;
            var allowed = action switch
            {
                "complete" or "cancel" => status == AssignmentStatus.Pending,
                "submit" => status is AssignmentStatus.Pending or AssignmentStatus.ReturnedForRevision || submitted,
                _ => submitted
            };
            yield return new object[] { status, action, allowed };
        }
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void Transition_table_rejects_without_mutation_or_events(AssignmentStatus status, string action, bool allowed)
    {
        var assignment = New(status);
        assignment.ClearDomainEvents();
        var result = action switch
        {
            "complete" => assignment.MarkCompleted(Now, Calendar),
            "submit" => assignment.AttachSubmission("key", "v.pdf", "application/pdf", 12, Now, Calendar),
            "approve" => assignment.Approve("iyi", Now),
            "return" => assignment.ReturnForRevision("düzelt", Now),
            _ => assignment.Cancel()
        };
        Assert.Equal(allowed, result.IsSuccess);
        if (allowed) return;
        Assert.Equal("assignments.invalid_transition", result.Error.Code);
        Assert.Equal(status, assignment.Status);
        Assert.Null(assignment.AttachmentStorageKey);
        Assert.Null(assignment.SubmittedOnUtc);
        Assert.Empty(assignment.SubmissionVersions);
        Assert.Empty(assignment.DomainEvents);
    }

    [Fact]
    public void Versions_keep_files_feedback_and_grade_and_approved_is_terminal()
    {
        var a = New();
        Assert.True(a.AttachSubmission("first", "ilk.pdf", "application/pdf", 11, Now, Calendar).IsSuccess);
        Assert.True(a.ReturnForRevision("ikinci soru", Now.AddMinutes(1)).IsSuccess);
        Assert.True(a.AttachSubmission("second", "son.pdf", "application/pdf", 22, Now.AddMinutes(2), Calendar).IsSuccess);
        Assert.True(a.Approve("tamam", Now.AddMinutes(3)).IsSuccess);
        Assert.True(a.Grade(90, "güzel", Now.AddMinutes(4)).IsSuccess);
        Assert.True(a.AttachSubmission("third", "yeni.pdf", "application/pdf", 33, Now.AddMinutes(5), Calendar).IsFailure);
        Assert.Equal("second", a.AttachmentStorageKey);
        Assert.Equal(2, a.SubmissionVersions.Count);
        var versions = a.SubmissionVersions.OrderBy(x => x.Number).ToArray();
        Assert.Equal("first", versions[0].StorageKey);
        Assert.Equal("ikinci soru", versions[0].TeacherFeedback);
        Assert.Null(versions[0].Score);
        Assert.Equal(90, versions[1].Score);
        Assert.Equal("güzel", versions[1].TeacherFeedback);
        Assert.Equal(Now.AddMinutes(2), a.SubmittedOnUtc);
    }

    [Fact]
    public void Revision_clears_legacy_score_but_keeps_it_on_submitted_version()
    {
        var a = New();
        a.AttachSubmission("key", "v.pdf", "application/pdf", 12, Now, Calendar);
        // Eski guard'sız sistemde teslim durumunda kalmış puanlı bir satır.
        typeof(Assignment).GetProperty(nameof(Assignment.Score))!.SetValue(a, 60);
        typeof(Assignment).GetProperty(nameof(Assignment.GradedOnUtc))!.SetValue(a, Now);
        Assert.True(a.ReturnForRevision("yeniden", Now.AddMinutes(1)).IsSuccess);
        Assert.Null(a.Score);
        Assert.Null(a.GradedOnUtc);
        Assert.Equal(60, Assert.Single(a.SubmissionVersions).Score);
    }

    [Theory]
    [InlineData(20, 59, 59, false)]
    [InlineData(21, 0, 0, true)]
    public void Late_boundary_uses_local_day_end(int hour, int minute, int second, bool late)
    {
        var a = New(due: new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));
        var at = new DateTime(2026, 11, 15, hour, minute, second, DateTimeKind.Utc);
        Assert.True(a.MarkCompleted(at, Calendar).IsSuccess);
        Assert.Equal(late, a.IsLate);
        Assert.Equal(late, Assert.Single(a.SubmissionVersions).IsLate);
        Assert.Equal(at, a.SubmittedOnUtc);
    }
}
