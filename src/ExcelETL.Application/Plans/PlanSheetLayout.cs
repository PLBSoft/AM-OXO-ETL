namespace ExcelETL.Application.Plans;

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md, 091.3): what PlanPageDetector needs to
// know about one plan sheet, read from the workbook by Infrastructure. Rows and columns are 1-based.
public sealed record PlanSheetLayout(
    IReadOnlyList<PlanSheetAnchor> Anchors,
    // Right-most column of the merged cells in rows 1-4: the page width drawn by the client (cartouche).
    int HeaderLastColumn,
    // From the sheet's used range (<dimension>).
    int LastUsedRow,
    int LastUsedColumn);

public enum PlanAnchorKind
{
    Picture,
    Shape,
}

// One drawing anchored on the sheet. ImageKey identifies a picture's content (two pictures showing the
// same image share it); null for a shape.
public sealed record PlanSheetAnchor(PlanAnchorKind Kind, int FromRow, int ToRow, int ToColumn, string? ImageKey = null);

// Rows of one page to print, between FirstRow and LastRow included.
public sealed record PlanPage(int FirstRow, int LastRow);

// What to print: the non-empty pages, all printed from column A to LastColumn.
public sealed record PlanSheetPrintLayout(IReadOnlyList<PlanPage> Pages, int LastColumn);
