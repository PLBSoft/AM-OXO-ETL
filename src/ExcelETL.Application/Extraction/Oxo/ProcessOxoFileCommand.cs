using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// WorkbookReader is passed in already constructed (unlike ProcessExcelFileCommand's raw Stream)
// because building one means instantiating ClosedXmlWorkbookReader, which lives in Infrastructure --
// Application cannot reference it. The caller (WebAPI host, same as every BlazorAdmin OXO page)
// constructs it from the uploaded file stream and owns its disposal.
public sealed record ProcessOxoFileCommand(
    Guid ImportProfileId, Guid ExportProfileId, IWorkbookReader WorkbookReader, string SourceFileName);

// GeneratedFileStream/GeneratedFileName are null exactly when ImportResult.Equipement is null --
// the whole-file-rejection case (model doc §3.1). No generation is attempted in that case.
public sealed record ProcessOxoFileResult(ImportResult ImportResult, Stream? GeneratedFileStream, string? GeneratedFileName);
