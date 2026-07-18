using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Generation.Profile;

// A Point column of a generated sheet -- ColonneNom matches PointPivot.ColonneNom to decide whether
// MarkValue is written for a given row. MarkValue itself is validated non-blank (not requested
// verbatim by the ticket, but an empty mark would be visually indistinguishable from "no Point" once
// written to a cell -- the same kind of self-consistency guard as
// SheetExtractionRule.SheetName/Locator.Sheet on the import side).
public sealed record PointColumnDefinition
{
    public const string DefaultMarkValue = "X";

    public string ColonneNom { get; }
    public string Header { get; }
    public string MarkValue { get; }

    public PointColumnDefinition(string colonneNom, string header, string markValue = DefaultMarkValue)
    {
        if (string.IsNullOrWhiteSpace(colonneNom))
        {
            throw new DomainValidationException(
                "Colonne nom must not be empty.", nameof(colonneNom), DomainErrorCode.PointColumnDefinition_EmptyColonneNom);
        }

        if (string.IsNullOrWhiteSpace(header))
        {
            throw new DomainValidationException(
                "Header must not be empty.", nameof(header), DomainErrorCode.PointColumnDefinition_EmptyHeader);
        }

        if (string.IsNullOrWhiteSpace(markValue))
        {
            throw new DomainValidationException(
                "Mark value must not be empty.", nameof(markValue), DomainErrorCode.PointColumnDefinition_EmptyMarkValue);
        }

        ColonneNom = colonneNom;
        Header = header;
        MarkValue = markValue;
    }
}
