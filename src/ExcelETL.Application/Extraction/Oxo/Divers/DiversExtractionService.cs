using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo.Divers;

// DIVERS has every one of its Colonnes conditional (sheetRule.UnconditionalColonneNames is expected
// empty), across 4 mutually-exclusive TypeElement values (INSTRUMENTATION/ZERO ENERGIE/SOUPAPE
// (x2 Colonnes)/POINT FEU (x3 Colonnes)) -- exactly the case ConditionalPointGroupEvaluator's
// aggregate-warning fix exists for: a SOUPAPE isolement must get its 2 Colonnes and no warning, not
// also warn for not matching the other 5. Every field is genuinely required (confirmed against the
// real fixtures), so the block walk delegates to the shared IRepeatingBlockReader.
//
// Also reads "loc1" (B6:E6, raw value, no transformation per spec §6) into the result for Lot D's
// orchestrator to broadcast onto the Equipement and every Isolement of the whole run.
//
// Equipement repere echo lives at N6, same discrepancy from the spec's stated "K6:U6" already found
// for AUTRES JOINTS TOUCHES (confirmed blank there in all 3 real fixtures). Since Lot 047, that
// coordinate comes from the profile's own HeaderFieldRule (SharedHeaderFieldNames.RepereEcho) via
// IHeaderRuleResolver, no longer a literal here. loc1 (B6:E6) stays a plain hardcoded
// IWorkbookReader read -- explicitly out of Lot 047's scope (spec/ticket only covers PROCEDURE's
// header + the N6 echo, not loc1).
public sealed class DiversExtractionService(
    IRepeatingBlockReader repeatingBlockReader,
    ITextTransformEvaluator textTransformEvaluator,
    IConditionalPointRuleEvaluator conditionalPointRuleEvaluator,
    IHeaderRuleResolver headerRuleResolver,
    ILogger<DiversExtractionService> logger)
    : IDiversExtractionService
{
    public DiversSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        var header = headerRuleResolver.Resolve(workbookReader, sheetRule, reperePrefix);
        var equipementRepere = header.Fields[SharedHeaderFieldNames.RepereEcho].Value ?? "";
        var loc1 = workbookReader.ReadCellValue(sheet, "B6:E6") ?? "";

        var blockResult = repeatingBlockReader.Read(sheetRule.Locator, workbookReader);
        foreach (var blockError in blockResult.Errors)
        {
            ExtractionErrorLogging.Log(logger, blockError);
        }

        var pointRuleGroups = sheetRule.PointRules.GroupBy(r => r.ColonneName).ToList();

        var isolements = new List<IsolementPivot>();
        var points = new List<PointPivot>();
        var errors = new List<ExtractionError>(blockResult.Errors);
        var warningTracker = new NoConditionalPointCreatedWarningTracker(sheet);

        foreach (var block in blockResult.Blocks)
        {
            var repere = ComposeRepere(equipementRepere, block[IsolementFieldNames.Identification]);
            var typeElement = block[IsolementFieldNames.TypeElement];

            isolements.Add(new IsolementPivot(
                repere, block[IsolementFieldNames.Designation], typeElement, positionALaPose: "", localisation: ""));

            var extractedFields = new Dictionary<string, string> { [IsolementFieldNames.TypeElement] = typeElement };
            var (colonneNames, warning) = ConditionalPointGroupEvaluator.Evaluate(
                conditionalPointRuleEvaluator, pointRuleGroups, extractedFields);

            foreach (var colonneName in sheetRule.UnconditionalColonneNames.Concat(colonneNames))
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            if (warning is not null)
            {
                warningTracker.RecordIfNew(repere, typeElement, logger, errors);
            }
        }

        return new DiversSheetExtractionResult(loc1, isolements, points, errors);
    }

    private string ComposeRepere(string equipementRepere, string identification)
    {
        var extractedFields = new Dictionary<string, string>
        {
            ["EquipementRepere"] = equipementRepere,
            [IsolementFieldNames.Identification] = identification
        };
        var transform = new Concat(
        [
            new FieldRef("EquipementRepere"),
            new Literal("-"),
            new FieldRef(IsolementFieldNames.Identification)
        ]);
        var (repere, _) = textTransformEvaluator.Evaluate(transform, rawValue: null, extractedFields);
        return repere!;
    }
}
