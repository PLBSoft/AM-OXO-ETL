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
//
// Tableaux/Applications (Lot U, docs/tickets-tdd-pivot-tableaux-applications-export.md) follow the
// exact same broadcast mechanism as Localisation: both start empty and are filled in later by
// ImportPipelineOrchestrator from ImportProfile.DefaultTableaux/DefaultApplicationNames (via a `with`
// expression), never constructed positionally, never a hardcoded constant in an extraction service.
public sealed record EquipementPivot
{
    public string Repere { get; }
    public string Designation { get; }
    public string TypeElementNom { get; }
    public string Localisation { get; init; }
    public IReadOnlyList<string> Tableaux { get; init; }
    public IReadOnlyList<string> Applications { get; init; }

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
        Tableaux = [];
        Applications = [];
    }

    // Tableaux/Applications are IReadOnlyList<string> -- default record equality compares collection
    // properties by reference, not content, so an explicit override is needed (same reason as
    // RepeatingBlockLocator/Concat in Extraction/Primitives).
    public bool Equals(EquipementPivot? other) =>
        other is not null
        && Repere == other.Repere
        && Designation == other.Designation
        && TypeElementNom == other.TypeElementNom
        && Localisation == other.Localisation
        && Tableaux.SequenceEqual(other.Tableaux)
        && Applications.SequenceEqual(other.Applications);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Repere);
        hash.Add(Designation);
        hash.Add(TypeElementNom);
        hash.Add(Localisation);
        foreach (var tableau in Tableaux)
        {
            hash.Add(tableau);
        }

        foreach (var application in Applications)
        {
            hash.Add(application);
        }

        return hash.ToHashCode();
    }
}
