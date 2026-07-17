using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A child of EquipementPivot, read from one repeating block of an isolement-style sheet
// (ISOLEMENT, PLATINES, ORIFICES CAPACITES, AUTRES JOINTS TOUCHES, DIVERS). Localisation starts
// empty and is filled in later by the DIVERS sheet's "loc1" broadcast (via a `with` expression) --
// see docs/modele-domaine-import-profile-2026-07-16.md §1.5 and docs/spec-extraction-fichier-source-oxo-2026-07-16.md §6.
//
// Designation is deliberately left unvalidated (not required non-blank): the real D8570 fixture has
// an ISOLEMENT row (Identification "V4", TypeElement "VANNE") with a blank Designation cell, and per
// spec §2/§3.2 that row must still be extracted normally (unrecognized TypeElement is a non-blocking
// warning, not a rejection) -- a required-Designation invariant would turn a legitimate row into a
// thrown exception instead. TypeElementNom/PositionALaPose stay required since no fixture evidence
// contradicts that yet.
public sealed record IsolementPivot
{
    public string Repere { get; }
    public string Designation { get; }
    public string TypeElementNom { get; }
    public string PositionALaPose { get; }
    public string Localisation { get; init; }

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

        if (string.IsNullOrWhiteSpace(positionALaPose))
        {
            throw new DomainValidationException(
                "Position a la pose must not be empty.", nameof(positionALaPose),
                DomainErrorCode.IsolementPivot_EmptyPositionALaPose);
        }

        Repere = repere;
        Designation = designation;
        TypeElementNom = typeElementNom;
        PositionALaPose = positionALaPose;
        Localisation = localisation;
    }
}
