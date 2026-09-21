using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
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
    ILogger<DefaultProfileSeeder> logger) : IDefaultProfileSeeder
{
    public static readonly Guid ImportProfileId = Guid.Parse("a2d81110-6ed6-4b56-ac38-59e543c79f22");
    public static readonly Guid ExportProfileId = Guid.Parse("2d0c19f0-9183-486d-8293-26993069858b");

    public const string ProfileName = "Profil OXO standard";

    // Explicit interface implementation so a Razor component consuming IDefaultProfileSeeder never
    // needs to reference this concrete Infrastructure type just to identify the standard profiles
    // (see IDefaultProfileSeeder's own comment) -- the static fields above stay the single source of
    // truth, referenced directly by every existing test/seeder call site.
    Guid IDefaultProfileSeeder.ImportProfileId => ImportProfileId;

    Guid IDefaultProfileSeeder.ExportProfileId => ExportProfileId;

    // ISOLEMENT's "ZERO ENERGIE" Colonne carries a "(PS941)" suffix that DIVERS' does not -- these are
    // two genuinely distinct Colonne names in the real OXO referential, not a typo. See
    // spec-extraction-fichier-source-oxo.md §6/§7.
    private const string IsolementZeroEnergieColonneName = "ZÉRO ENERGIE EN PRESENCE EE (PS941)";
    private const string PoseEtiquettesColonneName = "POSE ÉTIQUETTES";
    private const string ReceptionDebutMadColonneName = "RECEPTION DEBUT MAD";
    private const string ReceptionDebutRelColonneName = "RECEPTION DEBUT REL";

    // Lot 084: optional block fields read only by point rules.
    private const string ZeroEnergieFieldName = "ZeroEnergie";
    private const string PoseeLeFieldName = "PoseeLe";
    private const string DeposeeLeFieldName = "DeposeeLe";

    // The two tables every element of a MAD dossier belongs to -- the DefaultTableaux value seeded
    // below. Since lot 082 they only fill the "Tableaux" column; they create no Point (lot U3 used to
    // turn each Tableau name into an Equipement Point).
    private const string TravauxCompletColonneName = "TRAVAUX COMPLET";
    private const string TravauxDetailColonneName = "TRAVAUX DETAIL";

    // Client feedback (2026-09-16): a Point created for every Equipement. Lot 082
    // (docs/tickets/tickets-tdd-lot-082-points-office-element-parent-procedure.md): declared as an
    // unconditional Colonne of the PROCEDURE rule -- the only source of the Equipement's own Points --
    // and no longer in DefaultTableaux, which only names the tables the element belongs to.
    private const string VisitePrealableChantierColonneName = "VISITE PRÉALABLE CHANTIER";

    // Lot 083 (docs/tickets/tickets-tdd-lot-083-points-conditionnels-procedure-taches.md): the client's
    // full list of Equipement Points, in his order (E4). PROCÉDURE MAD/REL only when at least one real
    // task of that type exists -- PROCEDURE's conditional rules on the raw TYPE column.
    private const string ProcedureMadColonneName = "PROCÉDURE MAD";
    private const string AutorisationDeplatinagesColonneName = "AUTORISATION DÉPLATINAGES";
    private const string ProcedureRelColonneName = "PROCÉDURE REL";
    private const string AutorisationRemiseEnServiceColonneName = "AUTORISATION DE REMISE EN SERVICE";
    private const string ReceptionFinaleChantierColonneName = "RÉCEPTION FINALE CHANTIER";

    private static readonly string[] EquipementPointColonneNames =
    [
        VisitePrealableChantierColonneName, ProcedureMadColonneName, AutorisationDeplatinagesColonneName,
        ProcedureRelColonneName, AutorisationRemiseEnServiceColonneName, ReceptionFinaleChantierColonneName
    ];

    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), decision #4: the only
    // Application name seeded by default. "PROGRESS" is the legacy EF6 AMProgress Application name
    // this deployment cares about today -- an admin can add more via the profile editor.
    private const string ProgressApplicationName = "PROGRESS";

    // Client feedback (2026-09-11): so a garbage/template-artifact cell value (e.g. ORIFICES
    // CAPACITES' own unfilled-cell "DATE" placeholder) is reported as a warning instead of silently
    // imported. Not a hardcoded engine assumption -- lives on
    // SheetExtractionRule.AllowedCouleursEtiquette, editable per profile, exactly so a future,
    // genuinely new color doesn't need a code change. The two sheets' sets deliberately differ --
    // confirmed by the client (2026-09-11) as a genuine business distinction, not an oversight.
    //
    // PLATINES: the client's own stated set (ROUGE/BLANC/JAUNE/VERT) plus "BLEUE", which the client's
    // list omitted but which real fixture data (D8570, 8 of 21 blocks) already confirms is a genuine,
    // already-extracted color for this sheet -- kept rather than dropped (client-confirmed, 2026-09-11)
    // so those 8 already-verified blocks don't regress into warnings.
    private static readonly string[] PlatinesAllowedCouleursEtiquette = ["ROUGE", "BLANC", "JAUNE", "VERT", "BLEUE"];

    // ORIFICES CAPACITES: the client's own stated set -- no real fixture on disk has ever shown an
    // actual color here (every block's cell holds only the "DATE" template artifact), so there's no
    // fixture-confirmed value to reconcile against, unlike PLATINES.
    private static readonly string[] OrificesCapacitesAllowedCouleursEtiquette = ["ROUGE", "BLANC"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedImportProfileAsync(cancellationToken);
        await SeedExportProfileAsync(cancellationToken);
    }

    // Client feedback (2026-09-16): a profile already seeded before a deployment that changed this
    // class's own build methods is never touched again by SeedAsync above (by design -- an admin's
    // customization to the standard profile must never be silently overwritten on restart). That
    // leaves no in-app way to pick up a genuine seed-definition change other than manually
    // reconciling the profile by hand, or dropping the database -- neither of which the client
    // wants. These two methods give the admin UI a single, explicit, destructive action per
    // profile: discard whatever is currently stored under the standard profile's stable Id and
    // rebuild it fresh from the seed. IImportProfileStore/IExportProfileStore.SaveAsync is already a
    // true upsert keyed by the profile's own Id (delete the existing owned graph, then insert the
    // new one -- see EfImportProfileStore/EfExportProfileStore's own comments), so simply saving a
    // freshly built default profile IS the "delete and recreate from the seed" action; no separate
    // DeleteAsync call is needed or correct here (DeleteAsync would leave nothing to immediately
    // re-insert as part of the same action, and would momentarily surface the profile as gone).
    public async Task ResetImportProfileToDefaultAsync(CancellationToken cancellationToken = default)
    {
        await importProfileStore.SaveAsync(BuildDefaultImportProfile(), cancellationToken);
        logger.LogInformation("Reset default import profile {ProfileId} to its seed definition", ImportProfileId);
    }

    public async Task ResetExportProfileToDefaultAsync(CancellationToken cancellationToken = default)
    {
        await exportProfileStore.SaveAsync(BuildDefaultExportProfile(), cancellationToken);
        logger.LogInformation("Reset default export profile {ProfileId} to its seed definition", ExportProfileId);
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
                new RepeatingBlockLocator("PROCEDURE", 9, 1,
                [
                    new BlockFieldDefinition(ProcedureFieldNames.Action, "C:L", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Ordre, "B", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Acteur, "M:N", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.Risques, "O:Q", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.TypeTacheMultipleAlias, "R", 0, 0),
                    new BlockFieldDefinition(ProcedureFieldNames.DateValidation, "T:U", 0, 0)
                ]),
                [
                    new ConditionalPointRule(
                        ProcedureFieldNames.TypeTacheMultipleAlias, ConditionOperator.Equals, "MAD", ProcedureMadColonneName),
                    new ConditionalPointRule(
                        ProcedureFieldNames.TypeTacheMultipleAlias, ConditionOperator.Equals, "REL", ProcedureRelColonneName)
                ],
                [
                    VisitePrealableChantierColonneName, AutorisationDeplatinagesColonneName,
                    AutorisationRemiseEnServiceColonneName, ReceptionFinaleChantierColonneName
                ],
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
            // Lot 084 (docs/tickets/tickets-tdd-lot-084-moteur-generique-feuilles-elements.md): the five
            // element sheets share one engine (ElementSheetExtractionService). Everything that used to be
            // coded per sheet is now declared here: the repère echo and the zone as header fields (G6,
            // G16), which block fields are optional (G4), and whether an element that ticks no
            // conditional Colonne is reported (G3). The settings below reproduce the output of the
            // services they replace, cell for cell, on the 14 real fixtures (FixtureOutputSnapshotTests).
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator("ISOLEMENT", 19, 7,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    // Blank on the real D8570 "V4"/"VANNE" row, which must still be extracted.
                    new BlockFieldDefinition(ElementFieldNames.Designation, "H:U", -1, 0, isRequired: false),
                    new BlockFieldDefinition(ElementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 4),
                    // G5: the dedicated "zéro énergie" cell (column V), an ordinary optional field.
                    new BlockFieldDefinition(ZeroEnergieFieldName, "V", -1, 0, isRequired: false)
                ]),
                [
                    new ConditionalPointRule(
                        ZeroEnergieFieldName, ConditionOperator.Equals, "ZERO ENERGIE", IsolementZeroEnergieColonneName)
                ],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("ISOLEMENT", "K6:T6"))],
                [],
                warnWhenNoConditionalPoint: true),
            new SheetExtractionRule(
                "PLATINES",
                new RepeatingBlockLocator("PLATINES", 17, 8,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 5),
                    // Lot 068: "couleur d'étiquette", H:N at block offset +1 (same row as the form's
                    // "ÉTIQUETTE" label in column F). Free text, filtered by the allowed list below.
                    new BlockFieldDefinition(ElementFieldNames.CouleurEtiquette, "H:N", 1, 1, isRequired: false),
                    // The two H value cells of the block (POSÉE LE +2, DÉPOSÉE LE +3, merged H:N).
                    new BlockFieldDefinition(PoseeLeFieldName, "H:N", 2, 2, isRequired: false),
                    new BlockFieldDefinition(DeposeeLeFieldName, "H:N", 3, 3, isRequired: false)
                ]),
                // Client clarification (2026-09-16), the 4 PLATINES reception Colonnes are the DEB/FIN x
                // MAD/REL variants: "RÉCEPTION PLATINES/TAMPONS PLEINS" (FIN MAD) and "PLATINES / TAMPONS
                // PLEINS" (FIN REL) stay unconditional; "RECEPTION DEBUT MAD"/"RECEPTION DEBUT REL" are
                // ticked when either H value cell holds "DEBUT MAD"/"DEBUT REL" -- the row label doesn't
                // matter (real fixtures: DEBUT MAD in both rows, DEBUT REL only in POSÉE LE). A FIN value
                // ticks nothing, and is not a warning (G7).
                [
                    new ConditionalPointRule(PoseeLeFieldName, ConditionOperator.Equals, "DEBUT MAD", ReceptionDebutMadColonneName),
                    new ConditionalPointRule(DeposeeLeFieldName, ConditionOperator.Equals, "DEBUT MAD", ReceptionDebutMadColonneName),
                    new ConditionalPointRule(PoseeLeFieldName, ConditionOperator.Equals, "DEBUT REL", ReceptionDebutRelColonneName),
                    new ConditionalPointRule(DeposeeLeFieldName, ConditionOperator.Equals, "DEBUT REL", ReceptionDebutRelColonneName)
                ],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS",
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "PLATINES / TAMPONS PLEINS"
                ],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("PLATINES", "K6:U6"))],
                [],
                allowedCouleursEtiquette: PlatinesAllowedCouleursEtiquette),
            new SheetExtractionRule(
                "ORIFICES CAPACITES",
                new RepeatingBlockLocator("ORIFICES CAPACITES", 17, 8,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "H:V", -1, 0),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 5),
                    // Same "couleur d'étiquette" cell as PLATINES (client screenshot, 2026-09-11). The
                    // allowed list turns the unfilled template's "DATE" into a non-blocking warning.
                    new BlockFieldDefinition(ElementFieldNames.CouleurEtiquette, "H:N", 1, 1, isRequired: false)
                ]),
                [],
                [
                    PoseEtiquettesColonneName,
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                    "CONTRÔLE ETANCHÉITÉS"
                ],
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("ORIFICES CAPACITES", "K6:U6"))],
                [],
                allowedCouleursEtiquette: OrificesCapacitesAllowedCouleursEtiquette),
            new SheetExtractionRule(
                "AUTRES JOINTS TOUCHES",
                new RepeatingBlockLocator("AUTRES JOINTS TOUCHES", 17, 7,
                [
                    new BlockFieldDefinition(ElementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "F:Y", -1, 0),
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.NotEquals, "TUBING", PoseEtiquettesColonneName)
                ],
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"],
                // The repère echo lives at N6 on this sheet (K6:U6, stated by the spec, is blank).
                [new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("AUTRES JOINTS TOUCHES", "N6"))],
                [],
                // Client feedback (2026-09): no per-block cell -- every element is "BLEUE".
                defaultCouleurEtiquette: "BLEUE",
                warnWhenNoConditionalPoint: true),
            new SheetExtractionRule(
                "DIVERS",
                new RepeatingBlockLocator("DIVERS", 9, 3,
                [
                    new BlockFieldDefinition(ElementFieldNames.TypeElement, "B:G", 0, 2),
                    new BlockFieldDefinition(ElementFieldNames.Identification, "H:K", 0, 2),
                    new BlockFieldDefinition(ElementFieldNames.Designation, "L:V", 0, 2)
                ]),
                [
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    // Lot 066 (66.1): retargeted onto ISOLEMENT's own "ZERO ENERGIE" Colonne name (client
                    // decision, "fusionner les deux colonnes"), so one export column covers both sheets.
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", IsolementZeroEnergieColonneName),
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "SOUPAPE",
                        "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT DE FEU",
                        "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT DE FEU",
                        "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new ConditionalPointRule(
                        ElementFieldNames.TypeElement, ConditionOperator.Equals, "POINT DE FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                [],
                [
                    // Same N6 discrepancy as AUTRES JOINTS TOUCHES.
                    new HeaderFieldRule(SharedHeaderFieldNames.RepereEcho, new DirectCell("DIVERS", "N6")),
                    // "loc1": the zone broadcast on the equipment and every element of the run (G16).
                    new HeaderFieldRule(ElementFieldNames.ZoneHeader, new DirectCell("DIVERS", "B6:E6"))
                ],
                [],
                warnWhenNoConditionalPoint: true)
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
                    // Lot 070: source Excel tab name, as configured on the SheetExtractionRule that
                    // produced this Equipement (ProcedureExtractionService always, in practice).
                    new ColumnDefinition("Feuille", PivotFieldRef.EquipementSourceSheet),
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
                // Lot 082 (D6): the Equipement's own Points first, then the ones aggregated from its
                // children (66.4).
                [
                    .. EquipementPointColonneNames.Select(name => new PointColumnDefinition(name, name)),
                    .. BuildIsolementStylePointColumnDefinitions()
                ],
                [new ApplicationColumnDefinition(ProgressApplicationName, ProgressApplicationName, "O")]),
            new SheetGenerationRule(
                "Enfants",
                PivotSource.Isolement,
                [
                    new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
                    // Lot 070: source Excel tab name, as configured on the SheetExtractionRule that
                    // produced this Isolement (ISOLEMENT/PLATINES/ORIFICES CAPACITES/AUTRES JOINTS
                    // TOUCHES/DIVERS -- varies row by row).
                    new ColumnDefinition("Feuille", PivotFieldRef.IsolementSourceSheet),
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
                    // Lot 068: was Source: null (Lot 066, "unmapped identity column") -- now mapped to
                    // PLATINES' "couleur d'étiquette" cell (only sheet that populates it; every other
                    // isolement-style row leaves this "").
                    new ColumnDefinition("ETIQUETTE", PivotFieldRef.IsolementCouleurEtiquette),
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
    //
    // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
    // completes the sheet toward the client's real target trame (OXO.TRAME.IMPORT.MAD.xlsx). Order
    // follows the reference trame's own groupings, without reordering the 8 columns already above --
    // "GUID" (unmapped, no correspondence)/"TYPE TACHE" (the sheet's own code)/"ZONE" (Equipement's
    // zone, broadcast) lead alongside "Repère TM"; "LOC2"/"LOC3"/"LOT"/"Ressource" are unmapped, same
    // treatment as their Parents/Enfants counterparts (always blank for now); "Ligne" is the real source
    // row number; "SUPPRESSION" is constant per client instruction. "Type"/"AVANCEMENT POINT"/
    // "SIGNATURE"/"DERNIERE MODIF"/"UTILISATEUR" are deliberately not reported at all (client: "on
    // ignore"/"on ne reporte pas cette colonne").
    //
    // Follow-up (2026-09-07): "CRITERE" is no longer a constant -- confirmed with the client that a
    // factice/section-header row (no Ordre) must read "Pour info" rather than "A faire". Deliberately
    // NOT made profile-configurable (a client-specific one-off, not a general mechanism) -- the two
    // literal values are hardcoded directly in PivotFieldResolver, mirroring how PROCEDURE's own
    // unconditional Points ("TRAVAUX COMPLET"/"TRAVAUX DETAIL") are hardcoded in
    // ProcedureExtractionService rather than made configurable. "AVANCEMENT" is removed entirely from
    // the default profile per the same client confirmation.
    private static SheetGenerationRule BuildTacheMultipleSheetRule() => new(
        "Tâches multiples",
        PivotSource.TacheMultiple,
        [
            new ColumnDefinition("GUID", null),
            new ColumnDefinition("TYPE TACHE", PivotFieldRef.TacheMultipleTypeTacheMultipleCode),
            new ColumnDefinition("Repère TM", PivotFieldRef.TacheMultipleRepere),
            new ColumnDefinition("ZONE", PivotFieldRef.TacheMultipleLocalisation),
            new ColumnDefinition("LOC2", null),
            new ColumnDefinition("LOC3", null),
            new ColumnDefinition("TYPE ELEMENT CODE", PivotFieldRef.TacheMultipleTypeElementNom),
            new ColumnDefinition("LOT", null),
            new ColumnDefinition("Ressource", null),
            new ColumnDefinition("Ligne", PivotFieldRef.TacheMultipleLigneSource),
            new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre),
            new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction),
            new ColumnDefinition("Acteur", PivotFieldRef.TacheMultipleActeur),
            new ColumnDefinition("Risques", PivotFieldRef.TacheMultipleRisques),
            new ColumnDefinition("Date de validation", PivotFieldRef.TacheMultipleDateValidation),
            new ColumnDefinition("Colonne Travaux", PivotFieldRef.TacheMultipleColonneTravaux),
            new ColumnDefinition("CRITERE", PivotFieldRef.TacheMultipleCritere)
        ],
        [],
        [],
        [
            new ConstantColumnDefinition("SUPPRESSION", "N")
        ]);
}
