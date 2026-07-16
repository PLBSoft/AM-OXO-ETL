namespace ExcelETL.Application.Extraction.Oxo;

// Abstraction over the source workbook, implemented with ClosedXML in Infrastructure (Lot E1).
// Kept intentionally minimal -- the engine (Lot B/C) never needs anything beyond a raw cell/range
// read and a sheet-existence check.
public interface IWorkbookReader
{
    string? ReadCellValue(string sheet, string range);

    bool SheetExists(string sheet);
}
