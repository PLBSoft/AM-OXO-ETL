using ExcelETL.Application.Exceptions;
using ExcelETL.Domain.Enums;

namespace ExcelETL.Application.Extraction;

public sealed record ExtractedValue(string TargetPropertyName, object? Value, CellDataType DataType);

public sealed record ExtractedSheet(string SheetName, IReadOnlyList<ExtractedValue> Values);

public sealed record ExtractionResult(IReadOnlyList<ExtractedSheet> Sheets)
{
    public ExtractedValue GetValue(string sheetName, string targetPropertyName)
    {
        var sheet = Sheets.FirstOrDefault(s => s.SheetName == sheetName)
            ?? throw new ExtractionResultLookupException(
                $"Sheet '{sheetName}' was not found in the extraction result.",
                ApplicationErrorCode.ExtractionResult_SheetNotFound,
                sheetName);

        return sheet.Values.FirstOrDefault(v => v.TargetPropertyName == targetPropertyName)
            ?? throw new ExtractionResultLookupException(
                $"Property '{targetPropertyName}' was not found on sheet '{sheetName}' in the extraction result.",
                ApplicationErrorCode.ExtractionResult_PropertyNotFound,
                targetPropertyName, sheetName);
    }
}
