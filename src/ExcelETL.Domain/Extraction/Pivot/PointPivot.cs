using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A Point created for a parent (Equipement or Isolement) on a given Colonne, per a ConditionalPointRule
// match or an unconditional column.
public sealed record PointPivot
{
    public string ColonneNom { get; }
    public string ParentRepere { get; }

    public PointPivot(string colonneNom, string parentRepere)
    {
        if (string.IsNullOrWhiteSpace(colonneNom))
        {
            throw new DomainValidationException(
                "Colonne nom must not be empty.", nameof(colonneNom), DomainErrorCode.PointPivot_EmptyColonneNom);
        }

        if (string.IsNullOrWhiteSpace(parentRepere))
        {
            throw new DomainValidationException(
                "Parent repere must not be empty.", nameof(parentRepere), DomainErrorCode.PointPivot_EmptyParentRepere);
        }

        ColonneNom = colonneNom;
        ParentRepere = parentRepere;
    }
}
