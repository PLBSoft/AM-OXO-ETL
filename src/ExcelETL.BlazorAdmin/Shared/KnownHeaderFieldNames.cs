using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Procedure;

namespace ExcelETL.BlazorAdmin.Shared;

// UI-only, advisory table (Lot 048, 48.5): the extraction services (ProcedureExtractionService,
// AutresJointsTouchesExtractionService, DiversExtractionService) read HeaderFieldRule/HeaderCompositeRule
// values by name via an indexer -- renaming or removing one of these names from the profile editor
// produces an unhandled KeyNotFoundException at extraction time, not a clean domain error (Lot 047's
// known structural trap, see the Lot 048 ticket). This table is never consulted by the extraction
// pipeline itself -- it exists purely so SheetRuleForm can warn an admin before that happens.
//
// The 3 sheet-name literals below are deliberately duplicated rather than referenced from
// ImportPipelineOrchestrator, whose own equivalent constants are `private` and not reachable from
// BlazorAdmin.
public static class KnownHeaderFieldNames
{
    private const string Procedure = "PROCEDURE";
    private const string AutresJointsTouches = "AUTRES JOINTS TOUCHES";
    private const string Divers = "DIVERS";

    private static readonly Dictionary<string, (string[] Fields, string[] Composites)> BySheetName = new(StringComparer.Ordinal)
    {
        [Procedure] = (
            [ProcedureHeaderFieldNames.NomMad, ProcedureHeaderFieldNames.Revision, ProcedureHeaderFieldNames.DateRev],
            [ProcedureHeaderFieldNames.Designation]),
        [AutresJointsTouches] = ([SharedHeaderFieldNames.RepereEcho], []),
        [Divers] = ([SharedHeaderFieldNames.RepereEcho], []),
    };

    // Returns the field/composite names extraction expects for this sheet, or empty lists when the
    // sheet has none (e.g. ISOLEMENT/PLATINES/ORIFICES CAPACITES). Comparison against a profile's
    // actual names must stay ordinal/case-sensitive -- that's what the resolver's own Dictionary does.
    public static (IReadOnlyList<string> Fields, IReadOnlyList<string> Composites) For(string sheetName) =>
        BySheetName.TryGetValue(sheetName, out var expected) ? expected : ([], []);
}
