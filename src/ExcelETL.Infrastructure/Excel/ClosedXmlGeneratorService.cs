using ClosedXML.Excel;
using ExcelETL.Application.Extraction;

namespace ExcelETL.Infrastructure.Excel;

public class ClosedXmlGeneratorService : IExcelGeneratorService
{
    private const int MinSheets = 4;
    private const int MaxSheets = 5;

    public Stream Generate(ExtractionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Sheets.Count is < MinSheets or > MaxSheets)
        {
            throw new InvalidOperationException(
                $"Generated workbook must contain {MinSheets}-{MaxSheets} sheets; got {result.Sheets.Count}.");
        }

        using var workbook = new XLWorkbook();
        foreach (var sheet in result.Sheets)
        {
            WriteSheet(workbook, sheet);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void WriteSheet(XLWorkbook workbook, ExtractedSheet sheet)
    {
        var worksheet = workbook.Worksheets.Add(sheet.SheetName);
        worksheet.Cell(1, 1).Value = "Property";
        worksheet.Cell(1, 2).Value = "Value";

        var row = 2;
        foreach (var value in sheet.Values)
        {
            worksheet.Cell(row, 1).Value = value.TargetPropertyName;
            SetValue(worksheet.Cell(row, 2), value.Value);
            row++;
        }
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                cell.Value = text;
                break;
            case double number:
                cell.Value = number;
                break;
            case decimal @decimal:
                cell.Value = @decimal;
                break;
            case DateTime date:
                cell.Value = date;
                break;
            case bool boolean:
                cell.Value = boolean;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }
}
