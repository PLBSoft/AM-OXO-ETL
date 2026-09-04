using ExcelETL.Application.Exceptions;
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
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX",
        IReadOnlyList<string>? defaultTableaux = null, IReadOnlyList<string>? defaultApplicationNames = null,
        IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null)
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
            unconditionalColonneNames: ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []);

        return new ImportProfile(
            name, "MAD-OXO-", equipementTypeElementNom,
            defaultTableaux ?? ["TRAVAUX COMPLET", "TRAVAUX DETAIL"], defaultApplicationNames ?? ["PROGRESS"], [sheetRule],
            tacheMultipleTypeLabels);
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
        reloaded.DefaultTableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        reloaded.DefaultApplicationNames.Should().Equal("PROGRESS");
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
    public async Task SaveAsync_WithEmptyDefaultTableauxAndApplicationNames_PersistsAndReloadsAsEmpty()
    {
        var profile = CreateSampleProfile(defaultTableaux: [], defaultApplicationNames: []);
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.DefaultTableaux.Should().BeEmpty();
        reloaded.DefaultApplicationNames.Should().BeEmpty();
    }

    // Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md).
    [Fact]
    public async Task SaveAsync_WithTacheMultipleTypeLabels_PersistsAndReloadsThem()
    {
        var profile = CreateSampleProfile(tacheMultipleTypeLabels:
        [
            new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD"),
            new TacheMultipleTypeLabel("TM_PROC_REL", "Procédure REL")
        ]);
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.TacheMultipleTypeLabels.Should().HaveCount(2);
        reloaded.TacheMultipleTypeLabels.Should().Contain(l => l.Code == "TM_PROC_MAD" && l.Label == "Procédure MAD");
        reloaded.TacheMultipleTypeLabels.Should().Contain(l => l.Code == "TM_PROC_REL" && l.Label == "Procédure REL");
    }

    [Fact]
    public async Task SaveAsync_WithoutTacheMultipleTypeLabels_PersistsAndReloadsAsEmpty()
    {
        var profile = CreateSampleProfile();
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.TacheMultipleTypeLabels.Should().BeEmpty();
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
            ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], []);

        var diversLocator = new RepeatingBlockLocator(
            "DIVERS", 6, 3, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var diversRule = new SheetExtractionRule(
            "DIVERS", diversLocator,
            [
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE 1"),
                new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "SOUPAPE", "SOUPAPE 2")
            ],
            [], [], []);

        var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [isolementRule, diversRule]);
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
    public async Task SaveAsync_WithHeaderFieldsAndComposites_RoundTripsIdentically()
    {
        // Lot 047 (docs/tickets/tickets-tdd-lot-047-extraction-entetes-profile-driven-directcell.md),
        // 47.3: HeaderFieldRule (direct, incl. StripReperePrefix/DateFormat) + HeaderCompositeRule
        // (template referencing a HeaderFieldRule.Name) must round-trip through EF Core intact.
        var locator = new RepeatingBlockLocator(
            "PROCEDURE", 9, 1, "Action", [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
        var sheetRule = new SheetExtractionRule(
            "PROCEDURE", locator, [], [],
            headerFields:
            [
                new HeaderFieldRule("nomMAD", new DirectCell("PROCEDURE", "M2:O2"), stripReperePrefix: true),
                new HeaderFieldRule("revision", new DirectCell("PROCEDURE", "P2:Q2")),
                new HeaderFieldRule("dateRev", new DirectCell("PROCEDURE", "R2:T2"), dateFormat: "dd/MM/yyyy")
            ],
            headerComposites: [new HeaderCompositeRule("Designation", "Rév {revision} du {dateRev}")]);
        var profile = new ImportProfile("Profil avec en-têtes", "MAD TRAVAUX", [], [], [sheetRule]);
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        var reloadedRule = reloaded!.SheetRules.Single();
        reloadedRule.HeaderFields.Should().HaveCount(3);

        var nomMad = reloadedRule.HeaderFields.Single(f => f.Name == "nomMAD");
        nomMad.Cell.Sheet.Should().Be("PROCEDURE");
        nomMad.Cell.Range.Should().Be("M2:O2");
        nomMad.StripReperePrefix.Should().BeTrue();
        nomMad.DateFormat.Should().BeNull();

        var dateRev = reloadedRule.HeaderFields.Single(f => f.Name == "dateRev");
        dateRev.Cell.Range.Should().Be("R2:T2");
        dateRev.StripReperePrefix.Should().BeFalse();
        dateRev.DateFormat.Should().Be("dd/MM/yyyy");

        reloadedRule.HeaderComposites.Should().ContainSingle();
        var designation = reloadedRule.HeaderComposites.Single();
        designation.Name.Should().Be("Designation");
        designation.Template.Should().Be("Rév {revision} du {dateRev}");
    }

    [Fact]
    public async Task SaveAsync_WithNoHeaderRules_PersistsAndReloadsAsEmptyLists()
    {
        // Non-régression: an isolement-style sheet with no header rules at all (the pre-Lot-047
        // shape) stays valid -- CreateSampleProfile's ISOLEMENT rule never sets HeaderFields/
        // HeaderComposites.
        var profile = CreateSampleProfile();
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        var rule = reloaded!.SheetRules.Single();
        rule.HeaderFields.Should().BeEmpty();
        rule.HeaderComposites.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WithZeroEnergieExpectedValue_RoundTripsIdentically()
    {
        // Lot 063.
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT", 19, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("HasZeroEnergie", "V", -1, 0)]);
        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator,
            [new ConditionalPointRule("HasZeroEnergie", ConditionOperator.Equals, "true", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")],
            ["PROLOCK VANNES", "DEPROLOCK VANNES"], [], [], zeroEnergieExpectedValue: "ZERO ENERGIE");
        var profile = new ImportProfile("Profil zero energie", "MAD TRAVAUX", [], [], [sheetRule]);
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.SheetRules.Single().ZeroEnergieExpectedValue.Should().Be("ZERO ENERGIE");
    }

    [Fact]
    public async Task SaveAsync_WithNullZeroEnergieExpectedValue_PersistsAndReloadsAsNull()
    {
        // Not an empty string by default value -- same requirement as the other nullable owned-type
        // columns on this mapping (e.g. ColumnDefinition.Source, Lot I6).
        var profile = CreateSampleProfile();
        var store = CreateStore();

        await store.SaveAsync(profile);
        var reloaded = await store.GetByIdAsync(profile.Id);

        reloaded!.SheetRules.Single().ZeroEnergieExpectedValue.Should().BeNull();
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
        var editedRule = new SheetExtractionRule("DIVERS", editedLocator, [], [], [], []);
        var edited = new ImportProfile(
            original.Id, "Profil OXO (édité)", original.ReperePrefix, "MAD TRAVAUX EDITE", [], [], [editedRule]);

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

        // Lot 028 (28.1): the ticket's own expected behavior also covers GetAllAsync, not just
        // GetByIdAsync -- both were already true given DeleteAsync's implementation, just not
        // previously asserted together.
        var all = await store.GetAllAsync();
        all.Should().NotContain(p => p.Id == profile.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_DoesNotThrow()
    {
        var store = CreateStore();

        var act = async () => await store.DeleteAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_WithNameOfAnAlreadyExistingProfile_ThrowsProfileNameAlreadyExistsException_AndInsertsNothing()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSampleProfile("Profil A"));

        var act = async () => await store.SaveAsync(CreateSampleProfile("Profil A"));

        (await act.Should().ThrowAsync<ProfileNameAlreadyExistsException>())
            .Which.Name.Should().Be("Profil A");
        var all = await store.GetAllAsync();
        all.Should().ContainSingle();
    }

    [Fact]
    public async Task SaveAsync_UpdateWithOwnUnchangedName_DoesNotThrow()
    {
        var profile = CreateSampleProfile("Profil A");
        var store = CreateStore();
        await store.SaveAsync(profile);

        var edited = new ImportProfile(
            profile.Id, "Profil A", profile.ReperePrefix, profile.EquipementTypeElementNom, [], [], [.. profile.SheetRules]);

        var act = async () => await store.SaveAsync(edited);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_UpdateWithAnotherExistingProfilesName_ThrowsProfileNameAlreadyExistsException()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSampleProfile("Profil A"));
        var second = CreateSampleProfile("Profil B");
        await store.SaveAsync(second);

        var edited = new ImportProfile(
            second.Id, "Profil A", second.ReperePrefix, second.EquipementTypeElementNom, [], [], [.. second.SheetRules]);

        var act = async () => await store.SaveAsync(edited);

        await act.Should().ThrowAsync<ProfileNameAlreadyExistsException>();
    }

    [Fact]
    public async Task SaveAsync_WithNameCollidingOnlyAfterTrimAndCaseNormalization_ThrowsProfileNameAlreadyExistsException()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSampleProfile("Profil A"));

        var act = async () => await store.SaveAsync(CreateSampleProfile("  profil a  "));

        await act.Should().ThrowAsync<ProfileNameAlreadyExistsException>();
    }

    [Fact]
    public async Task SaveAsync_WithTwoGenuinelyDistinctNames_DoesNotThrow_BothCoexist()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSampleProfile("Profil A"));

        var act = async () => await store.SaveAsync(CreateSampleProfile("Profil B"));

        await act.Should().NotThrowAsync();
        var all = await store.GetAllAsync();
        all.Should().HaveCount(2);
    }
}
