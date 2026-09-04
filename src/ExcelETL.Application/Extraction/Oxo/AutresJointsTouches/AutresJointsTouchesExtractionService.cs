using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;

// Every field here is genuinely required (confirmed against all 3 real fixtures, no blanks
// observed), so unlike ISOLEMENT this delegates the block walk to the shared IRepeatingBlockReader.
// Still needs IConditionalPointRuleEvaluator though, for the one conditional Colonne ("POSE
// ÉTIQUETTES" unless TypeElement = "TUBING") alongside the 2 unconditional ones -- the same
// grouped-by-ColonneName pattern as IsolementExtractionService.
//
// The Equipement repere echo lives at cell N6, *not* the "K6:U6" the spec documents for this sheet
// (confirmed empty in all 3 real fixtures) -- probing the real files found the actual value at N6
// instead (a plain, unmerged cell). Since Lot 047, that coordinate comes from the profile's own
// HeaderFieldRule (SharedHeaderFieldNames.RepereEcho) via IHeaderRuleResolver, no longer a literal
// here -- see DefaultProfileSeeder for the seeded N6 rule.
public sealed class AutresJointsTouchesExtractionService(
    IRepeatingBlockReader repeatingBlockReader,
    ITextTransformEvaluator textTransformEvaluator,
    IConditionalPointRuleEvaluator conditionalPointRuleEvaluator,
    IHeaderRuleResolver headerRuleResolver,
    ILogger<AutresJointsTouchesExtractionService> logger)
    : IAutresJointsTouchesExtractionService
{
    public IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        var header = headerRuleResolver.Resolve(workbookReader, sheetRule, reperePrefix);
        var equipementRepere = header.Fields[SharedHeaderFieldNames.RepereEcho].Value ?? "";

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
            var repere = ComposeRepere(equipementRepere, block.Fields[IsolementFieldNames.Identification]);
            var typeElement = block.Fields[IsolementFieldNames.TypeElement];

            isolements.Add(new IsolementPivot(
                repere, block.Fields[IsolementFieldNames.Designation], typeElement, positionALaPose: "", localisation: ""));

            foreach (var colonneName in sheetRule.UnconditionalColonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            var extractedFields = new Dictionary<string, string> { [IsolementFieldNames.TypeElement] = typeElement };
            var (colonneNames, warning) = ConditionalPointGroupEvaluator.Evaluate(
                conditionalPointRuleEvaluator, pointRuleGroups, extractedFields);
            foreach (var colonneName in colonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            if (warning is not null)
            {
                warningTracker.RecordIfNew(repere, typeElement, logger, errors);
            }
        }

        return new IsolementSheetExtractionResult(isolements, points, errors);
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
