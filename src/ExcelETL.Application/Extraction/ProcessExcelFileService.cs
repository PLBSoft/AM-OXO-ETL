using ExcelETL.Domain.Entities;

namespace ExcelETL.Application.Extraction;

public sealed class ProcessExcelFileService(
    IExtractionConfigRepository extractionConfigRepository,
    IExcelExtractionService excelExtractionService,
    IExcelGeneratorService excelGeneratorService,
    IFileStorageService fileStorageService,
    IExtractionHistoryRepository extractionHistoryRepository) : IProcessExcelFileService
{
    public async Task<ProcessExcelFileResult> ProcessAsync(
        ProcessExcelFileCommand command, CancellationToken cancellationToken = default)
    {
        var config = await extractionConfigRepository.GetByIdAsync(command.ExtractionConfigId, cancellationToken)
            ?? throw new ExtractionConfigNotFoundException(command.ExtractionConfigId);

        var history = new ExtractionHistory(DateTimeOffset.UtcNow, command.SourceFileName);
        await extractionHistoryRepository.AddAsync(history, cancellationToken);

        try
        {
            var extractionResult = excelExtractionService.Extract(command.FileStream, config);
            var generatedStream = excelGeneratorService.Generate(extractionResult);
            var generatedFileName = BuildGeneratedFileName(command.SourceFileName);

            var storedPath = await fileStorageService.SaveAsync(generatedStream, generatedFileName, cancellationToken);
            generatedStream.Position = 0;

            await extractionHistoryRepository.MarkCompletedAsync(history.Id, storedPath, cancellationToken);

            return new ProcessExcelFileResult(generatedStream, generatedFileName);
        }
        catch
        {
            await extractionHistoryRepository.MarkFailedAsync(history.Id, cancellationToken);
            throw;
        }
    }

    private static string BuildGeneratedFileName(string sourceFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{baseName}-processed-{timestamp}.xlsx";
    }
}
