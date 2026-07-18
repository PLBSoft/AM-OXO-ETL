using ExcelETL.Domain.Exceptions;
using ExcelETL.Domain.Generation.Fields;

namespace ExcelETL.Domain.Generation.Profile;

// A descriptive (non-Point) column of a generated sheet. Source = null is a deliberately valid state:
// it reserves a column in the output schema (matching the target workbook's known layout) with no
// extraction rule wired to it yet -- writes an empty cell, not an error. See
// docs/tickets-tdd-ecriture-fichier-cible.md I1.
public sealed record ColumnDefinition
{
    public string Header { get; }
    public PivotFieldRef? Source { get; }

    public ColumnDefinition(string header, PivotFieldRef? source)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new DomainValidationException(
                "Header must not be empty.", nameof(header), DomainErrorCode.ColumnDefinition_EmptyHeader);
        }

        Header = header;
        Source = source;
    }
}
