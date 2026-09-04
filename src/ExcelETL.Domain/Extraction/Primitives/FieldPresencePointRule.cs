using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// Guards whether a Point is created based purely on whether an already-read cell has a value at all
// -- distinct from ConditionalPointRule, which compares an extracted field's value against a fixed
// ComparisonValue (and always requires one). Introduced for PLATINES' "RECEPTION DEBUT MAD"/"RECEPTION
// DEBUT REL" client feedback: the signal is "was POSEE LE/DEPOSEE LE filled in at all", not "does it
// equal a specific text" -- ConditionalPointRule can't express that (ComparisonValue must be non-blank).
//
// Cell reuses BlockFieldDefinition purely as "where to read this optional cell" (column range + row
// offsets) -- it is deliberately never added to a RepeatingBlockLocator's own Fields list, so it never
// goes through IRepeatingBlockReader's required-field policy (which would otherwise report
// RequiredFieldMissing and drop the whole block whenever the cell is blank -- the normal case here).
public sealed record FieldPresencePointRule
{
    public BlockFieldDefinition Cell { get; }
    public string ColonneName { get; }

    public FieldPresencePointRule(BlockFieldDefinition cell, string colonneName)
    {
        ArgumentNullException.ThrowIfNull(cell);

        if (string.IsNullOrWhiteSpace(colonneName))
        {
            throw new DomainValidationException(
                "Colonne name must not be empty.", nameof(colonneName),
                DomainErrorCode.FieldPresencePointRule_EmptyColonneName);
        }

        Cell = cell;
        ColonneName = colonneName;
    }

    // EF Core materialization only -- constructor binding cannot bind a reference to an owned type
    // (same "Navigations... cannot be bound" restriction HeaderFieldRule.Cell's own comment
    // documents). Cell is set directly via reflection immediately afterwards, bypassing this
    // constructor's (nonexistent) validation entirely.
    private FieldPresencePointRule()
    {
        Cell = null!;
        ColonneName = string.Empty;
    }
}
