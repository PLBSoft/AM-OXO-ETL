using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A row of the PROCEDURE sheet's TacheMultiple block. Action is the sheet's stop-condition field
// (column C:L), so it is always non-empty -- including for a "ligne de mise en page" (EstFactice)
// row, which by definition has a blank Ordre but non-blank Action. Acteur/Risques/TypeTacheMultipleCode
// are left unvalidated because a factice row can legitimately leave them blank -- see
// docs/spec-extraction-fichier-source-oxo-2026-07-16_4.md §1.2.
public sealed record TacheMultiplePivot
{
    public int? Ordre { get; }
    public string Action { get; }
    public string Acteur { get; }
    public string Risques { get; }
    public string TypeTacheMultipleCode { get; }
    public DateOnly? DateValidation { get; }
    public bool EstFactice { get; }

    public TacheMultiplePivot(
        int? ordre, string action, string acteur, string risques, string typeTacheMultipleCode,
        DateOnly? dateValidation, bool estFactice)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainValidationException(
                "Action must not be empty.", nameof(action), DomainErrorCode.TacheMultiplePivot_EmptyAction);
        }

        Ordre = ordre;
        Action = action;
        Acteur = acteur;
        Risques = risques;
        TypeTacheMultipleCode = typeTacheMultipleCode;
        DateValidation = dateValidation;
        EstFactice = estFactice;
    }
}
