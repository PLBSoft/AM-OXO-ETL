using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo.Platines;

// Unlike PROCEDURE/ISOLEMENT, every PLATINES field is genuinely required (confirmed against all 3
// real fixtures -- no blanks observed), and every one of its 7 Colonnes is unconditional (the FIN
// variants are deliberately excluded, spec §3) -- so this service delegates the block walk to the
// shared IRepeatingBlockReader and needs no IConditionalPointRuleEvaluator at all.
public sealed class PlatinesExtractionService(IRepeatingBlockReader repeatingBlockReader, ITextTransformEvaluator textTransformEvaluator)
    : IPlatinesExtractionService
{
    public IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        var equipementRepere = workbookReader.ReadCellValue(sheet, "K6:U6") ?? "";

        var blockResult = repeatingBlockReader.Read(sheetRule.Locator, workbookReader);

        var isolements = new List<IsolementPivot>();
        var points = new List<PointPivot>();

        foreach (var block in blockResult.Blocks)
        {
            var repere = ComposeRepere(equipementRepere, block[PlatinesFieldNames.Identification]);
            isolements.Add(new IsolementPivot(
                repere, block[PlatinesFieldNames.Designation], block[PlatinesFieldNames.TypeElement],
                positionALaPose: "", localisation: ""));

            foreach (var colonneName in sheetRule.UnconditionalColonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }
        }

        return new IsolementSheetExtractionResult(isolements, points, blockResult.Errors);
    }

    private string ComposeRepere(string equipementRepere, string identification)
    {
        var extractedFields = new Dictionary<string, string>
        {
            ["EquipementRepere"] = equipementRepere,
            [PlatinesFieldNames.Identification] = identification
        };
        var transform = new Concat(
        [
            new FieldRef("EquipementRepere"),
            new Literal("-"),
            new FieldRef(PlatinesFieldNames.Identification)
        ]);
        var (repere, _) = textTransformEvaluator.Evaluate(transform, rawValue: null, extractedFields);
        return repere!;
    }
}
