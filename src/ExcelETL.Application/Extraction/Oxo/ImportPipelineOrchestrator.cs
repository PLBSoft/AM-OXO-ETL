using System.Diagnostics;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Runs PROCEDURE, then the five element sheets through the one ElementSheetExtractionService (lot 084),
// and aggregates their contributions into one ImportResult.
// PROCEDURE runs first: per model doc §3.1, an invalid Equipement rejects the whole file (returned
// immediately, none of the other 5 services are even invoked -- not just "their output is discarded",
// see the unit tests' Mock.Verify(..., Times.Never)).
//
// Sheet roles are resolved from ImportProfile.SheetRules by matching SheetName against the 6 fixed
// literal names below. This is a deliberate simplification: SheetExtractionRule has no explicit
// "role" tag (Domain doesn't model "this rule is the ISOLEMENT-shaped one" independently of its
// configured name), and all 3 real fixtures plus the ticket doc's planned hardcoded profile use
// exactly these tab names -- revisit with a proper role enum only if a client profile ever needs to
// rename these tabs while keeping the same logical role, which nothing today requires.
public sealed class ImportPipelineOrchestrator(
    IProcedureExtractionService procedureExtractionService,
    IElementSheetExtractionService elementSheetExtractionService,
    ILogger<ImportPipelineOrchestrator> logger)
    : IImportPipelineOrchestrator
{
    private const string ProcedureSheetName = "PROCEDURE";
    private const string IsolementSheetName = "ISOLEMENT";
    private const string PlatinesSheetName = "PLATINES";
    private const string OrificesCapacitesSheetName = "ORIFICES CAPACITES";
    private const string AutresJointsTouchesSheetName = "AUTRES JOINTS TOUCHES";
    private const string DiversSheetName = "DIVERS";

    // Element sheets in pipeline order -- the order their elements, Points and errors are aggregated in.
    private static readonly string[] ElementSheetNames =
        [IsolementSheetName, PlatinesSheetName, OrificesCapacitesSheetName, AutresJointsTouchesSheetName, DiversSheetName];

    // PROCEDURE plus the five element sheets, all processed once PROCEDURE itself succeeds.
    private const int SheetsProcessedOnSuccess = 6;

    public ImportResult Run(IWorkbookReader workbookReader, ImportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(workbookReader);
        ArgumentNullException.ThrowIfNull(profile);

        logger.LogInformation("Starting import pipeline run for profile {ProfileName}", profile.Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var procedureResult = procedureExtractionService.Extract(
                workbookReader, FindRule(profile, ProcedureSheetName), profile.ReperePrefix, profile.EquipementTypeElementNom);

            if (procedureResult.Equipement is null)
            {
                logger.LogWarning(
                    "Import pipeline run for profile {ProfileName} rejected the whole file after {ElapsedMs}ms: " +
                    "{ErrorCount} blocking error(s)",
                    profile.Name, stopwatch.ElapsedMilliseconds, procedureResult.Errors.Count);
                return procedureResult;
            }

            var elementResults = ElementSheetNames.ToDictionary(
                name => name,
                name => elementSheetExtractionService.Extract(workbookReader, FindRule(profile, name), profile.ReperePrefix));

            // The zone ("loc1") broadcast on the whole run is DIVERS' one (G16).
            var loc1 = elementResults[DiversSheetName].Zone;
            var repereParent = procedureResult.Equipement.Repere;
            var equipement = procedureResult.Equipement with
            {
                Localisation = loc1,
                Tableaux = profile.DefaultTableaux,
                Applications = profile.DefaultApplicationNames
            };

            var isolements = new List<IsolementPivot>();
            var points = new List<PointPivot>(procedureResult.Points);
            var errors = new List<ExtractionError>(procedureResult.Errors);
            foreach (var name in ElementSheetNames)
            {
                isolements.AddRange(elementResults[name].Elements);
                points.AddRange(elementResults[name].Points);
                errors.AddRange(elementResults[name].Errors);
            }

            BroadcastEquipementContext(isolements, loc1, profile, repereParent);

            var tachesMultiples = BroadcastTachesMultiplesContext(
                procedureResult.TachesMultiples, equipement, profile.TacheMultipleTypeLabels);

            var totalElementCount = isolements.Count + points.Count + tachesMultiples.Count;

            logger.LogInformation(
                "Completed import pipeline run for profile {ProfileName} in {ElapsedMs}ms: {SheetCount} sheet(s) " +
                "processed, {TotalElementCount} element(s) extracted ({IsolementCount} isolement(s), " +
                "{PointCount} point(s), {TacheMultipleCount} tache(s) multiple(s)), {ErrorCount} non-blocking warning(s)",
                profile.Name, stopwatch.ElapsedMilliseconds, SheetsProcessedOnSuccess, totalElementCount,
                isolements.Count, points.Count, tachesMultiples.Count, errors.Count);

            return new ImportResult(equipement, isolements, points, tachesMultiples, errors);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Import pipeline run for profile {ProfileName} failed unexpectedly after {ElapsedMs}ms",
                profile.Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static SheetExtractionRule FindRule(ImportProfile profile, string sheetName) =>
        profile.SheetRules.First(r => r.SheetName == sheetName);

    // Broadcasts DIVERS' loc1 and the profile's DefaultTableaux/DefaultApplicationNames onto every
    // isolement of the run, plus the parent Equipement's own Repere -- "sans exception" per spec §1.5,
    // even when loc1 is blank (a no-op against the default-empty Localisation).
    private static void BroadcastEquipementContext(
        List<IsolementPivot> isolements, string loc1, ImportProfile profile, string repereParent)
    {
        for (var i = 0; i < isolements.Count; i++)
        {
            isolements[i] = isolements[i] with
            {
                Localisation = loc1,
                Tableaux = profile.DefaultTableaux,
                Applications = profile.DefaultApplicationNames,
                RepereParent = repereParent
            };
        }
    }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md):
    // Repere/TypeElementNom are broadcast from the run's single Equipement, same "sans exception"
    // convention as BroadcastEquipementContext above. ColonneTravaux is resolved by looking up each
    // tache's own TypeTacheMultipleCode in the profile's configured mapping -- trim + insensitive to
    // case, consistent with every other Colonne-name comparison in this pipeline (spec §7) -- and stays
    // "" when no configured entry matches, never an error.
    //
    // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
    // Localisation joins the same broadcast, from equipement.Localisation -- already the final,
    // DIVERS-broadcast value by the time this runs (equipement is built just above, in Run).
    private static List<TacheMultiplePivot> BroadcastTachesMultiplesContext(
        IReadOnlyList<TacheMultiplePivot> tachesMultiples, EquipementPivot equipement,
        IReadOnlyList<TacheMultipleTypeLabel> tacheMultipleTypeLabels) =>
        [.. tachesMultiples.Select(tache => tache with
        {
            Repere = equipement.Repere,
            TypeElementNom = equipement.TypeElementNom,
            ColonneTravaux = ResolveColonneTravaux(tache.TypeTacheMultipleCode, tacheMultipleTypeLabels),
            Localisation = equipement.Localisation
        })];

    private static string ResolveColonneTravaux(
        string typeTacheMultipleCode, IReadOnlyList<TacheMultipleTypeLabel> tacheMultipleTypeLabels)
    {
        var normalizedCode = typeTacheMultipleCode.Trim();
        var match = tacheMultipleTypeLabels.FirstOrDefault(
            label => string.Equals(label.Code.Trim(), normalizedCode, StringComparison.OrdinalIgnoreCase));

        return match?.Label ?? "";
    }
}
