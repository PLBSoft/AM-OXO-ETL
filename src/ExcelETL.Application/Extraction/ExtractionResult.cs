using ExcelETL.Domain.Enums;

namespace ExcelETL.Application.Extraction;

public sealed record ExtractedValue(string TargetPropertyName, object? Value, CellDataType DataType);

public sealed record ExtractedSheet(string SheetName, IReadOnlyList<ExtractedValue> Values);

public sealed record ExtractionResult(IReadOnlyList<ExtractedSheet> Sheets)
{
    public ExtractedValue GetValue(string sheetName, string targetPropertyName)
    {
        var sheet = Sheets.FirstOrDefault(s => s.SheetName == sheetName)
            ?? throw new KeyNotFoundException($"Sheet '{sheetName}' was not found in the extraction result.");

        return sheet.Values.FirstOrDefault(v => v.TargetPropertyName == targetPropertyName)
            ?? throw new KeyNotFoundException(
                $"Property '{targetPropertyName}' was not found on sheet '{sheetName}' in the extraction result.");
    }
}
