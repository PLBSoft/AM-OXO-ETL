using ClosedXML.Excel;
using ExcelETL.Application.Extraction;
using ExcelETL.Domain.Entities;
using ExcelETL.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Infrastructure.Excel;

public class ClosedXmlExtractionService(ILogger<ClosedXmlExtractionService> logger) : IExcelExtractionService
{
    public ExtractionResult Extract(Stream excelFileStream, ExtractionConfig config)
    {
        ArgumentNullException.ThrowIfNull(excelFileStream);
        ArgumentNullException.ThrowIfNull(config);

        logger.LogInformation(
            "Extracting {SheetCount} sheet(s) for extraction config {ExtractionConfigId}",
            config.Sheets.Count, config.Id);

        using var workbook = new XLWorkbook(excelFileStream);
        var sheets = config.Sheets
            .OrderBy(sheetConfig => sheetConfig.SheetIndex)
            .Select(sheetConfig => ExtractSheet(workbook, sheetConfig))
            .ToList();

        return new ExtractionResult(sheets);
    }

    private ExtractedSheet ExtractSheet(XLWorkbook workbook, SheetConfig sheetConfig)
    {
        if (!workbook.Worksheets.TryGetWorksheet(sheetConfig.SheetName, out var worksheet))
        {
            throw new WorksheetNotFoundInWorkbookException(sheetConfig.SheetName);
        }

        logger.LogDebug(
            "Extracting sheet {SheetName} ({MappingCount} cell mapping(s))",
            sheetConfig.SheetName, sheetConfig.CellMappings.Count);

        var values = sheetConfig.CellMappings
            .Select(mapping => ExtractValue(worksheet, sheetConfig.SheetName, mapping))
            .ToList();

        return new ExtractedSheet(sheetConfig.SheetName, values);
    }

    private ExtractedValue ExtractValue(IXLWorksheet worksheet, string sheetName, CellMapping mapping)
    {
        var cell = worksheet.Cell(TopLeftCellAddress(mapping.SourceCell));
        var value = ReadCellValue(cell, mapping.DataType);

        logger.LogDebug(
            "Extracted {TargetPropertyName} = {Value} from {SheetName}!{SourceCell}",
            mapping.TargetPropertyName, value, sheetName, mapping.SourceCell);

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
