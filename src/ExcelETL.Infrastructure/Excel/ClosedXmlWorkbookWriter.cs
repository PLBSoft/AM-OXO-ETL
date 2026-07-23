using System.Diagnostics;
using ClosedXML.Excel;
using ExcelETL.Application.Generation;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Infrastructure.Excel;

// Builds a fresh XLWorkbook from the intermediate GeneratedWorkbook structure and saves it into the
// caller-provided stream. Sheets are added in GeneratedWorkbook.Sheets order (already the
// ExportProfile's order); headers go on row 1, data rows start on row 2.
//
// ILogger<T> injected/logged the same way as ImportPipelineOrchestrator (Lot G1/G2).
public sealed class ClosedXmlWorkbookWriter(ILogger<ClosedXmlWorkbookWriter> logger) : IWorkbookWriter
{
    public void Write(GeneratedWorkbook workbook, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(destination);

        logger.LogInformation("Starting workbook write for {SheetCount} sheet(s)", workbook.Sheets.Count);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var xlWorkbook = new XLWorkbook();

            foreach (var sheet in workbook.Sheets)
            {
                var worksheet = xlWorkbook.Worksheets.Add(sheet.Name);

                for (var columnIndex = 0; columnIndex < sheet.Headers.Count; columnIndex++)
                {
                    worksheet.Cell(1, columnIndex + 1).Value = sheet.Headers[columnIndex];
                }

                for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
                {
                    var cells = sheet.Rows[rowIndex].Cells;
                    for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
                    {
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = cells[columnIndex];
                    }
                }
            }

            xlWorkbook.SaveAs(destination);

            if (destination.CanSeek)
            {
                destination.Position = 0;
            }

            logger.LogInformation(
                "Completed workbook write in {ElapsedMs}ms: {SheetCount} sheet(s)",
                stopwatch.ElapsedMilliseconds, workbook.Sheets.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workbook write failed unexpectedly after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
