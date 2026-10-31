namespace ExcelETL.WebAPI.Contracts;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, D14): one plan page printed to PDF.
// Index is the position in the record's plan list (0-based), used by DownloadUrl. No server path, same
// rule as GeneratedFileSummaryResponse. Available is false once the retention purge removed the file
// (or it is otherwise missing on disk); DownloadUrl then answers 404.
public sealed record GeneratedFilePlanResponse(
    int Index,
    string SheetName,
    int PageNumber,
    string FileName,
    long SizeBytes,
    bool Available,
    string DownloadUrl);
