using ExcelETL.Application.Extraction.Oxo;
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
            seeded!.Id, "Renamed by an admin", seeded.ReperePrefix, seeded.EquipementTypeElementNom, seeded.SheetRules);
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

        var isolement = profile.SheetRules.Single(r => r.SheetName == "ISOLEMENT");
        isolement.Locator.FirstBlockStartRow.Should().Be(19);
        isolement.Locator.Step.Should().Be(7);
        isolement.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES", "DEPROLOCK VANNES");
        isolement.PointRules.Should().ContainSingle();
        isolement.PointRules.Single().ComparisonValue.Should().Be("ZERO ENERGIE");
        isolement.PointRules.Single().ColonneName.Should().Be("ZÉRO ENERGIE EN PRESENCE EE (PS941)");

        var platines = profile.SheetRules.Single(r => r.SheetName == "PLATINES");
        platines.Locator.Step.Should().Be(8);
        platines.UnconditionalColonneNames.Should().HaveCount(7);
        platines.UnconditionalColonneNames.Should().NotContain(name => name.Contains("FIN"));
        platines.PointRules.Should().BeEmpty();

        var orificesCapacites = profile.SheetRules.Single(r => r.SheetName == "ORIFICES CAPACITES");
        orificesCapacites.Locator.Step.Should().Be(8);
        orificesCapacites.UnconditionalColonneNames.Should().HaveCount(4);

        var autresJointsTouches = profile.SheetRules.Single(r => r.SheetName == "AUTRES JOINTS TOUCHES");
        autresJointsTouches.Locator.Step.Should().Be(7);
        autresJointsTouches.PointRules.Should().ContainSingle();
        autresJointsTouches.PointRules.Single().ColonneName.Should().Be("POSE ÉTIQUETTES");
        autresJointsTouches.PointRules.Single().ComparisonValue.Should().Be("TUBING");

        var divers = profile.SheetRules.Single(r => r.SheetName == "DIVERS");
        divers.Locator.FirstBlockStartRow.Should().Be(9);
        divers.Locator.Step.Should().Be(3);
        divers.UnconditionalColonneNames.Should().BeEmpty();
        divers.PointRules.Should().HaveCount(7);
        divers.PointRules.Select(r => r.ColonneName).Should().Contain("PF : ACCORD TRAVAUX FEU");
        divers.PointRules.Select(r => r.ColonneName).Should().NotContain(name => name.Contains("POINT DE FEU"));
        divers.PointRules.Select(r => r.ColonneName).Should().Contain("ZÉRO ENERGIE EN PRESENCE EE");
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
        parents.ColumnDefinitions.Should().OnlyContain(c => c.Source != null);
        parents.ColumnDefinitions.Select(c => c.Source).Should().BeEquivalentTo(
        [
            PivotFieldRef.EquipementRepere,
            PivotFieldRef.EquipementTypeElementNom,
            PivotFieldRef.EquipementLocalisation,
            PivotFieldRef.EquipementDesignation
        ]);
        parents.PointColumnDefinitions.Select(p => p.ColonneNom).Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");

        var enfants = profile.SheetRules.Single(r => r.SheetName == "Enfants");
        enfants.PivotSource.Should().Be(PivotSource.Isolement);
        enfants.ColumnDefinitions.Should().OnlyContain(c => c.Source != null);
        enfants.ColumnDefinitions.Select(c => c.Source).Should().BeEquivalentTo(
        [
            PivotFieldRef.IsolementRepere,
            PivotFieldRef.IsolementTypeElementNom,
            PivotFieldRef.IsolementLocalisation,
            PivotFieldRef.IsolementDesignation,
            PivotFieldRef.IsolementPositionALaPose
        ]);
        enfants.PointColumnDefinitions.Should().HaveCount(17);

        var tachesMultiples = profile.SheetRules.Single(r => r.SheetName == "Tâches multiples");
        tachesMultiples.PivotSource.Should().Be(PivotSource.TacheMultiple);
        tachesMultiples.PointColumnDefinitions.Should().BeEmpty();
        tachesMultiples.ColumnDefinitions.Select(c => c.Source).Should().BeEquivalentTo(
        [
            PivotFieldRef.TacheMultipleOrdre,
            PivotFieldRef.TacheMultipleAction,
            PivotFieldRef.TacheMultipleActeur,
            PivotFieldRef.TacheMultipleRisques,
            PivotFieldRef.TacheMultipleDateValidation
        ]);
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
            .SelectMany(r => r.UnconditionalColonneNames.Concat(r.PointRules.Select(p => p.ColonneName)))
            .ToHashSet();

        var enfants = exportProfile!.SheetRules.Single(r => r.SheetName == "Enfants");
        enfants.PointColumnDefinitions.Select(p => p.ColonneNom).Should().OnlyContain(
            colonneNom => producedColonneNames.Contains(colonneNom));
    }

    [Fact]
    public async Task SeedAsync_ExportParentsPointColumns_MatchProcedureExtractionServicesHardcodedTravauxColonneNames()
    {
        // PROCEDURE's 2 Points are hardcoded in ProcedureExtractionService, not driven by the import
        // SheetExtractionRule (see docs/tickets-tdd-seed-profils-defaut.md M2 §1) -- so this is a
        // literal comparison, not a cross-profile aggregation like the Enfants test above.
        var seeder = CreateSeeder(out _, out var exportProfileStore);
        await seeder.SeedAsync();

        var exportProfile = await exportProfileStore.GetByIdAsync(DefaultProfileSeeder.ExportProfileId);
        var parents = exportProfile!.SheetRules.Single(r => r.SheetName == "Parents");

        parents.PointColumnDefinitions.Select(p => p.ColonneNom).Should().BeEquivalentTo(
            ["TRAVAUX COMPLET", "TRAVAUX DETAIL"]);
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
                    [new PointColumnDefinition("TRAVAUX COMPLET", "TRAVAUX COMPLET")]),
                new SheetGenerationRule(
                    "Enfants", PivotSource.Isolement,
                    [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)],
                    [new PointColumnDefinition("PROLOCK VANNES", "PROLOCK VANNES")])
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
        tachesMultiples.ColumnDefinitions.Select(c => c.Source).Should().BeEquivalentTo(
        [
            PivotFieldRef.TacheMultipleOrdre,
            PivotFieldRef.TacheMultipleAction,
            PivotFieldRef.TacheMultipleActeur,
            PivotFieldRef.TacheMultipleRisques,
            PivotFieldRef.TacheMultipleDateValidation
        ]);
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
