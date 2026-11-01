namespace ExcelETL.Application.Plans;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, decisions D7/D10): prints one plan sheet
// of a source workbook to PDF, one PDF per non-empty page. Returns null when the workbook has no such
// sheet, and an empty page list when the sheet holds no plan. Throws when rendering fails: the caller
// decides how much that matters (best-effort, D11). The LibreOffice implementation lives in
// Infrastructure; another engine (e.g. Syncfusion XlsIO) could replace it without touching callers.
public interface IPlanSheetPdfRenderer
{
    Task<RenderedPlanSheet?> RenderAsync(byte[] workbook, string planSheetName, CancellationToken cancellationToken = default);
}

// SheetName is the sheet's own name in the workbook (it may differ from the profile's in case or spaces).
public sealed record RenderedPlanSheet(string SheetName, IReadOnlyList<byte[]> Pages);
