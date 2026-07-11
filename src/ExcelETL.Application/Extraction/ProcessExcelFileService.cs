using ExcelETL.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction;

public sealed class ProcessExcelFileService(
    IExtractionConfigRepository extractionConfigRepository,
    IExcelExtractionService excelExtractionService,
    IExcelGeneratorService excelGeneratorService,
    IFileStorageService fileStorageService,
    IExtractionHistoryRepository extractionHistoryRepository,
    ILogger<ProcessExcelFileService> logger) : IProcessExcelFileService
{
    public async Task<ProcessExcelFileResult> ProcessAsync(
        ProcessExcelFileCommand command, CancellationToken cancellationToken = default)
    {
        var config = await extractionConfigRepository.GetByIdAsync(command.ExtractionConfigId, cancellationToken)
            ?? throw new ExtractionConfigNotFoundException(command.ExtractionConfigId);

        var history = new ExtractionHistory(DateTimeOffset.UtcNow, command.SourceFileName);
        await extractionHistoryRepository.AddAsync(history, cancellationToken);

        logger.LogInformation(
            "Starting extraction {HistoryId} for source file {SourceFileName} using config {ExtractionConfigId}",
            history.Id, command.SourceFileName, command.ExtractionConfigId);

        try
        {
            var extractionResult = excelExtractionService.Extract(command.FileStream, config);
            var generatedStream = excelGeneratorService.Generate(extractionResult);
            var generatedFileName = BuildGeneratedFileName(command.SourceFileName);

            var storedPath = await fileStorageService.SaveAsync(generatedStream, generatedFileName, cancellationToken);
            generatedStream.Position = 0;

            await extractionHistoryRepository.MarkCompletedAsync(history.Id, storedPath, cancellationToken);

            logger.LogInformation(
                "Completed extraction {HistoryId}: generated {GeneratedFileName} archived at {StoredPath}",
                history.Id, generatedFileName, storedPath);

            return new ProcessExcelFileResult(generatedStream, generatedFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Extraction {HistoryId} failed for source file {SourceFileName}",
                history.Id, command.SourceFileName);

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
