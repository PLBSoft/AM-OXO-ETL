using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.Application.Generation;

// Symmetric to the import side's RepeatingBlockReader/TextTransformEvaluator: a stateless engine that
// turns a pivot (ImportResult) + a profile (ExportProfile) into the intermediate GeneratedWorkbook
// structure -- no ClosedXML dependency at this layer, see docs/tickets-tdd-ecriture-fichier-cible.md I3.
//
// ImportResult.Equipement is null only for the whole-file-rejection case (model doc §3.1). Rather
// than omitting the Equipement sheet entirely in that case (which would make the generated workbook's
// sheet count vary depending on upstream extraction success), an Equipement-sourced sheet still gets
// its header row with zero data rows -- a stable, predictable output shape, matching the import side's
// existing precedent that an empty sheet is a legitimate result, not an error (ORIFICES CAPACITES for
// the C7401 fixture, Lot C4).
public sealed class SheetGenerationEngine : ISheetGenerationEngine
{
    public GeneratedWorkbook Generate(ImportResult importResult, ExportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(importResult);
        ArgumentNullException.ThrowIfNull(profile);

        var sheets = profile.SheetRules.Select(rule => GenerateSheet(rule, importResult)).ToList();
        return new GeneratedWorkbook(sheets);
    }

    private static GeneratedSheet GenerateSheet(SheetGenerationRule rule, ImportResult importResult)
    {
        var headers = rule.ColumnDefinitions.Select(column => column.Header)
            .Concat(rule.PointColumnDefinitions.Select(point => point.Header))
            .ToList();

        var rows = rule.PivotSource switch
        {
            PivotSource.Equipement => GenerateEquipementRows(rule, importResult),
            PivotSource.Isolement => GenerateIsolementRows(rule, importResult),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.PivotSource, "Unknown pivot source.")
        };

        return new GeneratedSheet(rule.SheetName, headers, rows);
    }

    private static List<GeneratedRow> GenerateEquipementRows(SheetGenerationRule rule, ImportResult importResult)
    {
        if (importResult.Equipement is null)
        {
            return [];
        }

        var equipement = importResult.Equipement;
        var descriptiveCells = rule.ColumnDefinitions.Select(
            column => column.Source is null ? string.Empty : PivotFieldResolver.Resolve(equipement, column.Source.Value));
        var pointCells = rule.PointColumnDefinitions.Select(
            point => HasPoint(importResult.Points, equipement.Repere, point.ColonneNom) ? point.MarkValue : string.Empty);

        return [new GeneratedRow([.. descriptiveCells, .. pointCells])];
    }

    private static List<GeneratedRow> GenerateIsolementRows(SheetGenerationRule rule, ImportResult importResult) =>
        importResult.Isolements.Select(isolement =>
        {
            var descriptiveCells = rule.ColumnDefinitions.Select(
                column => column.Source is null ? string.Empty : PivotFieldResolver.Resolve(isolement, column.Source.Value));
            var pointCells = rule.PointColumnDefinitions.Select(
                point => HasPoint(importResult.Points, isolement.Repere, point.ColonneNom) ? point.MarkValue : string.Empty);

            return new GeneratedRow([.. descriptiveCells, .. pointCells]);
        }).ToList();

    private static bool HasPoint(IReadOnlyList<PointPivot> points, string parentRepere, string colonneNom) =>
        points.Any(point => point.ParentRepere == parentRepere && point.ColonneNom == colonneNom);
}
