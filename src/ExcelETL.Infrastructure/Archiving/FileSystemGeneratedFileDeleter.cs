using ExcelETL.Application.Archiving;
using Microsoft.Extensions.Options;

namespace ExcelETL.Infrastructure.Archiving;

// Deletes a file the writer archived. The relative path comes from the database, so it is checked
// to stay under the archive root before anything is deleted -- a tampered record must never be able
// to delete a file elsewhere on the server.
public class FileSystemGeneratedFileDeleter(IOptions<GeneratedFilesArchiveOptions> options) : IGeneratedFileDeleter
{
    public long Delete(string relativePath)
    {
        var rootPath = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("GeneratedFilesArchive:RootPath must be configured.");
        }

        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{relativePath}' is outside the archive root.");
        }

        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            return 0;
        }

        var length = file.Length;
        file.Delete();
        return length;
    }
}
