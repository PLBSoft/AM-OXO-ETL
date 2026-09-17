using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Procedure;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 078.1 (docs/tickets/tickets-tdd-lot-078-vue-details-langage-courant-profil-import.md, D2):
// which parts of a SheetExtractionRule each extraction service actually reads. A member left out here
// is stored with the profile but has no effect on extraction -- the Details page reports it as ignored
// instead of describing it.
//
// Duplicated knowledge, on purpose: the real behavior lives in the 5 extraction services and the
// sheet-name literals in ImportPipelineOrchestrator (private, unreachable from BlazorAdmin), the same
// trade-off KnownHeaderFieldNames already made. ImportSheetUsageTests runs the real pipeline with every
// "not read" member filled in, so a service that starts reading one breaks the build here first.
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
            [SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules],
            [
                new(ProcedureHeaderFieldNames.NomMad, HeaderRole.EquipementRepere),
                new(ProcedureHeaderFieldNames.Revision, HeaderRole.Revision),
                new(ProcedureHeaderFieldNames.DateRev, HeaderRole.RevisionDate)
            ],
            [new(ProcedureHeaderFieldNames.Designation, HeaderRole.EquipementDesignation)]) {
                ItemKind = BlockItemKind.Task,
                FixedBehaviors =
                [
                    new(FixedBehaviorKind.UnreadableRevisionDateRejectsFile),
                    new(FixedBehaviorKind.TaskTypeMadRelMapping),
                    new(FixedBehaviorKind.TaskWithoutOrdreIsSectionTitle)
                ]
            },
        [Isolement] = new(
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.UnconditionalColonnes, SheetRuleMember.ConditionalPointRules,
                SheetRuleMember.ZeroEnergieExpectedValue
            ],
            [], []) { FixedBehaviors = [new(FixedBehaviorKind.ElementRepereFromCell, "K6:T6")] },
        [Platines] = PlatinesStyle(),
        [OrificesCapacites] = PlatinesStyle(),
        [AutresJointsTouches] = new(
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes,
                SheetRuleMember.ConditionalPointRules, SheetRuleMember.CouleurEtiquette
            ],
            RepereEchoOnly, []),
        [Divers] = new(
            [
                SheetRuleMember.BlockLocator, SheetRuleMember.HeaderRules, SheetRuleMember.UnconditionalColonnes,
                SheetRuleMember.ConditionalPointRules
            ],
            RepereEchoOnly, []) { FixedBehaviors = [new(FixedBehaviorKind.ZoneFromCell, "B6:E6")] },
    };

    // PLATINES and ORIFICES CAPACITES share UnconditionalIsolementSheetExtractionService.
    private static ImportSheetUsageEntry PlatinesStyle() => new(
        [
            SheetRuleMember.BlockLocator, SheetRuleMember.UnconditionalColonnes, SheetRuleMember.FieldPresencePointRules,
            SheetRuleMember.CouleurEtiquette
        ],
        [], []) { FixedBehaviors = [new(FixedBehaviorKind.ElementRepereFromCell, "K6:U6")] };

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

    // Behaviors coded in the extraction service, not editable in the profile (D3). Ranges copied from
    // IsolementExtractionService (K6:T6), UnconditionalIsolementSheetExtractionService (K6:U6) and
    // DiversExtractionService (B6:E6); the others from ProcedureExtractionService.
    public IReadOnlyList<FixedBehavior> FixedBehaviors { get; init; } = [];

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
    ElementRepereFromCell,
    ZoneFromCell,
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
    FieldPresencePointRules,
    ZeroEnergieExpectedValue,
    // CouleurEtiquetteCell, DefaultCouleurEtiquette and AllowedCouleursEtiquette, read together by
    // CouleurEtiquetteResolver.
    CouleurEtiquette
}

public enum HeaderRole
{
    EquipementRepere,
    Revision,
    RevisionDate,
    EquipementDesignation,
    RepereEcho
}
