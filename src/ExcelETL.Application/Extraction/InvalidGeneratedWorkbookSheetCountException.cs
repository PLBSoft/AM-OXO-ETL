using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction;

// Derives from InvalidOperationException -- see SheetNotFoundInExtractionConfigException for why.
public sealed class InvalidGeneratedWorkbookSheetCountException(int sheetCount, int minSheets, int maxSheets)
    : InvalidOperationException($"Generated workbook must contain {minSheets}-{maxSheets} sheets; got {sheetCount}.")
{
    public int SheetCount { get; } = sheetCount;

    public int MinSheets { get; } = minSheets;

    public int MaxSheets { get; } = maxSheets;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.GeneratedWorkbookSheetCountOutOfRange;

    public IReadOnlyList<object?> Args => [MinSheets, MaxSheets, SheetCount];
}
