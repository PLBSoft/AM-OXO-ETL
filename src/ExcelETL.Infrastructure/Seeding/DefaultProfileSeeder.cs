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

    // PROCEDURE's 2 Points are hardcoded in ProcedureExtractionService itself (private consts,
    // unconditional, independent of any profile) -- transcribed here only so the default export
    // profile can reference their exact Colonne names, not because an import SheetExtractionRule
    // configures them.
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
                []),
            new SheetExtractionRule(
                "ISOLEMENT",
                new RepeatingBlockLocator("ISOLEMENT", 19, 7, IsolementFieldNames.Identification,
                [
                    new BlockFieldDefinition(IsolementFieldNames.Identification, "B:E", 0, 1),
                    new BlockFieldDefinition(IsolementFieldNames.Designation, "H:U", -1, 0),
                    new BlockFieldDefinition(IsolementFieldNames.PositionALaPose, "H:O", 1, 2),
                    new BlockFieldDefinition(IsolementFieldNames.TypeElement, "B:E", 3, 4)
                ]),
                [
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", IsolementZeroEnergieColonneName)
                ],
                ["PROLOCK VANNES", "DEPROLOCK VANNES"]),
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
                    "RECEPTION DEBUT MAD",
                    "RÉCEPTION PLATINES/TAMPONS PLEINS",
                    "RECEPTION DEBUT REL",
                    "PLATINES / TAMPONS PLEINS"
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
                ]),
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
                ["RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS", "CONTRÔLE ETANCHÉITÉS"]),
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
                    new ConditionalPointRule(
                        IsolementFieldNames.TypeElement, ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE"),
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
                [])
        ]);

    // Minimal by design (ticket's "Décision actée" §3, client-confirmed): only the descriptive fields
    // BuildDefaultImportProfile's pivot already produces, plus every Point Colonne that profile
    // actually produces -- no Source = null placeholder columns anticipating an extraction rule that
    // doesn't exist yet. Point column Headers reuse the raw Colonne name verbatim: no localized-label
    // catalogue exists for these yet, and inventing one wasn't asked for by this ticket.
    //
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
                    new ColumnDefinition("Désignation", PivotFieldRef.EquipementDesignation)
                ],
                [
                    new PointColumnDefinition(TravauxCompletColonneName, TravauxCompletColonneName),
                    new PointColumnDefinition(TravauxDetailColonneName, TravauxDetailColonneName)
                ],
                []),
            new SheetGenerationRule(
                "Enfants",
                PivotSource.Isolement,
                [
                    new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere),
                    new ColumnDefinition("Type", PivotFieldRef.IsolementTypeElementNom),
                    new ColumnDefinition("Zone", PivotFieldRef.IsolementLocalisation),
                    new ColumnDefinition("Désignation", PivotFieldRef.IsolementDesignation),
                    new ColumnDefinition("Position à la pose", PivotFieldRef.IsolementPositionALaPose)
                ],
                [
                    new PointColumnDefinition("PROLOCK VANNES", "PROLOCK VANNES"),
                    new PointColumnDefinition("DEPROLOCK VANNES", "DEPROLOCK VANNES"),
                    new PointColumnDefinition(IsolementZeroEnergieColonneName, IsolementZeroEnergieColonneName),
                    new PointColumnDefinition(PoseEtiquettesColonneName, PoseEtiquettesColonneName),
                    new PointColumnDefinition(
                        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS",
                        "RÉCEPTIONS ASSEMBLAGES : BOULONNÉS (PS938) OU TUBINGS"),
                    new PointColumnDefinition("CONTRÔLE ETANCHÉITÉS", "CONTRÔLE ETANCHÉITÉS"),
                    new PointColumnDefinition("RECEPTION DEBUT MAD", "RECEPTION DEBUT MAD"),
                    new PointColumnDefinition("RÉCEPTION PLATINES/TAMPONS PLEINS", "RÉCEPTION PLATINES/TAMPONS PLEINS"),
                    new PointColumnDefinition("RECEPTION DEBUT REL", "RECEPTION DEBUT REL"),
                    new PointColumnDefinition("PLATINES / TAMPONS PLEINS", "PLATINES / TAMPONS PLEINS"),
                    new PointColumnDefinition("SYNCHRONISATION INSTRUMENTATION", "SYNCHRONISATION INSTRUMENTATION"),
                    new PointColumnDefinition("ZÉRO ENERGIE EN PRESENCE EE", "ZÉRO ENERGIE EN PRESENCE EE"),
                    new PointColumnDefinition("SOUPAPE : CONSTAT ENCRASSEMENT", "SOUPAPE : CONSTAT ENCRASSEMENT"),
                    new PointColumnDefinition(
                        "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS", "SOUPAPE : RÉCEPTION REPOSE AVEC ABSENCE BOUCHONS"),
                    new PointColumnDefinition(
                        "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES", "PF : SIGNATURE ÉTIQUETTE ET ACCORD COUPES"),
                    new PointColumnDefinition("PF : VALIDATION CONSTAT ENCRASSEMENT", "PF : VALIDATION CONSTAT ENCRASSEMENT"),
                    new PointColumnDefinition("PF : ACCORD TRAVAUX FEU", "PF : ACCORD TRAVAUX FEU")
                ],
                []),
            BuildTacheMultipleSheetRule()
        ]);

    // Extracted (T8) so the exact same rule definition is shared between the nominal seeding path
    // above (brand-new profile) and the migration path (MigrateTacheMultipleSheetRuleIfMissingAsync,
    // an already-seeded profile that predates this rule) -- one definition, never two copies to drift.
    private static SheetGenerationRule BuildTacheMultipleSheetRule() => new(
        "Tâches multiples",
        PivotSource.TacheMultiple,
        [
            new ColumnDefinition("Ordre", PivotFieldRef.TacheMultipleOrdre),
            new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction),
            new ColumnDefinition("Acteur", PivotFieldRef.TacheMultipleActeur),
            new ColumnDefinition("Risques", PivotFieldRef.TacheMultipleRisques),
            new ColumnDefinition("Date de validation", PivotFieldRef.TacheMultipleDateValidation)
        ],
        [],
        []);
}
