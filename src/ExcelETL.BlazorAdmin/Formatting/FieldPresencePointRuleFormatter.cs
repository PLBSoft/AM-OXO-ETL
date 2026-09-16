using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.BlazorAdmin.Formatting;

// Shared by SheetRuleForm's editable list and ImportProfileEditor's read-only card summary, so both
// show a field-presence rule's cell the same way: the absolute range of the first block, followed by
// the expected text when one is configured (e.g. H19:N19 = "DEBUT MAD").
public static class FieldPresencePointRuleFormatter
{
    public static string FormatCell(FieldPresencePointRule rule, int firstBlockStartRow)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var range = BlockFieldRangeFormatter.ToAbsoluteRange(
            firstBlockStartRow, rule.Cell.ColumnRange, rule.Cell.RowOffsetStart, rule.Cell.RowOffsetEnd);

        return rule.ExpectedValue is null ? range : $"{range} = \"{rule.ExpectedValue}\"";
    }
}
