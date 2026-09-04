using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Generation.Profile;

// A column whose Value is written verbatim on every generated row, independently of any pivot data --
// Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md), for legacy
// columns like "CRITERE"/"AVANCEMENT"/"SUPPRESSION" that always carry the same literal on export.
// Deliberately unrelated to PivotSource, unlike PointColumnDefinition/ApplicationColumnDefinition (both
// disallowed for PivotSource.TacheMultiple): a constant column references no pivot field at all, so it
// is valid for every PivotSource, TacheMultiple included -- see SheetGenerationRule.
public sealed record ConstantColumnDefinition
{
    public string Header { get; }
    public string Value { get; }

    public ConstantColumnDefinition(string header, string value)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new DomainValidationException(
                "Header must not be empty.", nameof(header), DomainErrorCode.ConstantColumnDefinition_EmptyHeader);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(
                "Value must not be empty.", nameof(value), DomainErrorCode.ConstantColumnDefinition_EmptyValue);
        }

        Header = header;
        Value = value;
    }
}
