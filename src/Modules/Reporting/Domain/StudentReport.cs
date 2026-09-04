using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Reporting.Domain;

/// <summary>
/// M14-4: Üretilmiş bir PDF öğrenci raporunun kaydı. Dosyanın kendisi <c>IFileStorage</c>'ta durur;
/// burada yalnız <b>anahtar</b> ve meta veri tutulur (public URL yoktur, P04 deseni).
/// <para>
/// Kayıt indirme ucunun yetki kaynağıdır: <c>GET /reports/{reportId}/file</c> yolunda öğrenci kimliği
/// yoktur, hangi öğrencinin raporu olduğu ancak bu satırdan bilinir.
/// </para>
/// </summary>
public sealed class StudentReport : AggregateRoot<Guid>
{
    private StudentReport()
    {
    }

    public StudentReport(
        Guid id,
        Guid studentId,
        Guid createdByUserId,
        DateOnly periodStart,
        DateOnly periodEnd,
        string storageKey,
        string fileName,
        string contentType,
        int sizeBytes,
        DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        CreatedByUserId = createdByUserId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedOnUtc = createdOnUtc;
    }

    public Guid StudentId { get; private set; }

    /// <summary>Raporu üreten kullanıcı (öğretmen, öğrencinin kendisi, veli ya da Admin).</summary>
    public Guid CreatedByUserId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    /// <summary><c>IFileStorage</c> anahtarı (public URL değildir).</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }
}
