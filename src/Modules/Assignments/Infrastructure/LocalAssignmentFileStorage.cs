using EgitimUssu.Modules.Assignments.Application;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

/// <summary>
/// Ödev dosyalarını yerel diskte saklayan basit depolama. Kök klasör `Assignments:UploadRoot`
/// yapılandırmasından; yoksa uygulama tabanının altında `uploads/assignments`. Dosya adı ödev kimliğine
/// göre normalize edilir (ödev başına tek teslim); orijinal ad `.name` yan dosyasında tutulur.
/// Not: Üretim için nesne depolama (S3/Blob) ile değiştirilebilir; bu yerel başlangıç sürümüdür.
/// </summary>
internal sealed class LocalAssignmentFileStorage : IAssignmentFileStorage
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly string _root;

    public LocalAssignmentFileStorage(IConfiguration configuration)
    {
        var configured = configuration["Assignments:UploadRoot"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "uploads", "assignments")
            : configured;
    }

    public async Task SaveAsync(Guid assignmentId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var extension = Path.GetExtension(fileName);
        var dataPath = DataPath(assignmentId, extension);

        // Aynı ödevin önceki teslimini temizle (farklı uzantı olabilir).
        foreach (var existing in Directory.EnumerateFiles(_root, assignmentId.ToString("N") + ".*"))
        {
            File.Delete(existing);
        }

        await using (var file = File.Create(dataPath))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        await File.WriteAllTextAsync(MetaPath(assignmentId), SanitizeFileName(fileName), cancellationToken);
    }

    public async Task<AssignmentFile?> OpenAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root))
        {
            return null;
        }

        var dataPath = Directory
            .EnumerateFiles(_root, assignmentId.ToString("N") + ".*")
            .FirstOrDefault(p => !p.EndsWith(".name", StringComparison.OrdinalIgnoreCase));

        if (dataPath is null)
        {
            return null;
        }

        var fileName = File.Exists(MetaPath(assignmentId))
            ? (await File.ReadAllTextAsync(MetaPath(assignmentId), cancellationToken)).Trim()
            : Path.GetFileName(dataPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(dataPath);
        }

        var contentType = ContentTypeProvider.TryGetContentType(fileName, out var resolved)
            ? resolved
            : "application/octet-stream";

        Stream stream = File.OpenRead(dataPath);
        return new AssignmentFile(stream, fileName, contentType);
    }

    private string DataPath(Guid assignmentId, string extension) =>
        Path.Combine(_root, assignmentId.ToString("N") + (string.IsNullOrWhiteSpace(extension) ? ".bin" : extension));

    private string MetaPath(Guid assignmentId) =>
        Path.Combine(_root, assignmentId.ToString("N") + ".name");

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "odev" : name;
    }
}
