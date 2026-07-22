using System.Text.RegularExpressions;

namespace ExcelETL.BlazorAdmin.Formatting;

// Pure presentation-layer conversion between BlockFieldDefinition's stored representation
// (ColumnRange + RowOffsetStart/RowOffsetEnd, relative to the owning SheetExtractionRule's
// FirstBlockStartRow) and the absolute Excel range (e.g. "B19:E20") a user reads by pointing a
// cell in the real source file. Domain deliberately keeps the relative representation (Step needs
// it to project onto every following block) -- only this Blazor-facing layer needs to translate,
// for both display and entry. See docs/tickets-tdd-blazor-profil-import-lisibilite-plages-excel.md.
public static partial class BlockFieldRangeFormatter
{
    // Level 1 (blocking, N1 §"Validation des bornes"): a workbook's real technical limits -- not a
    // business assumption, a coordinate outside these literally cannot exist in an .xlsx file.
    private const int MaxColumnNumber = 16384; // XFD
    private const int MaxRowNumber = 1_048_576;

    // Level 2 (non-blocking plausibility warning): an arbitrary business threshold based on the real
    // fixtures observed to date (rightmost column used: U: highest start row: 19) -- not a real Excel
    // limit, deliberately kept separate from the level-1 constants above so a future adjustment here
    // is never confused with a workbook-format constraint.
    private const int PracticalColumnThreshold = 52; // AZ
    private const int PracticalRowThreshold = 1000;

    public static string ToAbsoluteRange(int firstBlockStartRow, string columnRange, int rowOffsetStart, int rowOffsetEnd)
    {
        var (startColumn, endColumn) = SplitColumnRange(columnRange);
        var startRow = firstBlockStartRow + rowOffsetStart;
        var endRow = firstBlockStartRow + rowOffsetEnd;

        if (startColumn == endColumn && rowOffsetStart == rowOffsetEnd)
        {
            return $"{startColumn}{startRow}";
        }

        return $"{startColumn}{startRow}:{endColumn}{endRow}";
    }

    public static BlockFieldRangeParseResult FromAbsoluteRange(string absoluteRange, int firstBlockStartRow)
    {
        if (!TryParse(absoluteRange, out var startColumn, out var startRow, out var endColumn, out var endRow))
        {
            return BlockFieldRangeParseResult.Failure();
        }

        var startColumnNumber = ColumnToNumber(startColumn);
        var endColumnNumber = ColumnToNumber(endColumn);

        if (endRow < startRow || endColumnNumber < startColumnNumber)
        {
            return BlockFieldRangeParseResult.Failure();
        }

        if (startRow < 1 || endRow > MaxRowNumber || endColumnNumber > MaxColumnNumber)
        {
            return BlockFieldRangeParseResult.Failure();
        }

        var isBeyondPracticalRange = endColumnNumber > PracticalColumnThreshold || endRow > PracticalRowThreshold;

        var columnRange = startColumn == endColumn ? startColumn : $"{startColumn}:{endColumn}";
        var rowOffsetStart = startRow - firstBlockStartRow;
        var rowOffsetEnd = endRow - firstBlockStartRow;

        return BlockFieldRangeParseResult.Success(columnRange, rowOffsetStart, rowOffsetEnd, isBeyondPracticalRange);
    }

    private static bool TryParse(
        string absoluteRange, out string startColumn, out int startRow, out string endColumn, out int endRow)
    {
        startColumn = endColumn = string.Empty;
        startRow = endRow = 0;

        if (string.IsNullOrWhiteSpace(absoluteRange))
        {
            return false;
        }

        var trimmed = absoluteRange.Trim().ToUpperInvariant();

        var rangeMatch = FullRangePattern().Match(trimmed);
        if (rangeMatch.Success)
        {
            startColumn = rangeMatch.Groups[1].Value;
            startRow = int.Parse(rangeMatch.Groups[2].Value);
            endColumn = rangeMatch.Groups[3].Value;
            endRow = int.Parse(rangeMatch.Groups[4].Value);
            return true;
        }

        var singleCellMatch = SingleCellPattern().Match(trimmed);
        if (singleCellMatch.Success)
        {
            startColumn = endColumn = singleCellMatch.Groups[1].Value;
            startRow = endRow = int.Parse(singleCellMatch.Groups[2].Value);
            return true;
        }

        return false;
    }

    private static (string Start, string End) SplitColumnRange(string columnRange)
    {
        var parts = columnRange.Split(':');
        return parts.Length == 1 ? (parts[0], parts[0]) : (parts[0], parts[1]);
    }

    private static int ColumnToNumber(string column)
    {
        var number = 0;
        foreach (var c in column)
        {
            number = (number * 26) + (c - 'A' + 1);
        }

        return number;
    }

    [GeneratedRegex(@"^([A-Z]{1,3})(\d+):([A-Z]{1,3})(\d+)$")]
    private static partial Regex FullRangePattern();

    [GeneratedRegex(@"^([A-Z]{1,3})(\d+)$")]
    private static partial Regex SingleCellPattern();
}
