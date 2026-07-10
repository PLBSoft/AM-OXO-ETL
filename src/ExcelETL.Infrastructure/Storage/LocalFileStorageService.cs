using ExcelETL.Application.Extraction;
using Microsoft.Extensions.Options;

namespace ExcelETL.Infrastructure.Storage;

public class LocalFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService
{
    public async Task<string> SaveAsync(Stream fileContent, string fileName, CancellationToken cancellationToken = default)
    {
        var archiveDirectory = options.Value.ArchiveDirectory;
        if (string.IsNullOrWhiteSpace(archiveDirectory))
        {
            throw new InvalidOperationException("FileStorage:ArchiveDirectory must be configured.");
        }

        Directory.CreateDirectory(archiveDirectory);
        var fullPath = Path.Combine(archiveDirectory, fileName);

        await using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileContent.CopyToAsync(destination, cancellationToken);

        return fullPath;
    }
}
