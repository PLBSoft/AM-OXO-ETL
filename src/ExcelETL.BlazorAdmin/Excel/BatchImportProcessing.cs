using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Excel;
using Microsoft.AspNetCore.Components.Forms;

namespace ExcelETL.BlazorAdmin.Excel;

// Lot 033 (33.4): the import step (open stream, buffer, read workbook, run the pipeline, classify
// the outcome) is byte-for-byte identical between ImportProfileTest.razor and ExportProfileTest.razor
// -- ExportProfileTest.razor just needs a generation step afterwards. Factored here rather than
// duplicated: it doesn't complicate either page (each replaces ~25 lines with one call) and keeps a
// future fix to this step (see BrowserFileStreamBuffering's own history) from having to land twice.
// The summary/badge/accordion rendering logic stays page-local (see the ticket's own 33.4 note) --
// it's tied to each page's own IStringLocalizer keys (ImportProfileTest_Status* vs
// ExportProfileTest_Status*, deliberately not shared per this project's per-page resx convention) and
// diverges enough in per-file content (subsections vs. generation/download) that a shared abstraction
// would be more complex than the duplication it replaces.
public static class BatchImportProcessing
{
    public sealed record Result(string FileName, BatchFileStatus Status, ImportResult? ImportResult, string? TechnicalErrorMessage);

    public static async Task<Result> ProcessAsync(
        IBrowserFile file,
        ImportProfile profile,
        IImportPipelineOrchestrator orchestrator,
        BusinessExceptionLocalizer localizer)
    {
        Stream? fileStream = null;

        try
        {
            fileStream = file.OpenReadStream(BatchUploadLimits.MaxFileSizeBytes);
            await using var buffered = await BrowserFileStreamBuffering.BufferToSeekableStreamAsync(fileStream);
            using var workbookReader = new ClosedXmlWorkbookReader(buffered);
            var result = orchestrator.Run(workbookReader, profile);

            var status = result.Equipement is null
                ? BatchFileStatus.Rejected
                : result.Errors.Count > 0 ? BatchFileStatus.Warning : BatchFileStatus.Ok;

            return new Result(file.Name, status, result, null);
        }
        catch (Exception ex)
        {
            return new Result(file.Name, BatchFileStatus.TechnicalError, null, localizer.TryLocalize(ex) ?? ex.Message);
        }
        finally
        {
            if (fileStream is not null)
            {
                await fileStream.DisposeAsync();
            }
        }
    }
}
