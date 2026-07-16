using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A child of EquipementPivot, read from one repeating block of an isolement-style sheet
// (ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS). Localisation starts
// empty and is filled in later by the DIVERS sheet's "loc1" broadcast (via a `with` expression) --
// see docs/modele-domaine-import-profile-2026-07-16.md §1.5 and docs/spec-extraction-fichier-source-oxo-2026-07-16_4.md §6.
public sealed record IsolementPivot
{
    public string Repere { get; }
    public string Designation { get; }
    public string TypeElementNom { get; }
    public string Localisation { get; init; }

    public IsolementPivot(string repere, string designation, string typeElementNom, string localisation)
    {
        if (string.IsNullOrWhiteSpace(repere))
        {
            throw new DomainValidationException(
                "Repere must not be empty.", nameof(repere), DomainErrorCode.IsolementPivot_EmptyRepere);
        }

        if (string.IsNullOrWhiteSpace(designation))
        {
            throw new DomainValidationException(
                "Designation must not be empty.", nameof(designation), DomainErrorCode.IsolementPivot_EmptyDesignation);
        }

        if (string.IsNullOrWhiteSpace(typeElementNom))
        {
            throw new DomainValidationException(
                "Type element nom must not be empty.", nameof(typeElementNom),
                DomainErrorCode.IsolementPivot_EmptyTypeElementNom);
        }

        Repere = repere;
        Designation = designation;
        TypeElementNom = typeElementNom;
        Localisation = localisation;
    }
}
