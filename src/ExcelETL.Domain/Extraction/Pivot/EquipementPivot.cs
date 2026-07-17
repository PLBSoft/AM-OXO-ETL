using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// The extraction root, read from the PROCEDURE sheet's header. Every other pivot object links back
// to it by Repere -- see docs/modele-domaine-import-profile-2026-07-16.md §2.2.
//
// TypeElementNom (renamed from TypeElementCode, model doc v2/spec v5): the client confirmed source
// and target both use TypeElement.Nom throughout, including for the parent Equipement -- never
// TypeElement.Code. The value itself ("MAD TRAVAUX" for a MAD dossier) comes from
// ImportProfile.EquipementTypeElementNom, never a constant in the extraction service.
//
// Localisation starts empty and is filled in later by the DIVERS sheet's "loc1" broadcast (via a
// `with` expression), same pattern as IsolementPivot.Localisation -- see
// docs/modele-domaine-import-profile-2026-07-16.md §1.5 and docs/spec-extraction-fichier-source-oxo-2026-07-16.md §6.
// Applies to the Equipement in addition to every Isolement of the run (Lot D).
public sealed record EquipementPivot
{
    public string Repere { get; }
    public string Designation { get; }
    public string TypeElementNom { get; }
    public string Localisation { get; init; }

    public EquipementPivot(string repere, string designation, string typeElementNom)
    {
        if (string.IsNullOrWhiteSpace(repere))
        {
            throw new DomainValidationException(
                "Repere must not be empty.", nameof(repere), DomainErrorCode.EquipementPivot_EmptyRepere);
        }

        if (string.IsNullOrWhiteSpace(designation))
        {
            throw new DomainValidationException(
                "Designation must not be empty.", nameof(designation), DomainErrorCode.EquipementPivot_EmptyDesignation);
        }

        if (string.IsNullOrWhiteSpace(typeElementNom))
        {
            throw new DomainValidationException(
                "Type element nom must not be empty.", nameof(typeElementNom),
                DomainErrorCode.EquipementPivot_EmptyTypeElementNom);
        }

        Repere = repere;
        Designation = designation;
        TypeElementNom = typeElementNom;
        Localisation = "";
    }
}
