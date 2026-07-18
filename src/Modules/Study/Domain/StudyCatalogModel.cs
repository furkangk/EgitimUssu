using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Domain;

/// <summary>
/// Öğrencinin kendi tanımladığı ders (Subject) kataloğu. Kronometre, deneme/test, takvim ve gelişim
/// takibi bu katalogdan tutarlı ders/konu adları alır. Öğrenci-scoped; StudentId M03 StudentProfile.Id'ye
/// mantıksal referanstır (bkz. <see cref="StudyStudent"/>).
/// </summary>
public sealed class StudentSubjectCatalog : AggregateRoot<Guid>
{
    private StudentSubjectCatalog()
    {
    }

    public StudentSubjectCatalog(
        Guid id,
        Guid studentId,
        string name,
        string? colorHex,
        DateTime nowUtc)
    {
        Id = id;
        StudentId = studentId;
        Name = name.Trim();
        ColorHex = Normalize(colorHex);
        IsActive = true;
        CreatedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;
    }

    public Guid StudentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? ColorHex { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void Update(string name, string? colorHex, bool isActive, DateTime nowUtc)
    {
        Name = name.Trim();
        ColorHex = Normalize(colorHex);
        IsActive = isActive;
        UpdatedOnUtc = nowUtc;
    }

    private static string? Normalize(string? colorHex) =>
        string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim();
}

/// <summary>
/// Katalog dersine bağlı konu (Topic). Örn. Matematik → Türev, Limit, Olasılık. StudentId, sahiplik
/// çözümü tek sorguda yapılabilsin diye ders üzerinden denormalize edilir.
/// </summary>
public sealed class StudentTopicCatalog : Entity<Guid>
{
    private StudentTopicCatalog()
    {
    }

    public StudentTopicCatalog(
        Guid id,
        Guid subjectId,
        Guid studentId,
        string name,
        int orderIndex,
        DateTime nowUtc)
    {
        Id = id;
        SubjectId = subjectId;
        StudentId = studentId;
        Name = name.Trim();
        OrderIndex = orderIndex;
        IsActive = true;
        CreatedOnUtc = nowUtc;
    }

    public Guid SubjectId { get; private set; }

    public Guid StudentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int OrderIndex { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public void Update(string name, int orderIndex, bool isActive)
    {
        Name = name.Trim();
        OrderIndex = orderIndex;
        IsActive = isActive;
    }
}

/// <summary>
/// Öğrencinin kendi tuttuğu ders notu. Öğretmenin ders oturumuna bağlı <c>LessonNote</c>'undan (M06)
/// ayrıdır; öğrencinin kendi çalışma dünyasına aittir ve opsiyonel ders/konu ile ilişkilendirilir.
/// </summary>
public sealed class StudyNote : AggregateRoot<Guid>
{
    private StudyNote()
    {
    }

    public StudyNote(
        Guid id,
        Guid studentId,
        string title,
        string body,
        string? subject,
        string? topic,
        string? attachmentUrl,
        DateTime nowUtc)
    {
        Id = id;
        StudentId = studentId;
        Title = title.Trim();
        Body = body.Trim();
        Subject = Normalize(subject);
        Topic = Normalize(topic);
        AttachmentUrl = Normalize(attachmentUrl);
        CreatedOnUtc = nowUtc;
        UpdatedOnUtc = nowUtc;
    }

    public Guid StudentId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? Subject { get; private set; }

    public string? Topic { get; private set; }

    public string? AttachmentUrl { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void Update(string title, string body, string? subject, string? topic, string? attachmentUrl, DateTime nowUtc)
    {
        Title = title.Trim();
        Body = body.Trim();
        Subject = Normalize(subject);
        Topic = Normalize(topic);
        AttachmentUrl = Normalize(attachmentUrl);
        UpdatedOnUtc = nowUtc;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
