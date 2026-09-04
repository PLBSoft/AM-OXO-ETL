using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Isolement;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Infrastructure.Seeding;

// Bootstraps the standard OXO import/export profiles this deployment relies on, so extraction and
// generation work out of the box instead of requiring a manual post-deployment step -- same
// idempotent, startup-time pattern as IdentitySeeder (see Identity/IdentitySeeder.cs), applied to
// ImportProfile/ExportProfile instead of admin accounts. See docs/tickets-tdd-seed-profils-defaut.md.
//
// Looked up by a stable, hardcoded Id, never by Name: an admin can rename a seeded profile, so Name
// is not a safe identity check. Once a profile with that Id exists, it is never touched again, no
// matter how an admin has since modified its content -- exactly IdentitySeeder's "an existing account
// is never reset" behavior, confirmed with the client for profiles too (ticket's "Décisions actées" §2).
public class DefaultProfileSeeder(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore,
    ILogger<DefaultProfileSeeder> logger)
{
    public static readonly Guid ImportProfileId = Guid.Parse("a2d81110-6ed6-4b56-ac38-59e543c79f22");
    public static readonly Guid ExportProfileId = Guid.Parse("2d0c19f0-9183-486d-8293-26993069858b");

    public const string ProfileName = "Profil OXO standard";

    // ISOLEMENT's "ZERO ENERGIE" Colonne carries a "(PS941)" suffix that DIVERS' does not -- these are
    // two genuinely distinct Colonne names in the real OXO referential, not a typo. See
    // spec-extraction-fichier-source-oxo.md §6/§7.
    private const string IsolementZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";

    // PROCEDURE's 2 unconditional Points are driven by ImportProfile.DefaultTableaux since Lot U3 (no
    // longer hardcoded in ProcedureExtractionService) -- these two consts are transcribed here purely
    // so the default export profile can reference the same Colonne names, and are also the literal
    // DefaultTableaux value seeded below.
    private const string TravauxCompletColonneName = "TRAVAUX COMPLET";
    private const string TravauxDetailColonneName = "TRAVAUX DETAIL";

    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), decision #4: the only
    // Application name seeded by default. "PROGRESS" is the legacy EF6 AMProgress Application name
    // this deployment cares about today -- an admin can add more via the profile editor.
    private const string ProgressApplicationName = "PROGRESS";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedImportProfileAsync(cancellationToken);
        await SeedExportProfileAsync(cancellationToken);
    }

    private async Task SeedImportProfileAsync(CancellationToken cancellationToken)
    {
        var existing = await importProfileStore.GetByIdAsync(ImportProfileId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await importProfileStore.SaveAsync(BuildDefaultImportProfile(), cancellationToken);
        logger.LogInformation("Seeded default import profile {ProfileId}", ImportProfileId);
    }

    private async Task SeedExportProfileAsync(CancellationToken cancellationToken)
    {
        var existing = await exportProfileStore.GetByIdAsync(ExportProfileId, cancellationToken);
        if (existing is null)
        {
            await exportProfileStore.SaveAsync(BuildDefaultExportProfile(), cancellationToken);
            logger.LogInformation("Seeded default export profile {ProfileId}", ExportProfileId);
            return;
        }

        await MigrateTacheMultipleSheetRuleIfMissingAsync(existing, cancellationToken);
    }

    // T8 (docs/tickets-tdd-export-taches-multiples.md): a profile seeded before this lot's SheetRules
    // list gained the TacheMultiple rule (Lot M) never receives it, since SeedExportProfileAsync's own
    // "never touch an existing profile" rule (client-confirmed, see this class's own header comment)
    // means the nominal seeding path above is a no-op for it forever. This is a narrow, one-time,
    // additive migration -- not a general reseed: it only ever appends the exact rule T5 already
    // defines, only when no TacheMultiple rule exists yet, and never touches the Parents/Enfants rules
    // (or any admin customization already made to them). An admin who later deliberately removes the
    // TacheMultiple rule will see it reappear on the next restart under this simple "absent => add"
    // check -- flagged in the ticket as the simplest workable rule for now, to be revisited with a
    // dedicated migration marker only if that turns out to be a real problem in practice.
    private async Task MigrateTacheMultipleSheetRuleIfMissingAsync(ExportProfile existing, CancellationToken cancellationToken)
    {
        if (existing.SheetRules.Any(rule => rule.PivotSource == PivotSource.TacheMultiple))
        {
            return;
        }

        var migrated = new ExportProfile(existing.Id, existing.Name, [.. existing.SheetRules, BuildTacheMultipleSheetRule()]);
        await exportProfileStore.SaveAsync(migrated, cancellationToken);
        logger.LogInformation(
            "Migrated default export profile {ProfileId}: added the missing TacheMultiple sheet rule", ExportProfileId);
    }

    // Coordinates transcribed from spec-extraction-fichier-source-oxo.md, verified word for word
    // against the 5 real extraction services as part of this ticket's own pre-implementation
    // checklist (docs/tickets-tdd-seed-profils-defaut.md, closing section) -- zero divergence found.
    private static ImportProfile BuildDefaultImportProfile() => new(
        ImportProfileId, ProfileName, ImportProfile.DefaultReperePrefix, "MAD TRAVAUX",
        [TravauxCompletColonneName, TravauxDetailColonneName],
        [ProgressApplicationName],
        [
            new SheetExtractionRule(
                "PROCEDURE",
                new RepeatingBlockLocator("PROCEDURE", 9, 1, ProcedureFieldNames.Action,
                [
                    new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
                ]),
                [],
                [],
                [
                    new HeaderFieldRule(
                        ProcedureHeaderFieldNames.NomMad, new DirectCell("PROCEDURE", "M2:O2"), stripReperePrefix: true),
                    new HeaderFieldRule(ProcedureHeaderFieldNames.Revision, new DirectCell("PROCEDURE", "P2:Q2")),
                    new HeaderFieldRule(
                        ProcedureHeaderFieldNames.DateRev, new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy")
                ],
                [
                    new HeaderCompositeRule(
                        ProcedureHeaderFieldNames.Designation,
                        $"Rév {{{ProcedureHeaderFieldNames.Revision}}} du {{{ProcedureHeaderFieldNames.DateRev}}}")
                ]),
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator("ISOLEMENT", 19, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4),
                    new BlockFieldDefinition(IsolementFieldNames.HasZeroEnergie, "V", -1, 0)
                ]),
                [
                    new ConditionalPointRule(
                        IsolementFieldNames.HasZeroEnergie, ConditionOperator.Equals, "true", IsolementZeroEnergieColonneName)
                ],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], [], zeroEnergieExpectedValue: "ZERO ENERGIE"),
            new SheetExtractionRule(
                "PLATINES",
                new RepeatingBlockLocator("PLATINES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS",
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "PLATINES / TAMPONS PLEINS"
                ], [], [],
                // Client feedback (2026-09): "RECEPTION DEBUT MAD"/"RECEPTION DEBUT REL" are no longer
                // created unconditionally -- they now reflect whether the source block's own "POSÉE
                // LE"/"DÉPOSÉE LE" cells were actually filled in (H, block offsets +2/+3, same H:N
                // merge width as every other value cell in this form -- confirmed against all 4 real
                // client fixtures on disk, incl. G4010A, the file behind the client's screenshot).
                // Deliberately not folded into UnconditionalColonneNames/PointRules -- neither can
                // express "Point only if this specific cell has a value at all" (PointRules always
                // compares against a fixed ComparisonValue). Presence, not the label text itself, is
                // read -- known unreliable in the DEBUT/FIN block-split anomaly (spec §3, "jugé non
                // fiable"), accepted as-is, no special handling.
                fieldPresencePointRules:
                [
                    new FieldPresencePointRule(
                        new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD"),
                    new FieldPresencePointRule(
                        new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL")
                ]),
            new SheetExtractionRule(
                "ORIFICES CAPACITES",
                new RepeatingBlockLocator("ORIFICES CAPACITES", 17, 8, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 5)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS"
                ], [], []),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator("AUTRES JOINTS TOUCHES", 17, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)
                ],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("AUTRES JOINTS TOUCHES", "N6"))],
                []),
            new SheetExtractionRule(
                "DIVERS",
                new RepeatingBlockLocator("DIVERS", 9, 3, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:G", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "H:K", 0, 2),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "L:V", 0, 2)
                ]),
                [
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    // Lot 066 (docs/tickets/tickets-tdd-lot-066-completion-colonnes-parents-enfants-export.md,
                    // 66.1): retargeted onto ISOLEMENT's own "ZERO ENERGIE" Colonne name (client decision,
                    // "fusionner les deux colonnes") -- DIVERS' "ZERO ENERGIE" TypeElement used to produce a
                    // second, differently-spelled real Colonne ("ZÉRO ENERGIE EN PRESENCE EE", no "(PS941)"
                    // suffix), confirmed by this ticket's own 66.0 investigation to be a genuinely distinct,
                    // real extraction output (not the accidental export-side duplicate the ticket originally
                    // assumed -- D8570 alone produces 13 of these). Merging both sheets onto the same target
                    // Colonne name means a single PointColumnDefinition on the export profile now covers both.
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", IsolementZeroEnergieColonneName),
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE",
                        "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU",
                        "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU",
                        "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "POINT FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                [],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("DIVERS", "N6"))],
                [])
        ],
        // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md):
        // the "Colonne Travaux" values discussed with Simon -- configuration, no longer a hardcoded
        // switch in the generation engine (see ImportPipelineOrchestrator.ResolveColonneTravaux).
        [
            new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"),
            new TacheMultipleTypeLabel("TM_PROC_REL", "Procédure REL")
        ]);

    // Minimal by design (ticket's "Décision actée" §3, client-confirmed): only the descriptive fields
    // BuildDefaultImportProfile's pivot already produces, plus every Point Colonne that profile
    // actually produces -- no Source = null placeholder columns anticipating an extraction rule that
    // doesn't exist yet. Point column Headers reuse the raw Colonne name verbatim: no localized-label
    // catalogue exists for these yet, and inventing one wasn't asked for by this ticket.
    //
    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), U6: both sheets gain a "Tableaux"
    // descriptive column (comma-joined, positioned right after "Désignation") and a "PROGRESS"
    // Application column (right after "Tableaux") -- both before the existing Point columns, which are
    // otherwise unchanged. Enfants also gains "ELEMENT PARENT" (IsolementRepereParent, between "Zone"
    // and "Désignation") and its "Type" column is renamed to "Type Elément" (same source field,
    // IsolementTypeElementNom -- decision #5, no new pivot field needed) for naming consistency with
    // Parents' own "Type Elément" column.
    //
    // Lot 066 (docs/tickets/tickets-tdd-lot-066-completion-colonnes-parents-enfants-export.md):
    // - 66.1: "TRAVAUX COMPLET"/"TRAVAUX DETAIL" dropped from Parents (redundant with "Tableaux", same
    //   information exploded into two columns -- decision 2). Enfants' bare "ZÉRO ENERGIE EN PRESENCE
    //   EE" PointColumnDefinition is gone too -- not because it was a duplicate (66.0 found it wasn't;
    //   DIVERS genuinely produces that exact Colonne name), but because DIVERS' own ConditionalPointRule
    //   was retargeted onto ISOLEMENT's "(PS941)"-suffixed name instead (see BuildDefaultImportProfile's
    //   DIVERS rule), merging both sheets' output onto the one PS941 PointColumnDefinition that remains.
    // - 66.2: 7 (Parents) / 11 (Enfants) unmapped identity ColumnDefinitions (Source = null), decision 6
    //   -- a legitimately empty cell reserving a slot in the target workbook's known schema, same
    //   pattern already established by GenerationPipelineIntegrationTests' own "Fluide"/"Commentaires"
    //   approximation. Positioned per the ticket's own (explicitly non-blocking) guidance from
    //   OXO_TRAME_IMPORT_MAD.xlsx's column order; "SUPPRESSION"/"ADR Email"/"COMMENTAIRES" (Parents) and
    //   "SUPPRESSION" (Enfants) were asked to sit "after PROGRESS" specifically, which the engine cannot
    //   express (ColumnDefinitions are always rendered before ApplicationColumnDefinitions, regardless
    //   of list order -- see SheetGenerationEngine.GenerateSheet) -- placed at the end of the
    //   descriptive-columns block instead, per the ticket's own fallback instruction.
    // - 66.3/66.4: the same 16 Point columns now live on both Parents and Enfants (built from this one
    //   shared definition, so the two rules can never silently drift apart) -- marked on Parents via
    //   SheetGenerationEngine's new aggregation (66.3: at least one child IsolementPivot of this
    //   Équipement carries the Point), on Enfants exactly as before (direct match). A *method*, not a
    //   shared list, and called separately for each rule below -- deliberately, not an oversight: EF
    //   Core's owned-collection change tracker cannot have the very same PointColumnDefinition object
    //   instances be owned by two different SheetGenerationRule rows at once (confirmed empirically --
    //   sharing one static list silently orphaned Parents' whole PointColumnDefinitions collection on
    //   the very first SaveChangesAsync). Same "factory method, not a shared instance" precedent as
    //   BuildTacheMultipleSheetRule below.
    private static List<PointColumnDefinition> BuildIsolementStylePointColumnDefinitions() =>
    [
        new PointColumnDefinition("PROLOCK VANNES", "PROLOCK VANNES"),
        new PointColumnDefinition("DEPROLOCK VANNES", "DEPROLOCK VANNES"),
        new PointColumnDefinition(IsolementZeroEnergieColonneName, IsolementZeroEnergieColonneName),
        new PointColumnDefinition(PoseEtiquettesColonneName, PoseEtiquettesColonneName),
        new PointColumnDefinition(
            "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"),
        new PointColumnDefinition("CONTRÔLE ETANCHÉITÉS", "CONTRÔLE ETANCHÉITÉS"),
        new PointColumnDefinition("RECEPTION DEBUT MAD", "RECEPTION DEBUT MAD"),
        new PointColumnDefinition("RÉCEPTION PLATINES/TAMPONS PLEINS", "RÉCEPTION PLATINES/TAMPONS PLEINS"),
        new PointColumnDefinition("RECEPTION DEBUT REL", "RECEPTION DEBUT REL"),
        new PointColumnDefinition("PLATINES / TAMPONS PLEINS", "PLATINES / TAMPONS PLEINS"),
        new PointColumnDefinition("SYNCHRONISATION INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
        new PointColumnDefinition("SOUPAPE : CONSTAT ENCRASSEMENT", "SOUPAPE : CONSTAT ENCRASSEMENT"),
        new PointColumnDefinition(
            "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
        new PointColumnDefinition("PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
        new PointColumnDefinition("PF : VALIDATION CONSTAT ENCRASSEMENT", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
        new PointColumnDefinition("PF : ACCORD TRAVAUX FEU", "PF : ACCORD TRAVAUX FEU")
    ];

    // The third rule (Tâches multiples, Lot T) needs no Guid of its own, unlike ImportProfileId/
    // ExportProfileId above -- SheetGenerationRule is a plain record with no identity property (see
    // its own Domain source comment), not an aggregate root. For a brand-new profile, idempotence is
    // fully covered by ExportProfileId: this method only ever runs once, the very first time no
    // profile exists under that Id. For a profile seeded before this rule existed, T8's own
    // MigrateTacheMultipleSheetRuleIfMissingAsync is the (separate, narrower) idempotence guarantee --
    // see its own comment below.
    private static ExportProfile BuildDefaultExportProfile() => new(
        ExportProfileId, ProfileName,
        [
            new SheetGenerationRule(
                "Parents",
                PivotSource.Equipement,
                [
                    new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
                    new ColumnDefinition("Type Elément", PivotFieldRef.EquipementTypeElementNom),
                    new ColumnDefinition("Zone", PivotFieldRef.EquipementLocalisation),
                    new ColumnDefinition("LOC2", null),
                    new ColumnDefinition("LOC3", null),
                    new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation),
                    new ColumnDefinition("FLUIDE", null),
                    new ColumnDefinition("RECURRENT", null),
                    new ColumnDefinition("Tableaux", PivotFieldRef.EquipementTableaux),
                    new ColumnDefinition("SUPPRESSION", null),
                    new ColumnDefinition("ADR Email", null),
                    new ColumnDefinition("COMMENTAIRES", null)
                ],
                BuildIsolementStylePointColumnDefinitions(),
                [new ApplicationColumnDefinition(ProgressApplicationName, ProgressApplicationName, "O")]),
            new SheetGenerationRule(
                "Enfants",
                PivotSource.Isolement,
                [
                    new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
                    new ColumnDefinition("Type Elément", PivotFieldRef.IsolementTypeElementNom),
                    new ColumnDefinition("Zone", PivotFieldRef.IsolementLocalisation),
                    new ColumnDefinition("LOC2", null),
                    new ColumnDefinition("LOC3", null),
                    new ColumnDefinition("ELEMENT PARENT", PivotFieldRef.IsolementRepereParent),
                    new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation),
                    new ColumnDefinition("Position à la pose", PivotFieldRef.IsolementPositionALaPose),
                    new ColumnDefinition("POSITION A LA DEPOSE", null),
                    new ColumnDefinition("PHASE PROCESS", null),
                    new ColumnDefinition("REMARQUES", null),
                    new ColumnDefinition("ETIQUETTE", null),
                    new ColumnDefinition("DIAMETRE INCH", null),
                    new ColumnDefinition("SERIE LBS", null),
                    new ColumnDefinition("NATURE JOINT", null),
                    new ColumnDefinition("BESOIN ECHAF", null),
                    new ColumnDefinition("Tableaux", PivotFieldRef.IsolementTableaux),
                    new ColumnDefinition("SUPPRESSION", null)
                ],
                BuildIsolementStylePointColumnDefinitions(),
                [new ApplicationColumnDefinition(ProgressApplicationName, ProgressApplicationName, "O")]),
            BuildTacheMultipleSheetRule()
        ]);

    // Extracted (T8) so the exact same rule definition is shared between the nominal seeding path
    // above (brand-new profile) and the migration path (MigrateTacheMultipleSheetRuleIfMissingAsync,
    // an already-seeded profile that predates this rule) -- one definition, never two copies to drift.
    //
    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md): gains
    // "Repère TM"/"TYPE ELEMENT CODE" (identity columns, same lead position as Repère/Type Elément on
    // Parents/Enfants) and "Colonne Travaux" (the legacy app's own linking column, positioned last --
    // resolved per-row from ImportProfile.TacheMultipleTypeLabels, see ImportPipelineOrchestrator).
    private static SheetGenerationRule BuildTacheMultipleSheetRule() => new(
        "Tâches multiples",
        PivotSource.TacheMultiple,
        [
            new ColumnDefinition("Repère TM", PivotFieldRef.TacheMultipleRepere),
            new ColumnDefinition("TYPE ELEMENT CODE", PivotFieldRef.TacheMultipleTypeElementNom),
            new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre),
            new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction),
            new ColumnDefinition("Acteur", PivotFieldRef.TacheMultipleActeur),
            new ColumnDefinition("Risques", PivotFieldRef.TacheMultipleRisques),
            new ColumnDefinition("Date de validation", PivotFieldRef.TacheMultipleDateValidation),
            new ColumnDefinition("Colonne Travaux", PivotFieldRef.TacheMultipleColonneTravaux)
        ],
        [],
        []);
}
