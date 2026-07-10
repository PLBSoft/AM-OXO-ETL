using ClosedXML.Excel;
using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;

namespace ExcelETL.Infrastructure.Excel;

public class ClosedXmlExtractionService : IExcelExtractionService
{
    public ExtractionResult Extract(Stream excelFileStream, ExtractionConfig config)
    {
        ArgumentNullException.ThrowIfNull(excelFileStream);
        ArgumentNullException.ThrowIfNull(config);

        using var workbook = new XLWorkbook(excelFileStream);
        var sheets = config.Sheets
            .OrderBy(sheetConfig => sheetConfig.SheetIndex)
            .Select(sheetConfig => ExtractSheet(workbook, sheetConfig))
            .ToList();

        return new ExtractionResult(sheets);
    }

    private static ExtractedSheet ExtractSheet(XLWorkbook workbook, SheetConfig sheetConfig)
    {
        if (!workbook.Worksheets.TryGetWorksheet(sheetConfig.SheetName, out var worksheet))
        {
            throw new InvalidOperationException(
                $"Worksheet '{sheetConfig.SheetName}' was not found in the uploaded workbook.");
        }

        var values = sheetConfig.CellMappings
            .Select(mapping => ExtractValue(worksheet, mapping))
            .ToList();

        return new ExtractedSheet(sheetConfig.SheetName, values);
    }

    private static ExtractedValue ExtractValue(IXLWorksheet worksheet, CellMapping mapping)
    {
        var cell = worksheet.Cell(TopLeftCellAddress(mapping.SourceCell));
        var value = ReadCellValue(cell, mapping.DataType);
        return new ExtractedValue(mapping.TargetPropertyName, value, mapping.DataType);
    }

    private static string TopLeftCellAddress(string sourceCell)
    {
        var colonIndex = sourceCell.IndexOf(':');
        return colonIndex >= 0 ? sourceCell[..colonIndex] : sourceCell;
    }

    private static object? ReadCellValue(IXLCell cell, CellDataType dataType) => dataType switch
    {
        CellDataType.Text => cell.GetString(),
        CellDataType.Number => cell.GetValue<double>(),
        CellDataType.Decimal => cell.GetValue<decimal>(),
        CellDataType.Date => cell.GetDateTime(),
        CellDataType.Boolean => cell.GetBoolean(),
        _ => throw new NotSupportedException($"Unsupported cell data type '{dataType}'.")
    };
}
