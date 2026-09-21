using System.Text.RegularExpressions;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Profile;

// One directly-read header cell (Lot 047, spec-migration-entetes-profile-driven-directcell.md §3).
// Name is the logical identifier a HeaderCompositeRule.Template placeholder ({name}) references -- it
// is not tied to any Pivot field name. StripReperePrefix/DateFormat are deliberately the only two
// transformations offered: a flat, non-recursive model, chosen specifically so
// it stays trivially persistable and editable from the Blazor profile editor (Lot 048).
//
// CellRange is read in the sheet of the SheetExtractionRule that owns this rule (lot 084, G13: the
// sheet name is no longer repeated here -- the former DirectCell carried its own copy).
public sealed partial record HeaderFieldRule
{
    public string Name { get; }
    public string CellRange { get; }
    public bool StripReperePrefix { get; }
    public string? DateFormat { get; }

    public HeaderFieldRule(string name, string cellRange, bool stripReperePrefix = false, string? dateFormat = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(
                "Name must not be empty.", nameof(name), DomainErrorCode.HeaderFieldRule_EmptyName);
        }

        if (string.IsNullOrWhiteSpace(cellRange) || !ExcelRangePattern().IsMatch(cellRange))
        {
            throw new DomainValidationException(
                "Cell range must be a valid Excel cell reference (e.g. 'B4') or merged range (e.g. 'B4:D4').",
                nameof(cellRange),
                DomainErrorCode.HeaderFieldRule_InvalidCellRange);
        }

        if (dateFormat is not null && string.IsNullOrWhiteSpace(dateFormat))
        {
            throw new DomainValidationException(
                "Date format must not be blank when provided.", nameof(dateFormat),
                DomainErrorCode.HeaderFieldRule_BlankDateFormat);
        }

        Name = name;
        CellRange = cellRange;
        StripReperePrefix = stripReperePrefix;
        DateFormat = dateFormat;
    }

    [GeneratedRegex(@"^[A-Z]{1,3}[1-9][0-9]*(:[A-Z]{1,3}[1-9][0-9]*)?$")]
    private static partial Regex ExcelRangePattern();
}
