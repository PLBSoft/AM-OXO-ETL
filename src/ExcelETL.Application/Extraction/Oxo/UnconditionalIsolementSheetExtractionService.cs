using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Shared implementation for isolement-style sheets where every field is genuinely required and
// every Colonne is unconditional -- confirmed identical mechanics (Step, K6:U6 repere echo, field
// offsets) between PLATINES (Lot C3) and ORIFICES CAPACITES (Lot C4), per the spec's own cell
// ranges, so this reuses one implementation configured purely through SheetExtractionRule rather
// than duplicating a second near-identical service. Originally named PlatinesExtractionService
// before ORIFICES CAPACITES confirmed the same shape; renamed rather than duplicated. Not reused
// for AUTRES JOINTS TOUCHES (Lot C5, one conditional Colonne) or DIVERS (Lot C6, several) --
// those need IConditionalPointRuleEvaluator, which this class deliberately has no dependency on.
//
// PLATINES client feedback (2026-09): FieldPresencePointRules is the one exception to "every Colonne
// is unconditional" -- it stays fully config-driven (an empty list, ORIFICES CAPACITES' case today,
// is a true no-op) so this service still needs no ConditionalPointRuleEvaluator/warning-aggregation
// dependency: presence-vs-blank isn't a "recognized value" question, so a blank cell (the overwhelming
// majority in every real fixture) is not reported as a warning, unlike ConditionalPointRule's own
// NoConditionalPointCreated path.
public sealed class UnconditionalIsolementSheetExtractionService(
    IRepeatingBlockReader repeatingBlockReader,
    ITextTransformEvaluator textTransformEvaluator,
    ILogger<UnconditionalIsolementSheetExtractionService> logger)
    : IUnconditionalIsolementSheetExtractionService
{
    public IsolementSheetExtractionResult Extract(IWorkbookReader workbookReader, SheetExtractionRule sheetRule)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);

        var sheet = sheetRule.SheetName;
        var equipementRepere = workbookReader.ReadCellValue(sheet, "K6:U6") ?? "";

        var blockResult = repeatingBlockReader.Read(sheetRule.Locator, workbookReader);
        var errors = new List<ExtractionError>(blockResult.Errors);
        foreach (var error in blockResult.Errors)
        {
            ExtractionErrorLogging.Log(logger, error);
        }

        var isolements = new List<IsolementPivot>();
        var points = new List<PointPivot>();
        var couleurEtiquetteWarningTracker = new UnexpectedCouleurEtiquetteValueWarningTracker(sheet);

        foreach (var block in blockResult.Blocks)
        {
            var repere = ComposeRepere(equipementRepere, block.Fields[IsolementFieldNames.Identification]);
            var (couleurEtiquette, unexpectedCouleurEtiquetteValue) =
                CouleurEtiquetteResolver.Resolve(workbookReader, sheet, sheetRule, block.StartRow);
            if (unexpectedCouleurEtiquetteValue is not null)
            {
                couleurEtiquetteWarningTracker.RecordIfNew(repere, unexpectedCouleurEtiquetteValue, logger, errors);
            }

            isolements.Add(new IsolementPivot(
                repere, block.Fields[IsolementFieldNames.Designation], block.Fields[IsolementFieldNames.TypeElement],
                positionALaPose: "", localisation: "", couleurEtiquette: couleurEtiquette, sourceSheetName: sheetRule.SheetName));

            foreach (var colonneName in sheetRule.UnconditionalColonneNames)
            {
                points.Add(new PointPivot(colonneName, repere));
            }

            // Several rules may target the same Colonne (e.g. "DEBUT MAD in POSÉE LE or DÉPOSÉE LE"):
            // a block never gets the same Point twice, whichever rules matched.
            var fieldPresenceColonnesCreated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in sheetRule.FieldPresencePointRules)
            {
                var cellValue = workbookReader.ReadCellValue(
                    sheet, BlockFieldRangeCalculator.BuildRange(rule.Cell, block.StartRow));
                if (IsFieldPresenceSatisfied(cellValue, rule.ExpectedValue)
                    && fieldPresenceColonnesCreated.Add(rule.ColonneName))
                {
                    points.Add(new PointPivot(rule.ColonneName, repere));
                }
            }
        }

        return new IsolementSheetExtractionResult(isolements, points, errors);
    }

    // No ExpectedValue: any non-blank value satisfies the rule (historical behavior). With one: the
    // trimmed cell must equal it, ignoring case (spec §7). A different value is not a warning -- a
    // FIN MAD in POSÉE LE is legitimate data that simply doesn't tick the DEBUT Colonne.
    private static bool IsFieldPresenceSatisfied(string? cellValue, string? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue))
        {
            return false;
        }

        return expectedValue is null
            || string.Equals(cellValue.Trim(), expectedValue.Trim(), StringComparison.OrdinalIgnoreCase);
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
