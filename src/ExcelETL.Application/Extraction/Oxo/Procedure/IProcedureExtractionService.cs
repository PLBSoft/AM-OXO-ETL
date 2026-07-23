using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo.Procedure;

public interface IProcedureExtractionService
{
    // Returns a PROCEDURE-only ImportResult: Equipement + one unconditional Point per name in
    // defaultTableaux (Lot U -- never a hardcoded pair of constants, see
    // docs/tickets-tdd-pivot-tableaux-applications-export.md) + the TachesMultiples block, Isolements
    // always empty. Equipement is null (with a single blocking
    // ExtractionError) when the header itself is invalid -- see
    // docs/modele-domaine-import-profile-2026-07-16.md §3.1. Lot D's orchestrator merges this with
    // the other 5 sheets' contributions.
    ImportResult Extract(
        IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux);
}
