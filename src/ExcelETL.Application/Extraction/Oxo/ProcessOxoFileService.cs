using ExcelETL.Application.Archiving;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Archiving;
using ExcelETL.Domain.Extraction.Pivot;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Resolves both profiles, runs the import + generation pipeline, archives the generated workbook,
// and turns the whole-file-rejection case (ImportResult.Equipement is null, model doc §3.1) into a
// distinguishable result rather than an exception, so the WebAPI controller can return 422 instead
// of a 200 with an empty body.
//
// Archives BOTH the source and target files (IGeneratedFileWriter) plus their searchable metadata
// (IGeneratedFileArchiveStore, GeneratedFileRecord), systematically -- including when the file is
// rejected, per the client's own "proof the source data was corrupt" use case (Lot 034, see
// docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md). This is the sole archiving mechanism
// since Lot 046 removed the older, redundant IFileStorageService (Lot K) -- that mechanism only ever
// archived the target on success, flat, with no metadata and no source file; Lot 034 covers strictly
// more, so Simon confirmed the Lot K mechanism could be retired outright rather than kept alongside it.
public sealed class ProcessOxoFileService(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore,
    IImportPipelineOrchestrator importPipelineOrchestrator,
    ISheetGenerationEngine sheetGenerationEngine,
    IWorkbookWriter workbookWriter,
    IGeneratedFileWriter generatedFileWriter,
    IGeneratedFileArchiveStore generatedFileArchiveStore,
    IWorkbookReaderFactory workbookReaderFactory,
    IGeneratedFilePurger generatedFilePurger,
    ILogger<ProcessOxoFileService> logger) : IProcessOxoFileService
{
    public async Task<ProcessOxoFileResult> ProcessAsync(
        ProcessOxoFileCommand command, CancellationToken cancellationToken = default)
    {
        await TryPurgeExpiredFilesAsync(cancellationToken);

        var importProfile = await importProfileStore.GetByIdAsync(command.ImportProfileId, cancellationToken)
            ?? throw new ImportProfileNotFoundException(command.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(command.ExportProfileId, cancellationToken)
            ?? throw new ExportProfileNotFoundException(command.ExportProfileId);

        logger.LogInformation(
            "Starting OXO processing for source file {SourceFileName} (import profile {ImportProfileId}, " +
            "export profile {ExportProfileId})",
            command.SourceFileName, command.ImportProfileId, command.ExportProfileId);

        // Opened outside the try below: FileFormatException (not an Excel package) propagates to the
        // caller unlogged here, exactly as when the controller used to open the file (Lot 036.2).
        IWorkbookReader workbookReader;
        try
        {
            workbookReader = workbookReaderFactory.Open(command.SourceFileContent);
        }
        catch (UnreadableWorkbookException ex)
        {
            logger.LogWarning(
                ex, "OXO processing rejected source file {SourceFileName}: the workbook could not be loaded",
                command.SourceFileName);

            var unreadableResult = ex.ToRejectedImportResult(command.SourceFileName);
            var unreadableRecordId = await TryArchiveAsync(
                command, importProfile.Id, exportProfile.Id, unreadableResult, null, null, DateTime.UtcNow, cancellationToken);

            return new ProcessOxoFileResult(unreadableResult, null, null, unreadableRecordId);
        }

        using var _ = workbookReader as IDisposable;

        try
        {
            var importResult = importPipelineOrchestrator.Run(workbookReader, importProfile);
            var archivedAtUtc = DateTime.UtcNow;

            if (importResult.Equipement is null)
            {
                logger.LogWarning(
                    "OXO processing rejected source file {SourceFileName}: {ErrorCount} blocking error(s)",
                    command.SourceFileName, importResult.Errors.Count);

                var rejectedRecordId = await TryArchiveAsync(
                    command, importProfile.Id, exportProfile.Id, importResult, null, null, archivedAtUtc, cancellationToken);

                return new ProcessOxoFileResult(importResult, null, null, rejectedRecordId);
            }

            var generatedWorkbook = sheetGenerationEngine.Generate(importResult, exportProfile);

            var generatedStream = new MemoryStream();
            workbookWriter.Write(generatedWorkbook, generatedStream);
            var generatedFileName = TargetWorkbookFileNameBuilder.Build(importResult.Equipement.Repere, DateTime.UtcNow);

            generatedStream.Position = 0;

            logger.LogInformation(
                "Completed OXO processing for source file {SourceFileName}: generated {GeneratedFileName}",
                command.SourceFileName, generatedFileName);

            var archivedRecordId = await TryArchiveAsync(
                command, importProfile.Id, exportProfile.Id, importResult, generatedStream, generatedFileName,
                archivedAtUtc, cancellationToken);
            generatedStream.Position = 0;

            return new ProcessOxoFileResult(importResult, generatedStream, generatedFileName, archivedRecordId);
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
    // Returns the archived record's own Guid (Lot 072) so the caller can point an M2M client at
    // GET /api/generated-files/{id} for the full warning detail -- null exactly when archiving
    // itself failed, never a placeholder value.
    private async Task<Guid?> TryArchiveAsync(
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

            var warnings = importResult.Errors
                .Select(e => new GeneratedFileWarning(e.Sheet, e.BlockIdentifier, e.Code.ToString(), e.Message, e.ExtractedValue))
                .ToList();

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
                status,
                command.Username,
                isolementCount: importResult.Isolements.Count,
                pointCount: importResult.Points.Count,
                tacheMultipleCount: importResult.TachesMultiples.Count,
                warnings: warnings);

            await generatedFileArchiveStore.SaveAsync(record, cancellationToken);
            return record.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Failed to archive generated files for source file {SourceFileName} -- best-effort, HTTP " +
                "response unaffected", command.SourceFileName);
            return null;
        }
    }

    // Lot 090: runs at the start of every import, like the legacy app's own purge. Best-effort -- a
    // purge failure is logged and never blocks (or fails) the import it happens to ride on.
    private async Task TryPurgeExpiredFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await generatedFilePurger.PurgeExpiredAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Archive retention purge failed -- skipped, import unaffected");
        }
    }
}
