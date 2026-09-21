using ExcelETL.BlazorAdmin.Formatting;

namespace ExcelETL.BlazorAdmin.Shared;

// UI-only, advisory table (Lot 048, 48.5): the extraction services (ProcedureExtractionService,
// ElementSheetExtractionService) read HeaderFieldRule/HeaderCompositeRule values by name -- renaming or
// removing one of these names from the profile editor makes extraction fail, not a clean domain error
// (Lot 047's known structural trap, see the Lot 048 ticket). This table is never consulted by the extraction
// pipeline itself -- it exists purely so SheetRuleForm can warn an admin before that happens.
//
// Since Lot 078.1 the names come from ImportSheetUsage, the single BlazorAdmin-side table of what each
// sheet reads, so the editor's warning and the Details page can't disagree.
public static class KnownHeaderFieldNames
{
    // Returns the field/composite names extraction expects for this sheet, or empty lists when the
    // sheet has none (e.g. ISOLEMENT/PLATINES/ORIFICES CAPACITES). Comparison against a profile's
    // actual names must stay ordinal/case-sensitive -- that's what the resolver's own Dictionary does.
    public static (IReadOnlyList<string> Fields, IReadOnlyList<string> Composites) For(string sheetName)
    {
        var usage = ImportSheetUsage.For(sheetName);
        return usage is null
            ? ([], [])
            : ([.. usage.RequiredHeaderFields.Select(h => h.Name)], [.. usage.RequiredHeaderComposites.Select(h => h.Name)]);
    }
}
