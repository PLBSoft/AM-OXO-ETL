using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo.Isolement;

// ISOLEMENT can't reuse IRepeatingBlockReader either, for a narrower reason than PROCEDURE: the real
// D8570 fixture has a row (Identification "V4", TypeElement "VANNE") with a blank Designation cell
// that must still be extracted normally (an unrecognized TypeElement is a non-blocking warning per
// spec §2/§3.2, not a block rejection) -- so Designation is read leniently while
// Identification/TypeElement/PositionALaPose stay required, a policy RepeatingBlockReader's
// uniform-strictness can't express. Walks the block itself via BlockFieldRangeCalculator.
//
// Repere composition ({K6:T6}-{Identification}) reads K6:T6 directly off the ISOLEMENT sheet rather
// than taking the PROCEDURE-derived Equipement.Repere as a parameter -- confirmed against the real
// fixtures that K6:T6 is its own (different, shorter) value, not an echo of Equipement.Repere; the
// spec calls for composing from K6:T6 specifically, so this service is self-contained and doesn't
// need any cross-sheet context threaded in.
public sealed class IsolementExtractionService(
    ITextTransformEvaluator textTransformEvaluator,
    IConditionalPointRuleEvaluator conditionalPointRuleEvaluator,
    ILogger<IsolementExtractionService> logger)
    : IIsolementExtractionService
{
    public IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        var locator = sheetRule.Locator;
        var equipementRepere = workbookReader.ReadCellValue(sheet, "K6:T6") ?? "";

        var identificationField = FindField(locator, IsolementFieldNames.Identification);
        var designationField = FindField(locator, IsolementFieldNames.Designation);
        var positionField = FindField(locator, IsolementFieldNames.PositionALaPose);
        var typeElementField = FindField(locator, IsolementFieldNames.TypeElement);

        // Grouped by ColonneName, then aggregated via ConditionalPointGroupEvaluator so a warning
        // only fires when *none* of the sheet's conditional Colonnes matched (model doc §3.2) --
        // ISOLEMENT has exactly one conditional Colonne (ZERO ENERGIE), so every non-"ZERO ENERGIE"
        // isolement legitimately warns here, confirmed against all 3 real fixtures (none contain a
        // ZERO ENERGIE-typed row in this sheet).
        var pointRuleGroups = sheetRule.PointRules.GroupBy(r => r.ColonneName).ToList();

        var isolements = new List<IsolementPivot>();
        var points = new List<PointPivot>();
        var errors = new List<ExtractionError>();
        var blockIndex = 0;

        while (true)
        {
            var blockStartRow = locator.FirstBlockStartRow + blockIndex * locator.Step;
            var identification = workbookReader.ReadCellValue(
                sheet, BlockFieldRangeCalculator.BuildRange(identificationField, blockStartRow));

            if (string.IsNullOrWhiteSpace(identification))
            {
                break;
            }

            var designation = workbookReader.ReadCellValue(
                sheet, BlockFieldRangeCalculator.BuildRange(designationField, blockStartRow)) ?? "";
            var positionALaPose = workbookReader.ReadCellValue(
                sheet, BlockFieldRangeCalculator.BuildRange(positionField, blockStartRow));
            var typeElement = workbookReader.ReadCellValue(
                sheet, BlockFieldRangeCalculator.BuildRange(typeElementField, blockStartRow));

            var repere = ComposeRepere(equipementRepere, identification);

            var blankFieldNames = new List<string>();
            if (string.IsNullOrWhiteSpace(positionALaPose))
            {
                blankFieldNames.Add(IsolementFieldNames.PositionALaPose);
            }

            if (string.IsNullOrWhiteSpace(typeElement))
            {
                blankFieldNames.Add(IsolementFieldNames.TypeElement);
            }

            if (blankFieldNames.Count > 0)
            {
                var error = new ExtractionError(
                    sheet, repere, ExtractionErrorCode.RequiredFieldMissing,
                    $"Bloc à la ligne {blockStartRow} : champ(s) requis '{string.Join(", ", blankFieldNames)}' vide(s).");
                ExtractionErrorLogging.Log(logger, error);
                errors.Add(error);
                blockIndex++;
                continue;
            }

            isolements.Add(new IsolementPivot(repere, designation, typeElement!, positionALaPose!, ""));

            foreach (var colonneName in sheetRule.UnconditionalColonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            var extractedFields = new Dictionary<string, string> { [IsolementFieldNames.TypeElement] = typeElement! };
            var (colonneNames, warning) = ConditionalPointGroupEvaluator.Evaluate(
                conditionalPointRuleEvaluator, pointRuleGroups, extractedFields);
            foreach (var colonneName in colonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            if (warning is not null)
            {
                var error = new ExtractionError(sheet, repere, ExtractionErrorCode.UnrecognizedTypeElement, warning);
                ExtractionErrorLogging.Log(logger, error);
                errors.Add(error);
            }

            blockIndex++;
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

    private static BlockFieldDefinition FindField(RepeatingBlockLocator locator, string name) =>
        locator.Fields.First(f => f.Name == name);
}
