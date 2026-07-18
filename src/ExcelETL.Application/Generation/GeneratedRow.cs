namespace ExcelETL.Application.Generation;

// One data row of a GeneratedSheet -- Cells align 1:1, in order, with the sheet's Headers (descriptive
// columns first, then Point columns, matching SheetGenerationRule's own ordering). An empty string
// means a blank cell (ColumnDefinition.Source = null, or no matching Point for this row).
public sealed record GeneratedRow(IReadOnlyList<string> Cells);
