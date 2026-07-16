using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// The extraction root, read from the PROCEDURE sheet's header. Every other pivot object links back
// to it by Repere -- see docs/modele-domaine-import-profile-2026-07-16.md §2.2.
public sealed record EquipementPivot
{
    public string Repere { get; }
    public string Designation { get; }
    public string TypeElementCode { get; }

    public EquipementPivot(string repere, string designation, string typeElementCode)
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

        if (string.IsNullOrWhiteSpace(typeElementCode))
        {
            throw new DomainValidationException(
                "Type element code must not be empty.", nameof(typeElementCode),
                DomainErrorCode.EquipementPivot_EmptyTypeElementCode);
        }

        Repere = repere;
        Designation = designation;
        TypeElementCode = typeElementCode;
    }
}
