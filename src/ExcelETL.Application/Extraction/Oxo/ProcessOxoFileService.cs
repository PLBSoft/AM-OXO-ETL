using ExcelETL.Application.Archiving;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Resolves both profiles, runs the import + generation pipeline, archives the generated workbook
// (IFileStorageService), and turns the whole-file-rejection case (ImportResult.Equipement is null,
// model doc §3.1) into a distinguishable result rather than an exception, so the WebAPI controller
// can return 422 instead of a 200 with an empty body.
//
// Lot 034: additionally archives BOTH the source and target files (IGeneratedFileWriter) plus their
// searchable metadata (IGeneratedFileArchiveStore, GeneratedFileRecord), systematically -- including
// when the file is rejected, per the client's own "proof the source data was corrupt" use case (see
// docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md). This is deliberately a SECOND,
// independent archiving mechanism from the pre-existing IFileStorageService.SaveAsync call below
// (Lot K) -- IFileStorageService only ever archived the target on success, flat, with no metadata and
// no source file, and an existing WebAPI integration test (OxoProcessEndpointTests) already asserts
// on its exact single-flat-file behavior, so it is left untouched rather than folded into the new
// mechanism.
public sealed class ProcessOxoFileService(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore,
    IImportPipelineOrchestrator importPipelineOrchestrator,
    ISheetGenerationEngine sheetGenerationEngine,
    IWorkbookWriter workbookWriter,
    IFileStorageService fileStorageService,
    IGeneratedFileWriter generatedFileWriter,
    IGeneratedFileArchiveStore generatedFileArchiveStore,
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
            var archivedAtUtc = DateTime.UtcNow;

            if (importResult.Equipement is null)
            {
                logger.LogWarning(
                    "OXO processing rejected source file {SourceFileName}: {ErrorCount} blocking error(s)",
                    command.SourceFileName, importResult.Errors.Count);

                await TryArchiveAsync(
                    command, importProfile.Id, exportProfile.Id, importResult, null, null, archivedAtUtc, cancellationToken);

                return new ProcessOxoFileResult(importResult, null, null);
            }

            var generatedWorkbook = sheetGenerationEngine.Generate(importResult, exportProfile);

            var generatedStream = new MemoryStream();
            workbookWriter.Write(generatedWorkbook, generatedStream);
            var generatedFileName = TargetWorkbookFileNameBuilder.Build(importResult.Equipement.Repere, DateTime.UtcNow);

            generatedStream.Position = 0;
            var storedPath = await fileStorageService.SaveAsync(generatedStream, generatedFileName, cancellationToken);
            generatedStream.Position = 0;

            logger.LogInformation(
                "Completed OXO processing for source file {SourceFileName}: generated {GeneratedFileName} archived at " +
                "{StoredPath}",
                command.SourceFileName, generatedFileName, storedPath);

            await TryArchiveAsync(
                command, importProfile.Id, exportProfile.Id, importResult, generatedStream, generatedFileName,
                archivedAtUtc, cancellationToken);
            generatedStream.Position = 0;

            return new ProcessOxoFileResult(importResult, generatedStream, generatedFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OXO processing failed for source file {SourceFileName}", command.SourceFileName);
            throw;
        }
    }

    // Best-effort, deliberately isolated from the main try/catch above: a disk-full or database-down
    // failure here must never fail the HTTP response that already has a valid result to return (see
    // the ticket's 34.4 -- archiving is a side effect, not a transactional guarantee of the main flow).
    private async Task TryArchiveAsync(
        ProcessOxoFileCommand command,
        Guid importProfileId,
        Guid exportProfileId,
        ImportResult importResult,
        Stream? generatedContent,
        string? generatedFileName,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            string sourceFilePath;
            using (var sourceStream = new MemoryStream(command.SourceFileContent))
            {
                sourceFilePath = await generatedFileWriter.WriteSourceAsync(
                    sourceStream, command.SourceFileName, timestampUtc, cancellationToken);
            }

            string? targetFilePath = null;
            if (generatedContent is not null && generatedFileName is not null)
            {
                generatedContent.Position = 0;
                targetFilePath = await generatedFileWriter.WriteTargetAsync(
                    generatedContent, command.SourceFileName, timestampUtc, cancellationToken);
                generatedContent.Position = 0;
            }

            var status = importResult.Equipement is null
                ? GeneratedFileArchiveStatus.Rejected
                : importResult.HasErrors
                    ? GeneratedFileArchiveStatus.NonBlockingWarning
                    : GeneratedFileArchiveStatus.Success;

            var record = new GeneratedFileRecord(
                Guid.NewGuid(),
                timestampUtc,
                importResult.Equipement?.Repere,
                command.SourceFileName,
                sourceFilePath,
                generatedFileName,
                targetFilePath,
                importProfileId,
                exportProfileId,
                status);

            await generatedFileArchiveStore.SaveAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Failed to archive generated files for source file {SourceFileName} -- best-effort, HTTP " +
                "response unaffected", command.SourceFileName);
        }
    }
}
