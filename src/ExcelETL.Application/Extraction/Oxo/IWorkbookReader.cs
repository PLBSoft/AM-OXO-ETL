namespace ExcelETL.Application.Extraction.Oxo;

// Abstraction over the source workbook, implemented with ClosedXML in Infrastructure (Lot E1).
// Kept intentionally minimal -- the engine (Lot B/C) never needs anything beyond a raw cell/range
// read. A missing sheet is signaled by ReadCellValue itself throwing
// WorksheetNotFoundInWorkbookException, not by a separate existence check (Lot 046 removed the
// never-called SheetExists after confirming no caller depended on it).
public interface IWorkbookReader
{
    string? ReadCellValue(string sheet, string range);
}
