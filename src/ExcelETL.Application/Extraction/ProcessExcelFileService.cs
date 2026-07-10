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
        await extractionHistoryRepository.SaveChangesAsync(cancellationToken);

        try
        {
            var extractionResult = excelExtractionService.Extract(command.FileStream, config);
            var generatedStream = excelGeneratorService.Generate(extractionResult);
            var generatedFileName = BuildGeneratedFileName(command.SourceFileName);

            var storedPath = await fileStorageService.SaveAsync(generatedStream, generatedFileName, cancellationToken);
            generatedStream.Position = 0;

            history.MarkCompleted(storedPath);
            await extractionHistoryRepository.SaveChangesAsync(cancellationToken);

            return new ProcessExcelFileResult(generatedStream, generatedFileName);
        }
        catch
        {
            history.MarkFailed();
            await extractionHistoryRepository.SaveChangesAsync(cancellationToken);
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
