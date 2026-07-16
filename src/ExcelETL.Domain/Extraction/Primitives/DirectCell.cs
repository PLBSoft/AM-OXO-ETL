using System.Text.RegularExpressions;
using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// Direct read of a single cell or merged range (e.g. header lookups like "M2:O2", "K6:T6").
public sealed partial record DirectCell
{
    public string Sheet { get; }
    public string Range { get; }

    public DirectCell(string sheet, string range)
    {
        if (string.IsNullOrWhiteSpace(sheet))
        {
            throw new DomainValidationException(
                "Sheet must not be empty.", nameof(sheet), DomainErrorCode.DirectCell_EmptySheet);
        }

        if (string.IsNullOrWhiteSpace(range) || !ExcelRangePattern().IsMatch(range))
        {
            throw new DomainValidationException(
                "Range must be a valid Excel cell reference (e.g. 'B4') or merged range (e.g. 'B4:D4').",
                nameof(range),
                DomainErrorCode.DirectCell_InvalidRange);
        }

        Sheet = sheet;
        Range = range;
    }

    [GeneratedRegex(@"^[A-Z]{1,3}[1-9][0-9]*(:[A-Z]{1,3}[1-9][0-9]*)?$")]
    private static partial Regex ExcelRangePattern();
}
