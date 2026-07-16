using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

// Shared by every repeating-block reader (the generic RepeatingBlockReader, and
// ProcedureExtractionService's own hand-rolled loop -- see its comment for why PROCEDURE can't use
// RepeatingBlockReader directly). Extracted here rather than duplicated, since the range math itself
// (not the per-field blank/required policy) is identical for every sheet.
public static class BlockFieldRangeCalculator
{
    public static string BuildRange(BlockFieldDefinition field, int blockStartRow)
    {
        var (startColumn, endColumn) = SplitColumnRange(field.ColumnRange);
        var start = $"{startColumn}{blockStartRow + field.RowOffsetStart}";
        var end = $"{endColumn}{blockStartRow + field.RowOffsetEnd}";
        return start == end ? start : $"{start}:{end}";
    }

    private static (string Start, string End) SplitColumnRange(string columnRange)
    {
        var parts = columnRange.Split(':');
        return parts.Length == 1 ? (parts[0], parts[0]) : (parts[0], parts[1]);
    }
}
