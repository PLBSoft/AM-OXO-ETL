namespace ExcelETL.BlazorAdmin.Formatting;

// Outcome of BlockFieldRangeFormatter.FromAbsoluteRange -- a Result type rather than a thrown
// exception, matching the signature the ticket itself specifies (there is no DomainErrorCode/
// ApplicationErrorCode for this: the absolute-range text is a pure Blazor-layer presentation
// concept, BlockFieldDefinition never sees it). See
// docs/tickets-tdd-blazor-profil-import-lisibilite-plages-excel.md, N1.
public sealed record BlockFieldRangeParseResult
{
    public bool IsSuccess { get; private init; }
    public string ColumnRange { get; private init; } = string.Empty;
    public int RowOffsetStart { get; private init; }
    public int RowOffsetEnd { get; private init; }

    // Level-2 plausibility signal (N1 §"Validation des bornes"): still IsSuccess = true, saveable,
    // but the caller should surface a non-blocking warning to the user.
    public bool IsBeyondPracticalRange { get; private init; }

    public static BlockFieldRangeParseResult Success(
        string columnRange, int rowOffsetStart, int rowOffsetEnd, bool isBeyondPracticalRange) =>
        new()
        {
            IsSuccess = true,
            ColumnRange = columnRange,
            RowOffsetStart = rowOffsetStart,
            RowOffsetEnd = rowOffsetEnd,
            IsBeyondPracticalRange = isBeyondPracticalRange
        };

    public static BlockFieldRangeParseResult Failure() => new() { IsSuccess = false };
}
