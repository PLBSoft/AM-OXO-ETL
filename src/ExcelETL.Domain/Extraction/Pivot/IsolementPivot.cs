using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A child of EquipementPivot, read from one repeating block of an isolement-style sheet
// (ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS). Localisation starts
// empty and is filled in later by the DIVERS sheet's "loc1" broadcast (via a `with` expression) --
// see docs/modele-domaine-import-profile-2026-07-16.md §1.5 and docs/spec-extraction-fichier-source-oxo-2026-07-16.md §6.
//
// Only Repere and TypeElementNom are required non-blank -- the two fields every one of the 5
// isolement-style sheets always populates. Designation and PositionALaPose are both deliberately
// left unvalidated:
// - Designation: the real D8570 fixture has an ISOLEMENT row (Identification "V4", TypeElement
//   "VANNE") with a blank Designation cell, and per spec §2/§3.2 that row must still be extracted
//   normally (unrecognized TypeElement is a non-blocking warning, not a rejection).
// - PositionALaPose: only the ISOLEMENT sheet has a source cell for it ("Position MAD", H20:O21) --
//   PLATINES/ORIFICES CAPACITES/AUTRES JOINTS TOUCHES/DIVERS have no equivalent, so their
//   IsolementPivot instances necessarily leave it blank. A required-non-blank invariant here would
//   make every non-ISOLEMENT sheet's extraction throw. Individual sheet services (e.g.
//   IsolementExtractionService for the ISOLEMENT sheet specifically) may still enforce their own,
//   stricter "blank is reportable" policy on top of this before ever constructing the pivot.
// Tableaux/Applications/RepereParent (Lot U, docs/tickets-tdd-pivot-tableaux-applications-export.md)
// follow the exact same broadcast mechanism as Localisation: all start empty and are filled in later
// by ImportPipelineOrchestrator (Tableaux/Applications from ImportProfile.DefaultTableaux/
// DefaultApplicationNames, RepereParent from the run's EquipementPivot.Repere) via a `with`
// expression -- a plain copied string/list, not a navigation back to EquipementPivot itself.
public sealed record IsolementPivot
{
    public string Repere { get; }
    public string Designation { get; }
    public string TypeElementNom { get; }
    public string PositionALaPose { get; }
    public string Localisation { get; init; }
    public IReadOnlyList<string> Tableaux { get; init; }
    public IReadOnlyList<string> Applications { get; init; }
    public string RepereParent { get; init; }

    public IsolementPivot(string repere, string designation, string typeElementNom, string positionALaPose, string localisation)
    {
        if (string.IsNullOrWhiteSpace(repere))
        {
            throw new DomainValidationException(
                "Repere must not be empty.", nameof(repere), DomainErrorCode.IsolementPivot_EmptyRepere);
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
        PositionALaPose = positionALaPose;
        Localisation = localisation;
        Tableaux = [];
        Applications = [];
        RepereParent = "";
    }

    // Tableaux/Applications are IReadOnlyList<string> -- default record equality compares collection
    // properties by reference, not content, so an explicit override is needed (same reason as
    // RepeatingBlockLocator/Concat in Extraction/Primitives).
    public bool Equals(IsolementPivot? other) =>
        other is not null
        && Repere == other.Repere
        && Designation == other.Designation
        && TypeElementNom == other.TypeElementNom
        && PositionALaPose == other.PositionALaPose
        && Localisation == other.Localisation
        && Tableaux.SequenceEqual(other.Tableaux)
        && Applications.SequenceEqual(other.Applications)
        && RepereParent == other.RepereParent;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Repere);
        hash.Add(Designation);
        hash.Add(TypeElementNom);
        hash.Add(PositionALaPose);
        hash.Add(Localisation);
        hash.Add(RepereParent);
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
