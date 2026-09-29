namespace ExcelETL.Application.Extraction.Oxo;

// Thrown when a file is a real .xlsx package but the Excel library fails while loading it (seen in
// production: ClosedXML's "Sequence contains no matching element" on an unusual cell note). A file
// that isn't an Excel package at all keeps throwing System.IO.FileFormatException instead (400 on
// POST /api/oxo/process, Lot 036.2). ProcessOxoFileService turns this one into a whole-file
// rejection (ExtractionErrorCode.UnreadableWorkbook) so the reason reaches the processing report and
// the M2M caller; it never reaches GlobalExceptionHandler.
public sealed class UnreadableWorkbookException(Exception innerException)
    : Exception($"The workbook could not be loaded: {innerException.Message}", innerException);
