using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Extraction.Oxo.Elements;
using ExcelETL.Application.Extraction.Oxo.Procedure;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence.Repositories;
using ExcelETL.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExcelETL.Infrastructure.Tests.Seeding;

// Lot "seed des profils d'import/export par défaut" (docs/tickets-tdd-seed-profils-defaut.md), M1.
// Real EF Core InMemory provider throughout (real EfImportProfileStore/EfExportProfileStore), never
// mocked -- same convention as EfImportProfileStoreTests/EfExportProfileStoreTests.
public class DefaultProfileSeederTests
{
    private readonly IDbContextFactory<ExcelETL.Infrastructure.Persistence.ExcelEtlDbContext> _dbContextFactory =
        new TestExcelEtlDbContextFactory("DefaultProfileSeederTests_" + Guid.NewGuid());

    private DefaultProfileSeeder CreateSeeder(
        out IImportProfileStore importProfileStore, out IExportProfileStore exportProfileStore)
    {
        importProfileStore = new EfImportProfileStore(_dbContextFactory);
        exportProfileStore = new EfExportProfileStore(_dbContextFactory);
        return new DefaultProfileSeeder(importProfileStore, exportProfileStore, NullLogger<DefaultProfileSeeder>.Instance);
    }

    [Fact]
    public async Task SeedAsync_OnEmptyDatabase_CreatesBothDefaultProfilesUnderTheirStableIds()
    {
        var seeder = CreateSeeder(out var importProfileStore, out var exportProfileStore);

        await seeder.SeedAsync();

        var importProfile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        importProfile.Should().NotBeNull();
        importProfile!.Name.Should().Be("Profil OXO standard");
        exportProfile.Should().NotBeNull();
        exportProfile!.Name.Should().Be("Profil OXO standard");
    }

