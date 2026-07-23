using ExcelETL.Application.Generation;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Resolves both profiles, runs the import + generation pipeline, archives the generated workbook
// (IFileStorageService), and turns the whole-file-rejection case (ImportResult.Equipement is null,
// model doc §3.1) into a distinguishable result rather than an exception, so the WebAPI controller
// can return 422 instead of a 200 with an empty body.
public sealed class ProcessOxoFileService(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore,
    IImportPipelineOrchestrator importPipelineOrchestrator,
    ISheetGenerationEngine sheetGenerationEngine,
    IWorkbookWriter workbookWriter,
    IFileStorageService fileStorageService,
    ILogger<ProcessOxoFileService> logger) : IProcessOxoFileService
{
    public async Task<ProcessOxoFileResult> ProcessAsync(
        ProcessOxoFileCommand command, CancellationToken cancellationToken = default)
    {
        var importProfile = await importProfileStore.GetByIdAsync(command.ImportProfileId, cancellationToken)
            ?? throw new ImportProfileNotFoundException(command.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(command.ExportProfileId, cancellationToken)
            ?? throw new ExportProfileNotFoundException(command.ExportProfileId);

        logger.LogInformation(
            "Starting OXO processing for source file {SourceFileName} (import profile {ImportProfileId}, " +
            "export profile {ExportProfileId})",
            command.SourceFileName, command.ImportProfileId, command.ExportProfileId);

        try
        {
            var importResult = importPipelineOrchestrator.Run(command.WorkbookReader, importProfile);

            if (importResult.Equipement is null)
            {
                logger.LogWarning(
                    "OXO processing rejected source file {SourceFileName}: {ErrorCount} blocking error(s)",
                    command.SourceFileName, importResult.Errors.Count);
                return new ProcessOxoFileResult(importResult, null, null);
            }

            var generatedWorkbook = sheetGenerationEngine.Generate(importResult, exportProfile);

            var generatedStream = new MemoryStream();
            workbookWriter.Write(generatedWorkbook, generatedStream);
            var generatedFileName = TargetWorkbookFileNameBuilder.Build(importResult.Equipement.Repere, DateTime.UtcNow);

            var storedPath = await fileStorageService.SaveAsync(generatedStream, generatedFileName, cancellationToken);
            generatedStream.Position = 0;

            logger.LogInformation(
                "Completed OXO processing for source file {SourceFileName}: generated {GeneratedFileName} archived at " +
                "{StoredPath}",
                command.SourceFileName, generatedFileName, storedPath);

            return new ProcessOxoFileResult(importResult, generatedStream, generatedFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OXO processing failed for source file {SourceFileName}", command.SourceFileName);
            throw;
        }
    }
}
