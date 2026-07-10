namespace ExcelETL.Application.Extraction;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream fileContent, string fileName, CancellationToken cancellationToken = default);
}
