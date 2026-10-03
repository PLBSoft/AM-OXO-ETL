using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// SourceFileContent (Lot 034) is the raw uploaded bytes. ProcessOxoFileService opens them itself
// through IWorkbookReaderFactory (so a load failure becomes a reported rejection) and archives them
// as-is.
// Username (optional, last): the M2M caller's own end-user, when supplied -- see
// GeneratedFileRecord.Username for the full rationale (best-effort traceability, not a business
// invariant). Origin (optional, last): the calling environment, same treatment -- see
// GeneratedFileRecord.Origin.
public sealed record ProcessOxoFileCommand(
    Guid ImportProfileId, Guid ExportProfileId, string SourceFileName,
    byte[] SourceFileContent, string? Username = null, string? Origin = null);

// GeneratedFileStream/GeneratedFileName are null exactly when ImportResult.Equipement is null --
// the whole-file-rejection case (model doc §3.1). No generation is attempted in that case.
//
// ArchivedRecordId (Lot 072) is the Guid minted for the GeneratedFileRecord archived alongside
// this run (source + target files, metadata, ImportResult.Errors as GeneratedFileWarnings) --
// null exactly when archiving itself failed (best-effort, see ProcessOxoFileService.TryArchiveAsync)
// or has not run yet, never a placeholder Guid. Lets the WebAPI controller point a caller at
// GET /api/generated-files/{id} for the full warning detail without inventing a second
// correlation mechanism.
public sealed record ProcessOxoFileResult(
    ImportResult ImportResult, Stream? GeneratedFileStream, string? GeneratedFileName, Guid? ArchivedRecordId = null);
