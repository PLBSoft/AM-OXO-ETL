using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Tests.Persistence.Repositories;

public class EfImportProfileStoreTests
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("EfImportProfileStoreTests_" + Guid.NewGuid());

    private IImportProfileStore CreateStore() => new EfImportProfileStore(_dbContextFactory);

    private static ImportProfile CreateSampleProfile(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields:
            [
                new BlockFieldDefinition("Identification", "B:E", 0, 0),
                new BlockFieldDefinition("Designation", "F:J", 0, 0),
                new BlockFieldDefinition("TypeElement", "K:N", 0, 0)
            ]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT",
            locator,
            pointRules:
            [
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE...")
            ],
            unconditionalColonneNames: ["PROLOCK VANNES", "DEPROLOCK VANNES"]);

        return new ImportProfile(name, "MAD-OXO-", equipementTypeElementNom, [sheetRule]);
    }

    [Fact]
    public async Task SaveAsync_WithNewProfile_PersistsProfileWithNestedSheetRules()
    {
        var profile = CreateSampleProfile();
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(profile.Id);
        reloaded.Name.Should().Be("MAD OXO");
        reloaded.ReperePrefix.Should().Be("MAD-OXO-");
        reloaded.SheetRules.Should().ContainSingle();

        var rule = reloaded.SheetRules.Single();
        rule.SheetName.Should().Be("ISOLEMENT");
        rule.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES", "DEPROLOCK VANNES");

        rule.Locator.Sheet.Should().Be("ISOLEMENT");
        rule.Locator.FirstBlockStartRow.Should().Be(9);
        rule.Locator.Step.Should().Be(7);
        rule.Locator.StopFieldName.Should().Be("Identification");
        rule.Locator.Fields.Should().HaveCount(3);
        rule.Locator.Fields.Should().Contain(f => f.Name == "Designation" && f.ColumnRange == "F:J");

        rule.PointRules.Should().ContainSingle();
        var pointRule = rule.PointRules.Single();
        pointRule.SourceFieldName.Should().Be("TypeElement");
        pointRule.Operator.Should().Be(ConditionOperator.Equals);
        pointRule.ComparisonValue.Should().Be("ZERO ENERGIE");
        pointRule.ColonneName.Should().Be("ZÉRO ENERGIE...");
    }

    [Fact]
    public async Task SaveAsync_PersistsEquipementTypeElementNom_NotSilentlyDroppedByEfMapping()
    {
        var profile = CreateSampleProfile(equipementTypeElementNom: "MAD TRAVAUX");
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.EquipementTypeElementNom.Should().Be("MAD TRAVAUX");
    }

    [Fact]
    public async Task SaveAsync_WithMultipleSheetRules_PersistsEachRulesNestedDataIndependently()
    {
        var isolementLocator = new RepeatingBlockLocator(
            "ISOLEMENT", 9, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var isolementRule = new SheetExtractionRule(
            "ISOLEMENT", isolementLocator,
            [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE...")],
            ["PROLOCK VANNES", "DEPROLOCK VANNES"]);

        var diversLocator = new RepeatingBlockLocator(
            "DIVERS", 6, 3, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var diversRule = new SheetExtractionRule(
            "DIVERS", diversLocator,
            [
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE 1"),
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE 2")
            ],
            []);

        var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [isolementRule, diversRule]);
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.SheetRules.Should().HaveCount(2);
        var reloadedIsolement = reloaded.SheetRules.Single(r => r.SheetName == "ISOLEMENT");
        var reloadedDivers = reloaded.SheetRules.Single(r => r.SheetName == "DIVERS");

        reloadedIsolement.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES", "DEPROLOCK VANNES");
        reloadedIsolement.PointRules.Should().ContainSingle();

        reloadedDivers.UnconditionalColonneNames.Should().BeEmpty();
        reloadedDivers.PointRules.Should().HaveCount(2);
        reloadedDivers.PointRules.Select(r => r.ColonneName).Should().Equal("SOUPAPE 1", "SOUPAPE 2");
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
        var original = CreateSampleProfile("Profil OXO", "MAD TRAVAUX");
        var store = CreateStore();
        await store.SaveAsync(original);

        var editedLocator = new RepeatingBlockLocator(
            "DIVERS", 6, 3, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var editedRule = new SheetExtractionRule("DIVERS", editedLocator, [], []);
        var edited = new ImportProfile(
            original.Id, "Profil OXO (édité)", original.ReperePrefix, "MAD TRAVAUX EDITE", [editedRule]);

        await store.SaveAsync(edited);

        var reloaded = await store.GetByIdAsync(original.Id);
        reloaded!.Name.Should().Be("Profil OXO (édité)");
        reloaded.EquipementTypeElementNom.Should().Be("MAD TRAVAUX EDITE");
        reloaded.SheetRules.Should().ContainSingle(r => r.SheetName == "DIVERS");

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
