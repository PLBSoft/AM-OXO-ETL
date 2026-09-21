using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.Procedure;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078.1 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md, D2):
// which parts of a SheetExtractionRule extraction actually reads. A member left out here is stored with
// the profile but has no effect -- the Details page reports it as ignored instead of describing it.
//
// Since lot 084 there are only two forms: PROCEDURE (tasks, ProcedureExtractionService) and the five
// element sheets (one engine, ElementSheetExtractionService). What stays coded in the services is kept
// here: the task/element vocabulary, PROCEDURE's fixed behaviors and the header roles (repereEcho,
// DIVERS' zone). Duplicated knowledge, on purpose: the sheet-name literals live in
// ImportPipelineOrchestrator (private). ImportSheetUsageTests runs the real pipeline with every "not
// read" member filled in, so extraction starting to read one breaks the build here first.
public static class ImportSheetUsage
{
    private const string Procedure = "PROCEDURE";
    private const string Isolement = "ISOLEMENT";
    private const string Platines = "PLATINES";
    private const string OrificesCapacites = "ORIFICES CAPACITES";
    private const string AutresJointsTouches = "AUTRES JOINTS TOUCHES";
    private const string Divers = "DIVERS";

    // Pipeline order (ImportPipelineOrchestrator.Run).
    public static IReadOnlyList<string> KnownSheetNames { get; } =
        [Procedure, Isolement, Platines, OrificesCapacites, AutresJointsTouches, Divers];

    private static readonly RequiredHeaderName[] RepereEchoOnly =
        [new(SharedHeaderFieldNames.RepereEcho, HeaderRole.RepereEcho)];

    // Ordinal, like the orchestrator's own SheetName lookup.
    private static readonly Dictionary<string, ImportSheetUsageEntry> BySheetName = new(StringComparer.Ordinal)
    {
        [Procedure] = new(
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes,
                SheetRuleMember.ConditionalPointRules
            ],
            [
                new(ProcedureHeaderFieldNames.NomMad, HeaderRole.EquipementRepere),
                new(ProcedureHeaderFieldNames.Revision, HeaderRole.Revision),
                new(ProcedureHeaderFieldNames.DateRev, HeaderRole.RevisionDate)
            ],
            [new(ProcedureHeaderFieldNames.Designation, HeaderRole.EquipementDesignation)]) {
                ItemKind = BlockItemKind.Task,
                PointsTickTheEquipement = true,
                RequiredBlockFieldNames =
                [
                    ProcedureFieldNames.Action, ProcedureFieldNames.Ordre, ProcedureFieldNames.Acteur, ProcedureFieldNames.Risques,
                    ProcedureFieldNames.TypeTacheMultipleAlias, ProcedureFieldNames.DateValidation
                ],
                FixedBehaviors =
                [
                    new(FixedBehaviorKind.UnreadableRevisionDateRejectsFile),
                    new(FixedBehaviorKind.TaskTypeMadRelMapping),
                    new(FixedBehaviorKind.TaskWithoutOrdreIsSectionTitle)
                ]
            },
        // Lot 084.6: the five element sheets share ElementSheetExtractionService -- one shape.
        [Isolement] = ElementSheet(),
        [Platines] = ElementSheet(),
        [OrificesCapacites] = ElementSheet(),
        [AutresJointsTouches] = ElementSheet(),
        // G16: the zone broadcast on the whole run is DIVERS' "zone" header field.
        [Divers] = ElementSheet() with { OptionalHeaderFields = [new(ElementFieldNames.ZoneHeader, HeaderRole.Zone)] },
    };

    // Every setting of an element sheet is read.
    private static ImportSheetUsageEntry ElementSheet() => new(
        [
            SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes,
            SheetRuleMember.ConditionalPointRules, SheetRuleMember.CouleurEtiquette
        ],
        RepereEchoOnly, []);

    // Null when the sheet name isn't one the import pipeline processes.
    public static ImportSheetUsageEntry? For(string sheetName) => BySheetName.GetValueOrDefault(sheetName);

    public static bool IsProcessed(string sheetName) => BySheetName.ContainsKey(sheetName);
}

public sealed record ImportSheetUsageEntry(
    IReadOnlySet<SheetRuleMember> ReadMembers,
    IReadOnlyList<RequiredHeaderName> RequiredHeaderFields,
    IReadOnlyList<RequiredHeaderName> RequiredHeaderComposites)
{
    // What one repeating block holds, for the Details page vocabulary ("une tâche" / "un élément").
    public BlockItemKind ItemKind { get; init; } = BlockItemKind.Element;

    // True when the Points are created on the Equipement itself rather than on each block item: PROCEDURE's
    // unconditional Colonnes (lot 082) and its conditional rules, satisfied when at least one real task
    // matches, with no warning otherwise (lot 083) -- ProcedureExtractionService.
    public bool PointsTickTheEquipement { get; init; }

    // Behaviors coded in the extraction service, not editable in the profile (D3) -- all from
    // ProcedureExtractionService since lot 084 (the element sheets' repère and zone are header fields).
    public IReadOnlyList<FixedBehavior> FixedBehaviors { get; init; } = [];

    // Header fields read by name when present, never required (DIVERS' zone).
    public IReadOnlyList<RequiredHeaderName> OptionalHeaderFields { get; init; } = [];

    // The block field whose blank value ends the reading -- fixed by the services, not a setting (lot 084,
    // G12): PROCEDURE walks its tasks until Action is blank, the element sheets until Identification is.
    public string StopFieldName => ItemKind == BlockItemKind.Task ? ProcedureFieldNames.Action : ElementFieldNames.Identification;

    // Block field names the service looks up by name -- a missing one makes extraction fail.
    public IReadOnlyList<string> RequiredBlockFieldNames { get; init; } = ElementBlockFieldNames;

    // ElementSheetExtractionService needs these two; every other known field is optional.
    private static readonly string[] ElementBlockFieldNames =
        [ElementFieldNames.Identification, ElementFieldNames.TypeElement];

    public ImportSheetUsageEntry(
        SheetRuleMember[] readMembers, RequiredHeaderName[] requiredHeaderFields, RequiredHeaderName[] requiredHeaderComposites)
        : this(new HashSet<SheetRuleMember>(readMembers), requiredHeaderFields, requiredHeaderComposites)
    {
    }
}

public enum BlockItemKind
{
    Task,
    Element
}

public sealed record FixedBehavior(FixedBehaviorKind Kind, string? Range = null);

public enum FixedBehaviorKind
{
    UnreadableRevisionDateRejectsFile,
    TaskTypeMadRelMapping,
    TaskWithoutOrdreIsSectionTitle
}

public sealed record RequiredHeaderName(string Name, HeaderRole Role);

public enum SheetRuleMember
{
    BlockLocator,
    HeaderRules,
    UnconditionalColonnes,
    ConditionalPointRules,
    // The "CouleurEtiquette" block field, DefaultCouleurEtiquette and AllowedCouleursEtiquette, read together
    // by ElementSheetExtractionService (lot 084, G10).
    CouleurEtiquette
}

public enum HeaderRole
{
    EquipementRepere,
    Revision,
    RevisionDate,
    EquipementDesignation,
    RepereEcho,
    Zone
}
