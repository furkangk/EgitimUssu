using EgitimUssu.Modules.Assignments.Domain;

namespace EgitimUssu.Tests.Unit;

public sealed class LessonNoteTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Ctor_DefaultsAndStoresVisibility()
    {
        var note = new LessonNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "özet", "konu", "öneri", LessonNoteVisibility.StudentAndParent, Now);
        Assert.Equal(LessonNoteVisibility.StudentAndParent, note.Visibility);
    }

    [Fact]
    public void Update_ChangesVisibility()
    {
        var note = new LessonNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "özet", null, null, LessonNoteVisibility.Private, Now);
        note.Update("özet2", null, null, LessonNoteVisibility.Student);
        Assert.Equal(LessonNoteVisibility.Student, note.Visibility);
    }
}
