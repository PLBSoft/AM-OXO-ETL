using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction;

// Derives from InvalidOperationException -- see SheetNotFoundInExtractionConfigException for why.
public sealed class WorksheetNotFoundInWorkbookException(string sheetName)
    : InvalidOperationException($"Worksheet '{sheetName}' was not found in the uploaded workbook."),
        IHasApplicationErrorCode
{
    public string SheetName { get; } = sheetName;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.WorksheetNotFoundInUploadedWorkbook;

    public IReadOnlyList<object?> Args => [SheetName];

    public string ResourceKey => ErrorCode.ToString();
}
