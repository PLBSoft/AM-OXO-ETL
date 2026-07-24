using System.Diagnostics;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using Microsoft.Extensions.Logging;

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
//
// ILogger<T> injected/logged the same way as ImportPipelineOrchestrator (Lot G1/G2, mirrored here at
// Lot K0bis so the generation half of the pipeline isn't observability-blind once exposed over HTTP).
public sealed class SheetGenerationEngine(ILogger<SheetGenerationEngine> logger) : ISheetGenerationEngine
{
    public GeneratedWorkbook Generate(ImportResult importResult, ExportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(importResult);
        ArgumentNullException.ThrowIfNull(profile);

        logger.LogInformation("Starting sheet generation for profile {ProfileName}", profile.Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var sheets = profile.SheetRules.SelectMany(rule => GenerateSheets(rule, importResult)).ToList();
            var totalRowCount = sheets.Sum(sheet => sheet.Rows.Count);

            logger.LogInformation(
                "Completed sheet generation for profile {ProfileName} in {ElapsedMs}ms: {SheetCount} sheet(s), " +
                "{TotalRowCount} row(s)",
                profile.Name, stopwatch.ElapsedMilliseconds, sheets.Count, totalRowCount);

            return new GeneratedWorkbook(sheets);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Sheet generation for profile {ProfileName} failed unexpectedly after {ElapsedMs}ms",
                profile.Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    // TacheMultiple produces zero-to-many physical sheets per rule (one per distinct
    // TypeTacheMultipleCode encountered at runtime) -- Equipement/Isolement always produce exactly one,
    // matching this rule's own SheetName. This dynamic-grouping mechanic is deliberately specific to
    // TacheMultiple, not a generic primitive offered to the other two pivot sources (see the ticket's
    // own "note de conception", docs/tickets-tdd-export-taches-multiples.md T3).
    private static IEnumerable<GeneratedSheet> GenerateSheets(SheetGenerationRule rule, ImportResult importResult) =>
        rule.PivotSource == PivotSource.TacheMultiple
            ? GenerateTacheMultipleSheets(rule, importResult)
            : [GenerateSheet(rule, importResult)];

    // Sheets are ordered alphabetically by their raw (pre-sanitization) code for a deterministic,
    // reproducible output -- not by Dictionary/GroupBy encounter order. Rows within a sheet keep
    // ImportResult.TachesMultiples' own order (extraction order), including EstFactice rows in their
    // original position -- "fidélité à la structure source" per the ticket's own decision, not a sort
    // by Ordre (which is null for factice rows and would misplace them).
    private static IEnumerable<GeneratedSheet> GenerateTacheMultipleSheets(SheetGenerationRule rule, ImportResult importResult)
    {
        var headers = rule.ColumnDefinitions.Select(column => column.Header).ToList();

        return importResult.TachesMultiples
            .GroupBy(tache => tache.TypeTacheMultipleCode)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group.Select(tache => new GeneratedRow(
                    [.. rule.ColumnDefinitions.Select(column =>
                        column.Source is null ? string.Empty : PivotFieldResolver.Resolve(tache, column.Source.Value))]))
                    .ToList();

                return new GeneratedSheet(ExcelSheetNameSanitizer.Sanitize(group.Key), headers, rows);
            });
    }

    private static GeneratedSheet GenerateSheet(SheetGenerationRule rule, ImportResult importResult)
    {
        var headers = rule.ColumnDefinitions.Select(column => column.Header)
            .Concat(rule.ApplicationColumnDefinitions.Select(application => application.Header))
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
        var applicationCells = rule.ApplicationColumnDefinitions.Select(
            application => HasApplication(equipement.Applications, application.ApplicationNom) ? application.MarkValue : string.Empty);
        var pointCells = rule.PointColumnDefinitions.Select(
            point => HasPoint(importResult.Points, equipement.Repere, point.ColonneNom) ? point.MarkValue : string.Empty);

        return [new GeneratedRow([.. descriptiveCells, .. applicationCells, .. pointCells])];
    }

    private static List<GeneratedRow> GenerateIsolementRows(SheetGenerationRule rule, ImportResult importResult) =>
        importResult.Isolements.Select(isolement =>
        {
            var descriptiveCells = rule.ColumnDefinitions.Select(
                column => column.Source is null ? string.Empty : PivotFieldResolver.Resolve(isolement, column.Source.Value));
            var applicationCells = rule.ApplicationColumnDefinitions.Select(
                application => HasApplication(isolement.Applications, application.ApplicationNom) ? application.MarkValue : string.Empty);
            var pointCells = rule.PointColumnDefinitions.Select(
                point => HasPoint(importResult.Points, isolement.Repere, point.ColonneNom) ? point.MarkValue : string.Empty);

            return new GeneratedRow([.. descriptiveCells, .. applicationCells, .. pointCells]);
        }).ToList();

    private static bool HasPoint(IReadOnlyList<PointPivot> points, string parentRepere, string colonneNom) =>
        points.Any(point => point.ParentRepere == parentRepere && point.ColonneNom == colonneNom);

    // Trimmed + case-insensitive, same recommendation transverse as TypeElement/Colonne.Nom comparisons
    // elsewhere in the pipeline (spec §7) -- real fixtures/profiles can differ by trailing whitespace or
    // casing.
    private static bool HasApplication(IReadOnlyList<string> applications, string applicationNom) =>
        applications.Any(application => string.Equals(application.Trim(), applicationNom.Trim(), StringComparison.OrdinalIgnoreCase));
}