    [Fact]
    public async Task SeedAsync_CalledTwiceInARow_DoesNotCreateDuplicates()
    {
        var seeder = CreateSeeder(out var importProfileStore, out var exportProfileStore);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        (await importProfileStore.GetAllAsync()).Should().ContainSingle();
        (await exportProfileStore.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task SeedAsync_WhenImportProfileAlreadyModifiedByAnAdmin_DoesNotOverwriteIt()
    {
        var seeder = CreateSeeder(out var importProfileStore, out _);
        await seeder.SeedAsync();

        var seeded = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        var renamed = new ImportProfile(
            seeded!.Id, "Renamed by an admin", seeded.ReperePrefix, seeded.EquipementTypeElementNom,
            seeded.DefaultTableaux, seeded.DefaultApplicationNames, seeded.SheetRules);
        await importProfileStore.SaveAsync(renamed);

        await seeder.SeedAsync();

        var reloaded = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        reloaded!.Name.Should().Be("Renamed by an admin");
    }

    [Fact]
    public async Task SeedAsync_WhenExportProfileAlreadyModifiedByAnAdmin_DoesNotOverwriteIt()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync();

        var seeded = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        var renamed = new ExportProfile(seeded!.Id, "Renamed by an admin", seeded.SheetRules);
        await exportProfileStore.SaveAsync(renamed);

        await seeder.SeedAsync();

        var reloaded = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        reloaded!.Name.Should().Be("Renamed by an admin");
    }

    [Fact]
    public async Task SeedAsync_CreatesImportProfile_WithExpectedRootFieldsAndAllSixSheetRules()
    {
        var seeder = CreateSeeder(out var importProfileStore, out _);
        await seeder.SeedAsync();

        var profile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);

        profile!.ReperePrefix.Should().Be(ImportProfile.DefaultReperePrefix);
        profile.EquipementTypeElementNom.Should().Be("MAD TRAVAUX");
        profile.SheetRules.Select(r => r.SheetName).Should().Equal(
            "PROCEDURE", "ISOLEMENT", "PLATINES", "ORIFICES CAPACITES", "AUTRES JOINTS TOUCHES", "DIVERS");

        // Lot 082: "Tableaux" only names the tables -- the Equipement's Points are PROCEDURE's own
        // unconditional Colonnes.
        profile.DefaultTableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        var procedure = profile.SheetRules.Single(r => r.SheetName == "PROCEDURE");
        // Lot 083: the client's 6 Equipement Points -- 4 unconditional, 2 when at least one task of the type
        // exists.
        procedure.UnconditionalColonneNames.Should().Equal(
            "VISITE PRÉALABLE CHANTIER", "AUTORISATION DÉPLATINAGES", "AUTORISATION DE REMISE EN SERVICE",
            "RÉCEPTION FINALE CHANTIER");
        procedure.PointRules.Should().Equal(
            new ConditionalPointRule(ProcedureFieldNames.TypeTacheMultipleAlias, ConditionOperator.Equals, "MAD", "PROCÉDURE MAD"),
            new ConditionalPointRule(ProcedureFieldNames.TypeTacheMultipleAlias, ConditionOperator.Equals, "REL", "PROCÉDURE REL"));

        // Lot 084.6: the five element sheets share one engine; the seed reproduces the output of the
        // services it replaced (FixtureOutputSnapshotTests).
        var isolement = profile.SheetRules.Single(r => r.SheetName == "ISOLEMENT");
        isolement.Locator.FirstBlockStartRow.Should().Be(19);
        isolement.Locator.Step.Should().Be(7);
        isolement.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES", "DEPROLOCK VANNES");
        // G5: zéro énergie is an ordinary optional block field (column V) read by an ordinary rule.
        isolement.PointRules.Should().Equal(
            new ConditionalPointRule("ZeroEnergie", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE EN PRESENCE EE (PS941)"));
        isolement.Locator.Fields.Single(f => f.Name == "ZeroEnergie").Should().Be(new BlockFieldDefinition("ZeroEnergie", "V", -1, 0, isRequired: false));
        // Blank on the real D8570 "V4"/"VANNE" row, which must still be extracted.
        isolement.Locator.Fields.Single(f => f.Name == ElementFieldNames.Designation).IsRequired.Should().BeFalse();
        isolement.Locator.Fields.Where(f => f.Name is not ("ZeroEnergie" or ElementFieldNames.Designation))
            .Should().OnlyContain(f => f.IsRequired);
        isolement.WarnWhenNoConditionalPoint.Should().BeTrue();

        var platines = profile.SheetRules.Single(r => r.SheetName == "PLATINES");
        platines.Locator.Step.Should().Be(8);
        platines.UnconditionalColonneNames.Should().HaveCount(5);
        platines.UnconditionalColonneNames.Should().NotContain("RECEPTION DEBUT MAD");
        platines.UnconditionalColonneNames.Should().NotContain("RECEPTION DEBUT REL");
        // Client clarification (2026-09-16): a DEBUT Colonne is ticked when either H value cell of the
        // block (POSÉE LE +2, DÉPOSÉE LE +3) holds the exact text -- two optional block fields, four rules.
        platines.Locator.Fields.Where(f => !f.IsRequired).Should().BeEquivalentTo(new[]
        {
            new BlockFieldDefinition(ElementFieldNames.CouleurEtiquette, "H:N", 1, 1, isRequired: false),
            new BlockFieldDefinition("PoseeLe", "H:N", 2, 2, isRequired: false),
            new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3, isRequired: false)
        });
        platines.PointRules.Should().Equal(
            new ConditionalPointRule("PoseeLe", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD"),
            new ConditionalPointRule("DeposeeLe", ConditionOperator.Equals, "DEBUT MAD", "RECEPTION DEBUT MAD"),
            new ConditionalPointRule("PoseeLe", ConditionOperator.Equals, "DEBUT REL", "RECEPTION DEBUT REL"),
            new ConditionalPointRule("DeposeeLe", ConditionOperator.Equals, "DEBUT REL", "RECEPTION DEBUT REL"));
        // G7: a FIN value is legitimate data, never a warning.
        platines.WarnWhenNoConditionalPoint.Should().BeFalse();
        platines.DefaultCouleurEtiquette.Should().BeNull();
        // "BLEUE" is kept on top of the client's own set -- a genuine color on 8 of D8570's 21 blocks.
        platines.AllowedCouleursEtiquette.Should().BeEquivalentTo(["ROUGE", "BLANC", "JAUNE", "VERT", "BLEUE"]);

        var orificesCapacites = profile.SheetRules.Single(r => r.SheetName == "ORIFICES CAPACITES");
        orificesCapacites.Locator.Step.Should().Be(8);
        orificesCapacites.UnconditionalColonneNames.Should().HaveCount(4);
        // Same "couleur d'étiquette" cell as PLATINES.
        orificesCapacites.Locator.Fields.Single(f => f.Name == ElementFieldNames.CouleurEtiquette)
            .Should().Be(new BlockFieldDefinition(ElementFieldNames.CouleurEtiquette, "H:N", 1, 1, isRequired: false));
        orificesCapacites.PointRules.Should().BeEmpty();
        orificesCapacites.WarnWhenNoConditionalPoint.Should().BeFalse();
        orificesCapacites.DefaultCouleurEtiquette.Should().BeNull();
        orificesCapacites.AllowedCouleursEtiquette.Should().BeEquivalentTo(["ROUGE", "BLANC"]);

        var autresJointsTouches = profile.SheetRules.Single(r => r.SheetName == "AUTRES JOINTS TOUCHES");
        autresJointsTouches.Locator.Step.Should().Be(7);
        autresJointsTouches.PointRules.Should().ContainSingle();
        autresJointsTouches.PointRules.Single().ColonneName.Should().Be("POSE ÉTIQUETTES");
        autresJointsTouches.PointRules.Single().ComparisonValue.Should().Be("TUBING");
        autresJointsTouches.WarnWhenNoConditionalPoint.Should().BeTrue();
        // Client feedback (2026-09): no per-block cell -- every isolement it produces is "BLEUE".
        autresJointsTouches.Locator.Fields.Should().NotContain(f => f.Name == ElementFieldNames.CouleurEtiquette);
        autresJointsTouches.DefaultCouleurEtiquette.Should().Be("BLEUE");
        // No allowlist needed -- DefaultCouleurEtiquette is admin-typed config, not read from a cell.
        autresJointsTouches.AllowedCouleursEtiquette.Should().BeNull();

        var divers = profile.SheetRules.Single(r => r.SheetName == "DIVERS");
        divers.Locator.FirstBlockStartRow.Should().Be(9);
        divers.Locator.Step.Should().Be(3);
        divers.UnconditionalColonneNames.Should().BeEmpty();
        divers.PointRules.Should().HaveCount(7);
        divers.WarnWhenNoConditionalPoint.Should().BeTrue();
        divers.PointRules.Select(r => r.ColonneName).Should().Contain("PF : ACCORD TRAVAUX FEU");
        divers.PointRules.Select(r => r.ColonneName).Should().NotContain(name => name.Contains("POINT DE FEU"));

        // Lot 066 (docs/tickets/tickets-tdd-lot-066-completion-colonnes-parents-enfants-export.md), 66.1:
        // DIVERS' "ZERO ENERGIE" rule is retargeted onto ISOLEMENT's own "(PS941)"-suffixed Colonne name
        // (client decision, "fusionner les deux colonnes") -- both sheets now converge onto the same
        // real Colonne, so a single export PointColumnDefinition covers both.
        divers.PointRules.Select(r => r.ColonneName).Should().Contain("ZÉRO ENERGIE EN PRESENCE EE (PS941)");
        divers.PointRules.Select(r => r.ColonneName).Should().NotContain("ZÉRO ENERGIE EN PRESENCE EE");
    }

    [Fact]
    public async Task SeedAsync_CreatesImportProfile_WithExpectedHeaderRules_TranscribedFromThePreviousHardcode()
    {
        // Lot 047 (docs/tickets/tickets-tdd-lot-047-extraction-entetes-profile-driven-directcell.md),
        // 47.4: PROCEDURE's nomMAD/revision/dateRev + Designation composite, and AJT/DIVERS'
        // repereEcho -- transcribed verbatim from what was previously hardcoded in
        // ProcedureExtractionService/AutresJointsTouchesExtractionService/DiversExtractionService.
        var seeder = CreateSeeder(out var importProfileStore, out _);
        await seeder.SeedAsync();

        var profile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);

        var procedure = profile!.SheetRules.Single(r => r.SheetName == "PROCEDURE");
        procedure.HeaderFields.Should().HaveCount(3);

        var nomMad = procedure.HeaderFields.Single(f => f.Name == "nomMAD");
        nomMad.Cell.Sheet.Should().Be("PROCEDURE");
        nomMad.Cell.Range.Should().Be("M2:O2");
        nomMad.StripReperePrefix.Should().BeTrue();
        nomMad.DateFormat.Should().BeNull();

        var revision = procedure.HeaderFields.Single(f => f.Name == "revision");
        revision.Cell.Range.Should().Be("P2:Q2");
        revision.StripReperePrefix.Should().BeFalse();
        revision.DateFormat.Should().BeNull();

        var dateRev = procedure.HeaderFields.Single(f => f.Name == "dateRev");
        dateRev.Cell.Range.Should().Be("R2:T2");
        dateRev.StripReperePrefix.Should().BeFalse();
        dateRev.DateFormat.Should().Be("dd/MM/yyyy");

        procedure.HeaderComposites.Should().ContainSingle();
        var designation = procedure.HeaderComposites.Single();
        designation.Name.Should().Be("Designation");
        designation.Template.Should().Be("Rév {revision} du {dateRev}");

        var autresJointsTouches = profile.SheetRules.Single(r => r.SheetName == "AUTRES JOINTS TOUCHES");
        autresJointsTouches.HeaderFields.Should().ContainSingle();
        var ajtRepereEcho = autresJointsTouches.HeaderFields.Single();
        ajtRepereEcho.Name.Should().Be("repereEcho");
        ajtRepereEcho.Cell.Sheet.Should().Be("AUTRES JOINTS TOUCHES");
        ajtRepereEcho.Cell.Range.Should().Be("N6");
        autresJointsTouches.HeaderComposites.Should().BeEmpty();

        // Lot 084.6 (G6, G16): every element sheet declares its repère echo; DIVERS also its zone.
        var divers = profile.SheetRules.Single(r => r.SheetName == "DIVERS");
        divers.HeaderFields.Select(f => (f.Name, f.Cell.Sheet, f.Cell.Range)).Should().Equal(
            ("repereEcho", "DIVERS", "N6"), ("zone", "DIVERS", "B6:E6"));
        divers.HeaderComposites.Should().BeEmpty();

        foreach (var (sheetName, range) in new[] { ("ISOLEMENT", "K6:T6"), ("PLATINES", "K6:U6"), ("ORIFICES CAPACITES", "K6:U6") })
        {
            var rule = profile.SheetRules.Single(r => r.SheetName == sheetName);
            rule.HeaderFields.Select(f => (f.Name, f.Cell.Sheet, f.Cell.Range)).Should().Equal(("repereEcho", sheetName, range));
            rule.HeaderComposites.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SeedAsync_CreatesExportProfile_WithExpectedSheetsAndMinimalColumnSet()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync();

        var profile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        profile!.SheetRules.Select(r => r.SheetName).Should().Equal("Parents", "Enfants", "Tâches multiples");

        var parents = profile.SheetRules.Single(r => r.SheetName == "Parents");
        parents.PivotSource.Should().Be(PivotSource.Equipement);
        parents.ColumnDefinitions.Where(c => c.Source != null).Select(c => c.Source).Should().BeEquivalentTo(
        [
            PivotFieldRef.EquipementRepere,
            PivotFieldRef.EquipementSourceSheet,
            PivotFieldRef.EquipementTypeElementNom,
            PivotFieldRef.EquipementLocalisation,
            PivotFieldRef.EquipementDesignation,
            PivotFieldRef.EquipementTableaux
        ]);
        // Lot 070 (docs/tickets/tickets-tdd-lot-070-colonne-feuille-source-parents-enfants.md):
        // "Feuille" sits at index 1 -- column B, right after "Repère" at index 0/column A.
        parents.ColumnDefinitions.ElementAt(1).Should().BeEquivalentTo(
            new { Header = "Feuille", Source = PivotFieldRef.EquipementSourceSheet });
        // Lot 066, 66.1: "TRAVAUX COMPLET"/"TRAVAUX DETAIL" are gone (redundant with "Tableaux") --
        // 66.2/66.4 additions covered by their own dedicated tests below.
        parents.PointColumnDefinitions.Select(p => p.ColonneNom).Should().NotContain(["TRAVAUX COMPLET", "TRAVAUX DETAIL"]);
        parents.ApplicationColumnDefinitions.Should().ContainSingle(a => a.ApplicationNom == "PROGRESS" && a.MarkValue == "O");

        var enfants = profile.SheetRules.Single(r => r.SheetName == "Enfants");
        enfants.PivotSource.Should().Be(PivotSource.Isolement);
        enfants.ColumnDefinitions.Where(c => c.Source != null).Select(c => c.Source).Should().BeEquivalentTo(
        [
            PivotFieldRef.IsolementRepere,
            PivotFieldRef.IsolementSourceSheet,
            PivotFieldRef.IsolementTypeElementNom,
            PivotFieldRef.IsolementLocalisation,
            PivotFieldRef.IsolementRepereParent,
            PivotFieldRef.IsolementDesignation,
            PivotFieldRef.IsolementPositionALaPose,
            // Lot 068: "ETIQUETTE" moved from unmapped to IsolementCouleurEtiquette.
            PivotFieldRef.IsolementCouleurEtiquette,
            PivotFieldRef.IsolementTableaux
        ]);
        // Lot 070: "Feuille" sits at index 1 -- column B, right after "Numéro" at index 0/column A.
        enfants.ColumnDefinitions.ElementAt(1).Should().BeEquivalentTo(
            new { Header = "Feuille", Source = PivotFieldRef.IsolementSourceSheet });
        enfants.ColumnDefinitions.Should().ContainSingle(c => c.Header == "Type Elément" && c.Source == PivotFieldRef.IsolementTypeElementNom);
        enfants.ColumnDefinitions.Should().ContainSingle(c => c.Header == "ELEMENT PARENT" && c.Source == PivotFieldRef.IsolementRepereParent);
        // Lot 066, 66.1: the bare "ZÉRO ENERGIE EN PRESENCE EE" PointColumnDefinition is gone -- DIVERS
        // now targets the same "(PS941)" Colonne name as ISOLEMENT, so 17 collapses to 16.
        enfants.PointColumnDefinitions.Should().HaveCount(16);
        enfants.PointColumnDefinitions.Select(p => p.ColonneNom).Should().NotContain("ZÉRO ENERGIE EN PRESENCE EE");
        enfants.PointColumnDefinitions.Select(p => p.ColonneNom).Should().Contain("ZÉRO ENERGIE EN PRESENCE EE (PS941)");
        enfants.ApplicationColumnDefinitions.Should().ContainSingle(a => a.ApplicationNom == "PROGRESS" && a.MarkValue == "O");

        var tachesMultiples = profile.SheetRules.Single(r => r.SheetName == "Tâches multiples");
        tachesMultiples.PivotSource.Should().Be(PivotSource.TacheMultiple);
        tachesMultiples.PointColumnDefinitions.Should().BeEmpty();
        tachesMultiples.ApplicationColumnDefinitions.Should().BeEmpty();
        // Lot 069 (docs/tickets/tickets-tdd-lot-069-completion-colonnes-taches-multiples-export.md):
        // 8 columns became 16, completing toward the client's real target trame. Order among owned
        // ColumnDefinitions isn't guaranteed by the EF Core InMemory provider (same caveat as the
        // top-level SheetRules ordering noted elsewhere in this file), so this asserts the exact
        // Header<->Source correspondence and count, not sequence.
        //
        // Follow-up (2026-09-07): "CRITERE" moved from a constant to a real ColumnDefinition sourced
        // from PivotFieldRef.TacheMultipleCritere (conditional on Ordre presence -- see
        // PivotFieldResolver); "AVANCEMENT" was removed from the default profile entirely.
        tachesMultiples.ColumnDefinitions.Should().HaveCount(17);
        tachesMultiples.ColumnDefinitions.Select(c => (c.Header, c.Source)).Should().BeEquivalentTo(new (string Header, PivotFieldRef? Source)[]
        {
            ("GUID", null),
            ("TYPE TACHE", PivotFieldRef.TacheMultipleTypeTacheMultipleCode),
            ("Repère TM", PivotFieldRef.TacheMultipleRepere),
            ("ZONE", PivotFieldRef.TacheMultipleLocalisation),
            ("LOC2", null),
            ("LOC3", null),
            ("TYPE ELEMENT CODE", PivotFieldRef.TacheMultipleTypeElementNom),
            ("LOT", null),
            ("Ressource", null),
            ("Ligne", PivotFieldRef.TacheMultipleLigneSource),
            ("Ordre", PivotFieldRef.TacheMultipleOrdre),
            ("Action", PivotFieldRef.TacheMultipleAction),
            ("Acteur", PivotFieldRef.TacheMultipleActeur),
            ("Risques", PivotFieldRef.TacheMultipleRisques),
            ("Date de validation", PivotFieldRef.TacheMultipleDateValidation),
            ("Colonne Travaux", PivotFieldRef.TacheMultipleColonneTravaux),
            ("CRITERE", PivotFieldRef.TacheMultipleCritere)
        });
        tachesMultiples.ConstantColumnDefinitions.Should().ContainSingle();
        tachesMultiples.ConstantColumnDefinitions.Should().Contain(c => c.Header == "SUPPRESSION" && c.Value == "N");
    }

    // Lot 066, 66.2: unmapped identity ColumnDefinitions (decision 6) -- Source = null, a legitimately
    // empty cell reserving a slot in the target workbook's known schema.
    [Fact]
    public async Task SeedAsync_CreatesExportProfile_WithExpectedUnmappedIdentityColumns()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync();

        var profile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        var parents = profile!.SheetRules.Single(r => r.SheetName == "Parents");
        string[] expectedParentsUnmappedHeaders = ["LOC2", "LOC3", "FLUIDE", "RECURRENT", "SUPPRESSION", "ADR Email", "COMMENTAIRES"];
        parents.ColumnDefinitions.Where(c => expectedParentsUnmappedHeaders.Contains(c.Header))
            .Should().HaveCount(expectedParentsUnmappedHeaders.Length).And.OnlyContain(c => c.Source == null);

        var enfants = profile.SheetRules.Single(r => r.SheetName == "Enfants");
        // "ETIQUETTE" removed from this list at Lot 068 -- now mapped, see the dedicated test below.
        string[] expectedEnfantsUnmappedHeaders =
        [
            "LOC2", "LOC3", "PHASE PROCESS", "REMARQUES", "DIAMETRE INCH", "SERIE LBS",
            "NATURE JOINT", "BESOIN ECHAF", "SUPPRESSION", "POSITION A LA DEPOSE"
        ];
        enfants.ColumnDefinitions.Where(c => expectedEnfantsUnmappedHeaders.Contains(c.Header))
            .Should().HaveCount(expectedEnfantsUnmappedHeaders.Length).And.OnlyContain(c => c.Source == null);
    }

    // Lot 068 (couleur d'étiquette, client remark): the "ETIQUETTE" column -- unmapped since Lot 066 --
    // is now sourced from PLATINES' own new pivot field.
    [Fact]
    public async Task SeedAsync_CreatesExportProfile_WithEtiquetteColumn_MappedToIsolementCouleurEtiquette()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync();

        var profile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        var enfants = profile!.SheetRules.Single(r => r.SheetName == "Enfants");
        enfants.ColumnDefinitions.Should().ContainSingle(c => c.Header == "ETIQUETTE")
            .Which.Source.Should().Be(PivotFieldRef.IsolementCouleurEtiquette);
    }

