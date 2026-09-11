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
public static class CouleurEtiquetteResolver
{
    public static string Resolve(
        IWorkbookReader workbookReader, string sheet, SheetExtractionRule sheetRule, int blockStartRow)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        if (sheetRule.CouleurEtiquetteCell is not null)
        {
            var range = BlockFieldRangeCalculator.BuildRange(sheetRule.CouleurEtiquetteCell, blockStartRow);
            return workbookReader.ReadCellValue(sheet, range) ?? "";
        }

        return sheetRule.DefaultCouleurEtiquette ?? "";
    }
}
