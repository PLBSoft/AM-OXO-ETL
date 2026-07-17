using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;

// Every field here is genuinely required (confirmed against all 3 real fixtures, no blanks
// observed), so unlike ISOLEMENT this delegates the block walk to the shared IRepeatingBlockReader.
// Still needs IConditionalPointRuleEvaluator though, for the one conditional Colonne ("POSE
// ÉTIQUETTES" unless TypeElement = "TUBING") alongside the 2 unconditional ones -- the same
// grouped-by-ColonneName pattern as IsolementExtractionService.
//
// The Equipement repere echo lives at cell N6, *not* the "K6:U6" the spec documents for this sheet
// (confirmed empty in all 3 real fixtures) -- probing the real files found the actual value at N6
// instead (a plain, unmerged cell). Hardcoded here as sheet-specific business knowledge, same as
// PROCEDURE's header ranges.
public sealed class AutresJointsTouchesExtractionService(
    IRepeatingBlockReader repeatingBlockReader,
    ITextTransformEvaluator textTransformEvaluator,
    IConditionalPointRuleEvaluator conditionalPointRuleEvaluator)
    : IAutresJointsTouchesExtractionService
{
    public IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        var equipementRepere = workbookReader.ReadCellValue(sheet, "N6") ?? "";

        var blockResult = repeatingBlockReader.Read(sheetRule.Locator, workbookReader);
        var pointRuleGroups = sheetRule.PointRules.GroupBy(r => r.ColonneName).ToList();

        var isolements = new List<IsolementPivot>();
        var points = new List<PointPivot>();
        var errors = new List<ExtractionError>(blockResult.Errors);

        foreach (var block in blockResult.Blocks)
        {
            var repere = ComposeRepere(equipementRepere, block[IsolementFieldNames.Identification]);
            var typeElement = block[IsolementFieldNames.TypeElement];

            isolements.Add(new IsolementPivot(
                repere, block[IsolementFieldNames.Designation], typeElement, positionALaPose: "", localisation: ""));

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
                errors.Add(new ExtractionError(sheet, repere, ExtractionErrorCode.UnrecognizedTypeElement, warning));
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
