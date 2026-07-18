using ClosedXML.Excel;
using ExcelETL.Application.Generation;

namespace ExcelETL.Infrastructure.Excel;

// The Lot I4 implementation of IWorkbookWriter -- builds a fresh XLWorkbook from the intermediate
// GeneratedWorkbook structure (I3) and saves it into the caller-provided stream. Sheets are added in
// GeneratedWorkbook.Sheets order (already the ExportProfile's order); headers go on row 1, data rows
// start on row 2. New, independent of ClosedXmlGeneratorService (the ExtractionResult/ExtractedSheet
// POC generator) -- explicitly out of scope for reuse per this lot's ticket.
public sealed class ClosedXmlWorkbookWriter : IWorkbookWriter
{
    public void Write(GeneratedWorkbook workbook, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(destination);

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
    }
}
