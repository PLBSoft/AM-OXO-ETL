using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// WorkbookReader is passed in already constructed (unlike ProcessExcelFileCommand's raw Stream)
// because building one means instantiating ClosedXmlWorkbookReader, which lives in Infrastructure --
// Application cannot reference it. The caller (WebAPI host, same as every BlazorAdmin OXO page)
// constructs it from the uploaded file stream and owns its disposal.
//
// SourceFileContent (Lot 034) is the raw uploaded bytes, separate from WorkbookReader: the reader
// only exposes cell values, never the original bytes, but the archiving step (IGeneratedFileWriter,
// best-effort in ProcessOxoFileService) needs to persist the exact original file to disk. The
// controller buffers IFormFile.OpenReadStream() once into this byte[] and builds WorkbookReader from
// a fresh MemoryStream over the same bytes, rather than trying to re-read/rewind a single shared
// stream across both concerns.
public sealed record ProcessOxoFileCommand(
    Guid ImportProfileId, Guid ExportProfileId, IWorkbookReader WorkbookReader, string SourceFileName,
    byte[] SourceFileContent);

// GeneratedFileStream/GeneratedFileName are null exactly when ImportResult.Equipement is null --
// the whole-file-rejection case (model doc §3.1). No generation is attempted in that case.
public sealed record ProcessOxoFileResult(ImportResult ImportResult, Stream? GeneratedFileStream, string? GeneratedFileName);
