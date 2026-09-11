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
// Client feedback (2026-09-11): a real ORIFICES CAPACITES screenshot confirmed CouleurEtiquetteCell
// genuinely is the right cell -- but its form template leaves stray non-color text behind (e.g.
// "DATE") until someone actually types a color over it. Rather than blacklisting that one literal
// (the earlier fix), SheetExtractionRule.AllowedCouleursEtiquette is an opt-in whitelist: when
// configured, a cell value matching none of it (trim + case-insensitive, spec §7) is reported as a
// non-blocking warning via UnexpectedRawValue instead of being imported as-is -- the caller owns
// building/deduplicating the actual ExtractionError (see UnexpectedCouleurEtiquetteValueWarningTracker),
// this resolver stays free of any ILogger/errors-list dependency, same "evaluators return a tuple,
// the sheet service owns the warning tracker" convention as ConditionalPointRuleEvaluator/
// TextTransformEvaluator. No allowlist configured (null, the default) means "accept the cell's raw
// content as-is" -- backward compatible with any profile predating this feature.
public static class CouleurEtiquetteResolver
{
    public static (string Value, string? UnexpectedRawValue) Resolve(
        IWorkbookReader workbookReader, string sheet, SheetExtractionRule sheetRule, int blockStartRow)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        if (sheetRule.CouleurEtiquetteCell is not null)
        {
            var range = BlockFieldRangeCalculator.BuildRange(sheetRule.CouleurEtiquetteCell, blockStartRow);
            var rawValue = workbookReader.ReadCellValue(sheet, range) ?? "";
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return ("", null);
            }

            var trimmedValue = rawValue.Trim();
            if (sheetRule.AllowedCouleursEtiquette is null)
            {
                return (trimmedValue, null);
            }

            var match = sheetRule.AllowedCouleursEtiquette.FirstOrDefault(
                allowed => string.Equals(allowed.Trim(), trimmedValue, StringComparison.OrdinalIgnoreCase));
            return match is not null ? (match, null) : ("", trimmedValue);
        }

        return (sheetRule.DefaultCouleurEtiquette ?? "", null);
    }
}
