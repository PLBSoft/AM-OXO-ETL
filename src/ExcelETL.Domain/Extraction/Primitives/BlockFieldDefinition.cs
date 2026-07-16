using System.Text.RegularExpressions;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// A single field read within a repeating block, relative to the block's start row (see RepeatingBlockLocator).
public sealed partial record BlockFieldDefinition
{
    public string Name { get; }
    public string ColumnRange { get; }
    public int RowOffsetStart { get; }
    public int RowOffsetEnd { get; }

    public BlockFieldDefinition(string name, string columnRange, int rowOffsetStart, int rowOffsetEnd)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.BlockFieldDefinition_EmptyName);
        }

        if (string.IsNullOrWhiteSpace(columnRange) || !ExcelColumnRangePattern().IsMatch(columnRange))
        {
            throw new DomainValidationException(
                "Column range must be a valid Excel column letter (e.g. 'B') or column range (e.g. 'B:E').",
                nameof(columnRange),
                DomainErrorCode.BlockFieldDefinition_InvalidColumnRange);
        }

        if (rowOffsetEnd < rowOffsetStart)
        {
            throw new DomainValidationException(
                "Row offset end must not be before row offset start.",
                nameof(rowOffsetEnd),
                DomainErrorCode.BlockFieldDefinition_RowOffsetEndBeforeStart);
        }

        Name = name;
        ColumnRange = columnRange;
        RowOffsetStart = rowOffsetStart;
        RowOffsetEnd = rowOffsetEnd;
    }

    [GeneratedRegex(@"^[A-Z]{1,3}(:[A-Z]{1,3})?$")]
    private static partial Regex ExcelColumnRangePattern();
}
