using System.Globalization;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo.Procedure;

// PROCEDURE is the one sheet that can't reuse IRepeatingBlockReader: every field in its TacheMultiple
// block except Action (the stop field) is genuinely optional at the pivot level -- not just Ordre
// (whose blankness is the documented "ligne de mise en page" rule, spec §1.2), but also
// Acteur/Risques/TypeTacheMultipleCode/DateValidation, which TacheMultiplePivot itself leaves
// unvalidated. RepeatingBlockReader's shared policy (any non-stop field blank => report an error and
// drop the whole block) would wrongly reject/skip perfectly valid rows, so this service walks the
// block itself, via BlockFieldRangeCalculator for the range math only.
//
// The Equipement's TypeElement.Nom (e.g. "MAD TRAVAUX" for a MAD dossier, still unconfirmed for a
// future REL dossier -- see spec §0/§9) is never a constant here: it comes from
// ImportProfile.EquipementTypeElementNom (model doc v2 §2.1), since the client confirmed this value
// varies by profile, not by any cell in PROCEDURE.
public sealed class ProcedureExtractionService(
    ITextTransformEvaluator textTransformEvaluator, ILogger<ProcedureExtractionService> logger)
    : IProcedureExtractionService
{
    private static readonly string[] DateFormats = ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy"];

    public ImportResult Extract(
        IWorkbookReader workbookReader, SheetExtractionRule sheetRule, string reperePrefix, string equipementTypeElementNom,
        IReadOnlyList<string> defaultTableaux)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(sheetRule);
        ArgumentNullException.ThrowIfNull(defaultTableaux);

        var sheet = sheetRule.SheetName;

        var repereRaw = workbookReader.ReadCellValue(sheet, "M2:O2");
        if (string.IsNullOrWhiteSpace(repereRaw))
        {
            return Rejected(sheet, "M2:O2", ExtractionErrorCode.RequiredFieldMissing,
                "Cellule M2:O2 (repère de l'équipement) introuvable ou vide.");
        }

        var (repere, prefixError) = textTransformEvaluator.Evaluate(
            new SubstringAfter(reperePrefix), repereRaw, new Dictionary<string, string>());
        if (prefixError is not null || string.IsNullOrWhiteSpace(repere))
        {
            return Rejected(sheet, "M2:O2", ExtractionErrorCode.UnparsableValue,
                prefixError ?? "Cellule M2:O2 (repère de l'équipement) vide après retrait du préfixe.");
        }

        var dateRevisionRaw = workbookReader.ReadCellValue(sheet, "R2:T2");
        if (!TryParseDate(dateRevisionRaw, out var dateRevision))
        {
            return Rejected(sheet, "R2:T2", ExtractionErrorCode.UnparsableValue,
                $"Cellule R2:T2 (date de révision) introuvable ou illisible : '{dateRevisionRaw}'.");
        }

        var numeroRevision = workbookReader.ReadCellValue(sheet, "P2:Q2") ?? "";
        var designation = BuildDesignation(numeroRevision, dateRevision);

        var equipement = new EquipementPivot(repere, designation, equipementTypeElementNom);
        var points = defaultTableaux.Select(tableauName => new PointPivot(tableauName, repere)).ToList();
        var tachesMultiples = ReadTachesMultiples(workbookReader, sheetRule.Locator);
        var typeCoherenceErrors = DetectTypeIncoherences(sheet, tachesMultiples);

        return new ImportResult(equipement, [], points, tachesMultiples, typeCoherenceErrors);
    }

    // Lot 032 (docs/tickets-tdd-lot-032-detection-incoherence-type-procedure.md): a client
    // data-entry-quality guard-rail cabled directly here (ticket decision 3), not a business rule
    // generalized into ImportProfile/SheetExtractionRule -- consistent with the pre-existing
    // Ordre/ligne factice rule on this same sheet. Non-blocking (decision 8): the TacheMultiplePivots
    // themselves are always extracted normally regardless of what this detects.
    private List<ExtractionError> DetectTypeIncoherences(string sheet, IReadOnlyList<TacheMultiplePivot> tachesMultiples)
    {
        var errors = new List<ExtractionError>();

        foreach (var section in TacheMultipleSectionGrouper.GroupBySection(tachesMultiples))
        {
            var analysis = TacheMultipleTypeCoherenceAnalyzer.Analyze(section.Tasks);
            foreach (var error in BuildTypeIncoherenceErrors(sheet, section.Title, analysis))
            {
                ExtractionErrorLogging.Log(logger, error);
                errors.Add(error);
            }
        }

        return errors;
    }

    private static IReadOnlyList<ExtractionError> BuildTypeIncoherenceErrors(
        string sheet, string sectionTitle, TacheMultipleTypeCoherenceAnalysis analysis)
    {
        if (analysis.AmbiguousGroups.Count > 0)
        {
            var groupsText = JoinWithEt(analysis.AmbiguousGroups
                .Select(group => $"{group.Type} ({string.Join(", ", group.Runs.Select(FormatRange))})")
                .ToList());
            var message =
                $"Répartition de TYPE ambiguë dans la tâche multiple \"{sectionTitle}\" : {groupsText} " +
                "se partagent la section à parts égales — impossible de déterminer le type correct, vérifier manuellement.";

            return [new ExtractionError(
                sheet, sectionTitle, ExtractionErrorCode.TypeIncoherenceDansTacheMultiple, message)];
        }

        var errors = new List<ExtractionError>();
        foreach (var anomaly in analysis.MinorityRunAnomalies)
        {
            var run = anomaly.Run;
            var blockIdentifier = $"{sectionTitle} (tâches {run.OrdreDebut}-{run.OrdreFin})";
            var message = anomaly.Position == TypeRunPosition.Sandwich
                ? $"Incohérence de TYPE détectée dans la tâche multiple \"{sectionTitle}\" : tâches " +
                  $"{FormatRange(run)} en {run.Type}, encadrées par des tâches en {analysis.MajorityType} " +
                  "— vérifier une possible erreur de saisie."
                : $"Incohérence de TYPE détectée dans la tâche multiple \"{sectionTitle}\" : tâches " +
                  $"{FormatRange(run)} en {run.Type}, en {DebutOuFin(anomaly.Position)} de section, adjacentes " +
                  $"à des tâches en {analysis.MajorityType} — vérifier une possible erreur de saisie.";

            errors.Add(new ExtractionError(
                sheet, blockIdentifier, ExtractionErrorCode.TypeIncoherenceDansTacheMultiple, message));
        }

        return errors;
    }

    private static string DebutOuFin(TypeRunPosition position) =>
        position == TypeRunPosition.DebutDeSection ? "début" : "fin";

    private static string FormatRange(TypeRun run) => $"{run.OrdreDebut}–{run.OrdreFin}";

    private static string JoinWithEt(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " et " + items[^1];

    private string BuildDesignation(string numeroRevision, DateOnly dateRevision)
    {
        var extractedFields = new Dictionary<string, string>
        {
            ["NumeroRevision"] = numeroRevision,
            ["DateRevision"] = dateRevision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        };
        var transform = new Concat(
        [
            new Literal("Rév "),
            new FieldRef("NumeroRevision"),
            new Literal(" du "),
            new FieldRef("DateRevision")
        ]);
        var (designation, _) = textTransformEvaluator.Evaluate(transform, rawValue: null, extractedFields);
        return designation!;
    }

    private List<TacheMultiplePivot> ReadTachesMultiples(IWorkbookReader workbookReader, RepeatingBlockLocator locator)
    {
        var actionField = FindField(locator, ProcedureFieldNames.Action);
        var ordreField = FindField(locator, ProcedureFieldNames.Ordre);
        var acteurField = FindField(locator, ProcedureFieldNames.Acteur);
        var risquesField = FindField(locator, ProcedureFieldNames.Risques);
        var aliasField = FindField(locator, ProcedureFieldNames.TypeTacheMultipleAlias);
        var dateValidationField = FindField(locator, ProcedureFieldNames.DateValidation);

        var tachesMultiples = new List<TacheMultiplePivot>();
        var blockIndex = 0;

        while (true)
        {
            var blockStartRow = locator.FirstBlockStartRow + blockIndex * locator.Step;
            var action = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(actionField, blockStartRow));

            if (string.IsNullOrWhiteSpace(action))
            {
                break;
            }

            var ordreRaw = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(ordreField, blockStartRow));
            var acteur = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(acteurField, blockStartRow)) ?? "";
            var risques = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(risquesField, blockStartRow)) ?? "";
            var aliasRaw = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(aliasField, blockStartRow));
            var dateValidationRaw = workbookReader.ReadCellValue(
                locator.Sheet, BlockFieldRangeCalculator.BuildRange(dateValidationField, blockStartRow));

            var estFactice = string.IsNullOrWhiteSpace(ordreRaw);
            var ordre = int.TryParse(ordreRaw, out var parsedOrdre) ? parsedOrdre : (int?)null;
            var typeTacheMultipleCode = MapTypeTacheMultipleAlias(aliasRaw);
            var dateValidation = TryParseDate(dateValidationRaw, out var parsedDate) ? parsedDate : (DateOnly?)null;

            tachesMultiples.Add(new TacheMultiplePivot(
                ordre, action, acteur, risques, typeTacheMultipleCode, dateValidation, estFactice));

            blockIndex++;
        }

        return tachesMultiples;
    }

    private static string MapTypeTacheMultipleAlias(string? aliasRaw)
    {
        var trimmed = aliasRaw?.Trim() ?? "";
        return trimmed.ToUpperInvariant() switch
        {
            "MAD" => "TM_PROC_MAD",
            "REL" => "TM_PROC_REL",
            _ => trimmed
        };
    }

    private static BlockFieldDefinition FindField(RepeatingBlockLocator locator, string name) =>
        locator.Fields.First(f => f.Name == name);

    private static bool TryParseDate(string? raw, out DateOnly date)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            DateTime.TryParseExact(
                raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            date = DateOnly.FromDateTime(dateTime);
            return true;
        }

        date = default;
        return false;
    }

    private ImportResult Rejected(string sheet, string range, ExtractionErrorCode code, string message)
    {
        var error = new ExtractionError(sheet, range, code, message);
        ExtractionErrorLogging.Log(logger, error);
        return new(null, [], [], [], [error]);
    }
}
