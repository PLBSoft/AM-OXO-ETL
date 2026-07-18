using ExcelETL.Application.Generation;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class EfExportProfileStoreTests
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("EfExportProfileStoreTests_" + Guid.NewGuid());

    private IExportProfileStore CreateStore() => new EfExportProfileStore(_dbContextFactory);

    private static ExportProfile CreateSampleProfile(string name = "MAD OXO export") => new(
        name,
        [
            new SheetGenerationRule(
                "Parents",
                PivotSource.Equipement,
                [
                    new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere),
                    new ColumnDefinition("Colonne libre", null)
                ],
                [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")])
        ]);

    [Fact]
    public async Task SaveAsync_WithNewProfile_PersistsProfileWithNestedSheetRules()
    {
        var profile = CreateSampleProfile();
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(profile.Id);
        reloaded.Name.Should().Be("MAD OXO export");
        reloaded.SheetRules.Should().ContainSingle();

        var rule = reloaded.SheetRules.Single();
        rule.SheetName.Should().Be("Parents");
        rule.PivotSource.Should().Be(PivotSource.Equipement);

        rule.ColumnDefinitions.Should().HaveCount(2);
        rule.ColumnDefinitions.Should().Contain(c => c.Header == "Repère" && c.Source == PivotFieldRef.EquipementRepere);

        rule.PointColumnDefinitions.Should().ContainSingle();
        var pointColumn = rule.PointColumnDefinitions.Single();
        pointColumn.ColonneNom.Should().Be("TRAVAUX COMPLET");
        pointColumn.Header.Should().Be("Travaux complet");
        pointColumn.MarkValue.Should().Be("X");
    }

    [Fact]
    public async Task SaveAsync_WithNullColumnSource_PersistsAndReloadsAsNull_NotADefaultValue()
    {
        var profile = CreateSampleProfile();
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        var freeColumn = reloaded!.SheetRules.Single().ColumnDefinitions.Single(c => c.Header == "Colonne libre");
        freeColumn.Source.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithMultipleSheetRules_PersistsEachRulesNestedDataIndependently()
    {
        var parentsRule = new SheetGenerationRule(
            "Parents", PivotSource.Equipement,
            [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
            [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")]);

        var enfantsRule = new SheetGenerationRule(
            "Enfants", PivotSource.Isolement,
            [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)],
            [
                new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes"),
                new PointColumnDefinition("DEPROLOCK VANNES", "Deprolock vannes")
            ]);

        var profile = new ExportProfile("Profil export test", [parentsRule, enfantsRule]);
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.SheetRules.Should().HaveCount(2);
        var reloadedParents = reloaded.SheetRules.Single(r => r.SheetName == "Parents");
        var reloadedEnfants = reloaded.SheetRules.Single(r => r.SheetName == "Enfants");

        reloadedParents.PointColumnDefinitions.Should().ContainSingle();
        reloadedEnfants.PointColumnDefinitions.Should().HaveCount(2);
        reloadedEnfants.PointColumnDefinitions.Select(p => p.ColonneNom).Should().Equal("PROLOCK VANNES", "DEPROLOCK VANNES");
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var store = CreateStore();

        var result = await store.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProfilesOrderedByName()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSampleProfile("Zebra Profile"));
        await store.SaveAsync(CreateSampleProfile("Alpha Profile"));

        var result = await store.GetAllAsync();

        result.Select(p => p.Name).Should().Equal("Alpha Profile", "Zebra Profile");
    }

    [Fact]
    public async Task SaveAsync_WithExistingProfileId_ReplacesContentInPlace_WithoutDuplicating()
    {
        var original = CreateSampleProfile("Profil export OXO");
        var store = CreateStore();
        await store.SaveAsync(original);

        var editedRule = new SheetGenerationRule(
            "Enfants", PivotSource.Isolement, [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)], []);
        var edited = new ExportProfile(original.Id, "Profil export OXO (édité)", [editedRule]);

        await store.SaveAsync(edited);

        var reloaded = await store.GetByIdAsync(original.Id);
        reloaded!.Name.Should().Be("Profil export OXO (édité)");
        reloaded.SheetRules.Should().ContainSingle(r => r.SheetName == "Enfants");

        var all = await store.GetAllAsync();
        all.Should().ContainSingle(p => p.Id == original.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingProfile_RemovesItAndItsNestedRows()
    {
        var profile = CreateSampleProfile();
        var store = CreateStore();
        await store.SaveAsync(profile);

        await store.DeleteAsync(profile.Id);

        var result = await store.GetByIdAsync(profile.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_DoesNotThrow()
    {
        var store = CreateStore();

        var act = async () => await store.DeleteAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }
}
