using System.Diagnostics;
using ExcelETL.Application.Extraction.Oxo.AutresJointsTouches;
using ExcelETL.Application.Extraction.Oxo.Divers;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Pivot;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Extraction.Oxo;

// Runs the 6 per-sheet services (Lot C) and aggregates their contributions into one ImportResult.
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
    IIsolementExtractionService isolementExtractionService,
    IUnconditionalIsolementSheetExtractionService unconditionalIsolementSheetExtractionService,
    IAutresJointsTouchesExtractionService autresJointsTouchesExtractionService,
    IDiversExtractionService diversExtractionService,
    ILogger<ImportPipelineOrchestrator> logger)
    : IImportPipelineOrchestrator
{
    private const string ProcedureSheetName = "PROCEDURE";
    private const string IsolementSheetName = "ISOLEMENT";
    private const string PlatinesSheetName = "PLATINES";
    private const string OrificesCapacitesSheetName = "ORIFICES CAPACITES";
    private const string AutresJointsTouchesSheetName = "AUTRES JOINTS TOUCHES";
    private const string DiversSheetName = "DIVERS";

    // A successful run always processes exactly these 6 sheets -- PROCEDURE plus the other 5,
    // unconditionally, once PROCEDURE itself succeeds. Not derived from a collection count because
    // there's no single list of "the 6 sheets" in this class (PLATINES/ORIFICES CAPACITES share one
    // service call each, see below), so a literal is clearer than reconstructing one just to count it.
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
                workbookReader, FindRule(profile, ProcedureSheetName), profile.ReperePrefix, profile.EquipementTypeElementNom,
                profile.DefaultTableaux);

            if (procedureResult.Equipement is null)
            {
                logger.LogWarning(
                    "Import pipeline run for profile {ProfileName} rejected the whole file after {ElapsedMs}ms: " +
                    "{ErrorCount} blocking error(s)",
                    profile.Name, stopwatch.ElapsedMilliseconds, procedureResult.Errors.Count);
                return procedureResult;
            }

            var isolementResult = isolementExtractionService.Extract(workbookReader, FindRule(profile, IsolementSheetName));
            var platinesResult = unconditionalIsolementSheetExtractionService.Extract(workbookReader, FindRule(profile, PlatinesSheetName));
            var orificesCapacitesResult = unconditionalIsolementSheetExtractionService.Extract(
                workbookReader, FindRule(profile, OrificesCapacitesSheetName));
            var autresJointsTouchesResult = autresJointsTouchesExtractionService.Extract(
                workbookReader, FindRule(profile, AutresJointsTouchesSheetName), profile.ReperePrefix);
            var diversResult = diversExtractionService.Extract(
                workbookReader, FindRule(profile, DiversSheetName), profile.ReperePrefix);

            var loc1 = diversResult.Loc1;
            var repereParent = procedureResult.Equipement.Repere;
            var equipement = procedureResult.Equipement with
            {
                Localisation = loc1,
                Tableaux = profile.DefaultTableaux,
                Applications = profile.DefaultApplicationNames
            };

            var isolements = new List<IsolementPivot>();
            isolements.AddRange(isolementResult.Isolements);
            isolements.AddRange(platinesResult.Isolements);
            isolements.AddRange(orificesCapacitesResult.Isolements);
            isolements.AddRange(autresJointsTouchesResult.Isolements);
            isolements.AddRange(diversResult.Isolements);
            BroadcastEquipementContext(isolements, loc1, profile, repereParent);

            var points = new List<PointPivot>();
            points.AddRange(procedureResult.Points);
            points.AddRange(isolementResult.Points);
            points.AddRange(platinesResult.Points);
            points.AddRange(orificesCapacitesResult.Points);
            points.AddRange(autresJointsTouchesResult.Points);
            points.AddRange(diversResult.Points);

            var errors = new List<ExtractionError>();
            errors.AddRange(procedureResult.Errors);
            errors.AddRange(isolementResult.Errors);
            errors.AddRange(platinesResult.Errors);
            errors.AddRange(orificesCapacitesResult.Errors);
            errors.AddRange(autresJointsTouchesResult.Errors);
            errors.AddRange(diversResult.Errors);

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
