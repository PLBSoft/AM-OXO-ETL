namespace ExcelETL.Domain.Extraction.Pivot;

// Deliberately not exhaustive -- new members are added as Lot B/C business rules surface concrete
// failure cases, rather than pre-guessing the full catalogue now. See
// docs/modele-domaine-import-profile-2026-07-16.md §3.
public enum ExtractionErrorCode
{
    RequiredFieldMissing,
    UnparsableValue,
    NoConditionalPointCreated,
    TacheMultipleTypeMismatch,
    UnexpectedCouleurEtiquetteValue,

    // Lot 085: two elements of one file share the same generated repère (trimmed, case-insensitive).
    // Blocking: the whole file is rejected.
    DuplicateRepere,

    // The file is a genuine .xlsx package but ClosedXML fails while loading it (an internal structure
    // it doesn't support). Blocking: the whole file is rejected, reported like any other rejection.
    UnreadableWorkbook
}
