namespace ExcelETL.Domain.Extraction.Pivot;

// Deliberately not exhaustive -- new members are added as Lot B/C business rules surface concrete
// failure cases, rather than pre-guessing the full catalogue now. See
// docs/modele-domaine-import-profile-2026-07-16.md §3.
public enum ExtractionErrorCode
{
    RequiredFieldMissing,
    UnparsableValue,
    UnrecognizedTypeElement,
    TypeIncoherenceDansTacheMultiple
}
