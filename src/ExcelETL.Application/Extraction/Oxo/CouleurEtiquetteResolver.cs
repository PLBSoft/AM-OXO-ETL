using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo;

// Shared by every isolement-style sheet service that constructs IsolementPivot.CouleurEtiquette
// (Lot 068 + client feedback 2026-09). Two independent, mutually-exclusive-in-practice ways a
// profile can supply this value:
// - CouleurEtiquetteCell: read per block (PLATINES/ORIFICES CAPACITES today).
// - DefaultCouleurEtiquette: one fixed value applied to every isolement of the sheet, no workbook
//   read at all (AUTRES JOINTS TOUCHES today).
// If a profile somehow configures both, the cell wins -- a real per-block reading is always more
// specific/trustworthy than a blanket default. Neither configured means "" (ISOLEMENT/DIVERS today).
//
// Client feedback (2026-09-11), a real ORIFICES CAPACITES screenshot: H18:N18 (and every other
// block's own offset) genuinely *is* the couleur d'étiquette input cell (confirmed by 2 filled-in
// "ROUGE" blocks sitting right next to an unfilled 3rd one) -- but the form's own template leaves
// the literal text "DATE" in that cell until a color is actually typed over it (an Excel default
// cell value, not a real color -- ROUGE/BLEUE/JAUNE are the only ones ever observed for real).
// Treated as equivalent to "not filled in" here, trimmed + case-insensitive (spec §7 convention),
// so an un-filled block never silently imports "DATE" as its couleur d'étiquette.
public static class CouleurEtiquetteResolver
{
    private const string UnfilledCellTemplateArtifact = "DATE";

    public static string Resolve(
        IWorkbookReader workbookReader, string sheet, SheetExtractionRule sheetRule, int blockStartRow)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        if (sheetRule.CouleurEtiquetteCell is not null)
        {
            var range = BlockFieldRangeCalculator.BuildRange(sheetRule.CouleurEtiquetteCell, blockStartRow);
            var value = workbookReader.ReadCellValue(sheet, range) ?? "";
            return value.Trim().Equals(UnfilledCellTemplateArtifact, StringComparison.OrdinalIgnoreCase) ? "" : value;
        }

        return sheetRule.DefaultCouleurEtiquette ?? "";
    }
}
