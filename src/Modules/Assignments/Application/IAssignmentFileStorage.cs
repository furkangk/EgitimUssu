namespace EgitimUssu.Modules.Assignments.Application;

/// <summary>Öğrencinin yüklediği ödev dosyasının kalıcı depolaması (yerel disk ile başlar).</summary>
public interface IAssignmentFileStorage
{
    /// <summary>Ödev için dosyayı kaydeder (ödev başına tek dosya; varsa üzerine yazılır).</summary>
    Task SaveAsync(Guid assignmentId, string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>Kayıtlı dosyayı okur. Yoksa null.</summary>
    Task<AssignmentFile?> OpenAsync(Guid assignmentId, CancellationToken cancellationToken);
}

public sealed record AssignmentFile(Stream Content, string FileName, string ContentType);