    // Lot 066, 66.4: the same 16 Point columns live on Parents too, in the same order as Enfants --
    // marked via SheetGenerationEngine's aggregation mechanism (66.3). Lot 082 (D6): Parents' own
    // Equipement Point ("VISITE PRÉALABLE CHANTIER") comes first, before them.
    [Fact]
    public async Task SeedAsync_CreatesExportProfile_WithParentsPointColumns_EquipementPointThenEnfantsOnes()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync();

        var profile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        var parents = profile!.SheetRules.Single(r => r.SheetName == "Parents");
        var enfants = profile.SheetRules.Single(r => r.SheetName == "Enfants");

        // Lot 083 (E4): the client's order.
        string[] equipementColonnes =
        [
            "VISITE PRÉALABLE CHANTIER", "PROCÉDURE MAD", "AUTORISATION DÉPLATINAGES", "PROCÉDURE REL",
            "AUTORISATION DE REMISE EN SERVICE", "RÉCEPTION FINALE CHANTIER"
        ];
        parents.PointColumnDefinitions.Take(6).Should().Equal(
            equipementColonnes.Select(name => new PointColumnDefinition(name, name)));
        parents.PointColumnDefinitions.Skip(6).Should().Equal(enfants.PointColumnDefinitions);
        enfants.PointColumnDefinitions.Select(p => p.ColonneNom).Should().NotIntersectWith(equipementColonnes);
    }

    [Fact]
    public async Task SeedAsync_ExportParentsPointColumns_AllReferenceColonneNamesTheImportProfileActuallyProduces()
    {
        var seeder = CreateSeeder(out var importProfileStore, out var exportProfileStore);
        await seeder.SeedAsync();

        var importProfile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        var producedColonneNames = importProfile!.SheetRules
            .SelectMany(r => r.UnconditionalColonneNames
                .Concat(r.PointRules.Select(p => p.ColonneName)))
            .ToHashSet();

        var parents = exportProfile!.SheetRules.Single(r => r.SheetName == "Parents");
        parents.PointColumnDefinitions.Select(p => p.ColonneNom).Should().OnlyContain(
            colonneNom => producedColonneNames.Contains(colonneNom));
    }

    [Fact]
    public async Task SeedAsync_ExportEnfantsPointColumns_AllReferenceColonneNamesTheImportProfileActuallyProduces()
    {
        var seeder = CreateSeeder(out var importProfileStore, out var exportProfileStore);
        await seeder.SeedAsync();

        var importProfile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        var isolementStyleSheets = importProfile!.SheetRules.Where(r => r.SheetName != "PROCEDURE");
        var producedColonneNames = isolementStyleSheets
            .SelectMany(r => r.UnconditionalColonneNames
                .Concat(r.PointRules.Select(p => p.ColonneName)))
            .ToHashSet();

        var enfants = exportProfile!.SheetRules.Single(r => r.SheetName == "Enfants");
        enfants.PointColumnDefinitions.Select(p => p.ColonneNom).Should().OnlyContain(
            colonneNom => producedColonneNames.Contains(colonneNom));
    }

    [Fact]
    public async Task SeedAsync_ImportAndExportProfiles_ShareConsistentDefaultTableauxAndApplicationNames()
    {
        var seeder = CreateSeeder(out var importProfileStore, out var exportProfileStore);
        await seeder.SeedAsync();

        var importProfile = await importProfileStore.GetByIdAsync(DefaultProfileSeeder.ImportProfileId);
        var exportProfile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        importProfile!.DefaultTableaux.Should().NotBeEmpty();
        importProfile.DefaultApplicationNames.Should().NotBeEmpty();

        foreach (var rule in exportProfile!.SheetRules.Where(r => r.PivotSource != PivotSource.TacheMultiple))
        {
            foreach (var applicationColumn in rule.ApplicationColumnDefinitions)
            {
                importProfile.DefaultApplicationNames.Should().Contain(applicationColumn.ApplicationNom);
            }
        }
    }

    // T8 (docs/tickets-tdd-export-taches-multiples.md): simulates a profile seeded before this lot
    // (Lot M-era, Parents/Enfants only) still sitting under the stable ExportProfileId -- built and
    // saved directly through the store, bypassing the seeder entirely, exactly like a real profile
    // that predates the TacheMultiple rule would look today.
    private async Task<ExportProfile> SeedPreExistingProfileWithoutTacheMultipleRuleAsync(
        IExportProfileStore exportProfileStore, string name = "Profil OXO standard")
    {
        var preExisting = new ExportProfile(
            DefaultProfileSeeder.ExportProfileId, name,
            [
                new SheetGenerationRule(
                    "Parents", PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "TRAVAUX COMPLET")],
                    []),
                new SheetGenerationRule(
                    "Enfants", PivotSource.Isolement,
                    [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)],
                    [new PointColumnDefinition("PROLOCK VANNES", "PROLOCK VANNES")],
                    [])
            ]);

        await exportProfileStore.SaveAsync(preExisting);
        return preExisting;
    }

    [Fact]
    public async Task SeedAsync_WhenExistingExportProfileLacksTacheMultipleRule_AddsItOnce()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await SeedPreExistingProfileWithoutTacheMultipleRuleAsync(exportProfileStore);

        await seeder.SeedAsync();

        var migrated = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        // Order among owned SheetRules isn't guaranteed by the EF Core InMemory provider across a
        // delete+reinsert round trip (no explicit ordering column is configured) -- T8 only cares that
        // all 3 rules are present, not their relative order, so this asserts membership, not sequence.
        migrated!.SheetRules.Select(r => r.SheetName).Should().BeEquivalentTo(["Parents", "Enfants", "Tâches multiples"]);

        var tachesMultiples = migrated.SheetRules.Single(r => r.SheetName == "Tâches multiples");
        tachesMultiples.PivotSource.Should().Be(PivotSource.TacheMultiple);
        tachesMultiples.PointColumnDefinitions.Should().BeEmpty();
        // Lot 069: the migration path shares BuildTacheMultipleSheetRule with the nominal seed path, so
        // it gains the same columns automatically. Order-independent, same caveat as above.
        tachesMultiples.ColumnDefinitions.Should().HaveCount(17);
        tachesMultiples.ColumnDefinitions.Select(c => (c.Header, c.Source)).Should().BeEquivalentTo(new (string Header, PivotFieldRef? Source)[]
        {
            ("GUID", null),
            ("TYPE TACHE", PivotFieldRef.TacheMultipleTypeTacheMultipleCode),
            ("Repère TM", PivotFieldRef.TacheMultipleRepere),
            ("ZONE", PivotFieldRef.TacheMultipleLocalisation),
            ("LOC2", null),
            ("LOC3", null),
            ("TYPE ELEMENT CODE", PivotFieldRef.TacheMultipleTypeElementNom),
            ("LOT", null),
            ("Ressource", null),
            ("Ligne", PivotFieldRef.TacheMultipleLigneSource),
            ("Ordre", PivotFieldRef.TacheMultipleOrdre),
            ("Action", PivotFieldRef.TacheMultipleAction),
            ("Acteur", PivotFieldRef.TacheMultipleActeur),
            ("Risques", PivotFieldRef.TacheMultipleRisques),
            ("Date de validation", PivotFieldRef.TacheMultipleDateValidation),
            ("Colonne Travaux", PivotFieldRef.TacheMultipleColonneTravaux),
            ("CRITERE", PivotFieldRef.TacheMultipleCritere)
        });
    }

    [Fact]
    public async Task SeedAsync_WhenExistingExportProfileLacksTacheMultipleRule_LeavesParentsAndEnfantsStructurallyUnchanged()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        var preExisting = await SeedPreExistingProfileWithoutTacheMultipleRuleAsync(exportProfileStore, "Renamed by an admin");

        await seeder.SeedAsync();

        var migrated = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        // Admin customizations (a renamed profile, a trimmed-down Point column set) must survive the
        // migration untouched -- only the missing rule is appended.
        migrated!.Name.Should().Be("Renamed by an admin");
        migrated.SheetRules.Should().Contain(preExisting.SheetRules[0]);
        migrated.SheetRules.Should().Contain(preExisting.SheetRules[1]);
    }

    [Fact]
    public async Task SeedAsync_WhenExistingExportProfileAlreadyHasTacheMultipleRule_DoesNotModifyIt()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync(); // creates the full profile, TacheMultiple rule included from the start

        var beforeSecondRun = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);

        await seeder.SeedAsync();

        var afterSecondRun = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        afterSecondRun.Should().BeEquivalentTo(beforeSecondRun);
        afterSecondRun!.SheetRules.Count(r => r.PivotSource == PivotSource.TacheMultiple).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_CalledRepeatedlyAfterMigration_NeverCreatesMoreThanOneTacheMultipleRule()
    {
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await SeedPreExistingProfileWithoutTacheMultipleRuleAsync(exportProfileStore);

        await seeder.SeedAsync();
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var profile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        profile!.SheetRules.Count(r => r.PivotSource == PivotSource.TacheMultiple).Should().Be(1);
        profile.SheetRules.Select(r => r.SheetName).Should().BeEquivalentTo(["Parents", "Enfants", "Tâches multiples"]);
    }
}
