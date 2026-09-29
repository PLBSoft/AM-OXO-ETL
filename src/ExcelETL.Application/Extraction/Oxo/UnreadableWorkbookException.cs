using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// Thrown when a file is a real .xlsx package but the Excel library fails while loading it (seen in
// production: ClosedXML's "Sequence contains no matching element" on an unusual cell note). A file
// that isn't an Excel package at all keeps throwing System.IO.FileFormatException instead (400 on
// POST /api/oxo/process, Lot 036.2). Both ProcessOxoFileService and BlazorAdmin's test pages
// (BatchImportProcessing) turn it into a whole-file rejection via ToRejectedImportResult, so the
// reason reaches the processing report and the M2M caller with one wording; it never reaches
// GlobalExceptionHandler.
public sealed class UnreadableWorkbookException(Exception innerException)
    : Exception($"The workbook could not be loaded: {innerException.Message}", innerException)
{
    // Shown to the end user (legacy app, /generated-files, test pages): what to do first, the
    // library's own message last for support.
    public ImportResult ToRejectedImportResult(string sourceFileName) =>
        new(null, [], [], [],
        [
            new ExtractionError(
                "Classeur",
                sourceFileName,
                ExtractionErrorCode.UnreadableWorkbook,
                "Le fichier n'a pas pu être lu : il contient un élément que le service ne sait pas ouvrir. " +
                "Ouvrez-le dans Excel, enregistrez-le de nouveau au format .xlsx puis relancez l'import. " +
                $"Détail technique : {InnerException!.Message}")
        ]);
}
