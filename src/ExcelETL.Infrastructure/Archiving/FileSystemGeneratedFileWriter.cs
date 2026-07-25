using ExcelETL.Application.Archiving;
using Microsoft.Extensions.Options;

namespace ExcelETL.Infrastructure.Archiving;

// Archives a raw file stream (source upload or generated target workbook) under
// {RootPath}\{yyyy}\{MM}\{yyyyMMdd-HHmmss-fff}_{source|target}_{sanitized original file name} --
// timestampUtc is supplied by the caller (not read from the clock here) so the same timestamp can be
// reused across both the source and target write of one request, and so tests can simulate two calls
// at the exact same millisecond deterministically. No retry/collision handling beyond the millisecond
// precision of the file name -- an exact collision (same original file name AND same millisecond)
// simply overwrites the previous file (FileMode.Create), a decision accepted as negligible in
// occasional real-world usage (see docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md, 34.3).
public class FileSystemGeneratedFileWriter(IOptions<GeneratedFilesArchiveOptions> options) : IGeneratedFileWriter
{
    public Task<string> WriteSourceAsync(
        Stream content, string originalFileName, DateTime timestampUtc, CancellationToken cancellationToken = default) =>
        WriteAsync(content, originalFileName, timestampUtc, "source", cancellationToken);

    public Task<string> WriteTargetAsync(
        Stream content, string originalFileName, DateTime timestampUtc, CancellationToken cancellationToken = default) =>
        WriteAsync(content, originalFileName, timestampUtc, "target", cancellationToken);

    private async Task<string> WriteAsync(
        Stream content, string originalFileName, DateTime timestampUtc, string kind, CancellationToken cancellationToken)
    {
        var rootPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("GeneratedFilesArchive:RootPath must be configured.");
        }

        var sanitizedFileName = GeneratedFileNameSanitizer.Sanitize(originalFileName);
        var relativeDirectory = Path.Combine(timestampUtc.ToString("yyyy"), timestampUtc.ToString("MM"));
        var fileName = $"{timestampUtc:yyyyMMdd-HHmmss-fff}_{kind}_{sanitizedFileName}";
        var relativePath = Path.Combine(relativeDirectory, fileName);

        Directory.CreateDirectory(Path.Combine(rootPath, relativeDirectory));

        var fullPath = Path.Combine(rootPath, relativePath);
        await using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(destination, cancellationToken);

        return relativePath;
    }
}
