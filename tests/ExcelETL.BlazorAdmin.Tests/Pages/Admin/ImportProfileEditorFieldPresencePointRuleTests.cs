using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Client-reported (2026-09-16): PLATINES' "RECEPTION DEBUT MAD"/"RECEPTION DEBUT REL" field-presence
// rules (seeded since 2026-09-04) "disappeared" from the standard profile. SheetRuleForm rebuilt the
// SheetExtractionRule without them, so editing and saving a PLATINES rule silently deleted both --
// same class of data-loss bug as Lot 048.1 for header rules.
public class ImportProfileEditorFieldPresencePointRuleTests : BunitContext
{
    public ImportProfileEditorFieldPresencePointRuleTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorFieldPresencePointRuleTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private static ImportProfile BuildProfileWithPlatinesFieldPresenceRules()
    {
        var locator = new RepeatingBlockLocator(
            "PLATINES", 17, 8, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var rule = new SheetExtractionRule(
            "PLATINES", locator, [], ["POSE ÉTIQUETTES"], [], [],
            fieldPresencePointRules:
            [
                new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD"),
                new FieldPresencePointRule(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL")
            ],
            couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1));

        return new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [rule]);
    }

    [Fact]
    public async Task ModifyAndSaveSheetRule_PreservesFieldPresencePointRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-allowed-couleurs-etiquette-input").Change("ROUGE, BLANC");
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            var platines = reloaded!.SheetRules.Single();
            platines.AllowedCouleursEtiquette.Should().Equal("ROUGE", "BLANC");
            platines.FieldPresencePointRules
                .Select(r => (r.ColonneName, r.Cell.ColumnRange, r.Cell.RowOffsetStart, r.Cell.RowOffsetEnd))
                .Should().BeEquivalentTo(new[]
                {
                    ("RECEPTION DEBUT MAD", "H:N", 2, 2),
                    ("RECEPTION DEBUT REL", "H:N", 3, 3)
                });
        });

    // --- Full editing (client request 2026-09-16): the rules were invisible in the editor. ---

    private static void ExpandDetails(IRenderedComponent<ImportProfileEditor> cut) =>
        cut.Find("#sheet-rule-details-toggle-0").Click();

    private async Task<SheetExtractionRule> SaveSheetRuleAndProfileAndReloadAsync(
        IRenderedComponent<ImportProfileEditor> cut, Guid profileId)
    {
        cut.Find("#save-sheet-rule-button-0").Click();
        cut.Find("#save-profile-button").Click();
        var reloaded = await Store.GetByIdAsync(profileId);
        return reloaded!.SheetRules.Single();
    }

    [Fact]
    public async Task SheetRuleCardSummary_ShowsFieldPresencePointRules_WithAbsoluteCellAndCount() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("2 filled-cell rules");
            ExpandDetails(cut);
            var content = cut.Find("#sheet-rule-details-content-0").TextContent;
            content.Should().Contain("Colonnes checked when a cell is filled in");
            content.Should().Contain("RECEPTION DEBUT MAD (H19:N19)");
            content.Should().Contain("RECEPTION DEBUT REL (H20:N20)");
        });

    [Fact]
    public async Task ModifyMode_ListsExistingFieldPresencePointRules_WithModifyAndDeleteButtons() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            var items = cut.FindAll("#edit-0-field-presence-rule-list .block-field-item");
            items.Should().HaveCount(2);
            items[0].QuerySelector(".block-field-name")!.TextContent.Should().Be("RECEPTION DEBUT MAD");
            items[0].QuerySelector(".block-field-range")!.TextContent.Should().Be("H19:N19");
            cut.FindAll("#edit-0-modify-field-presence-rule-button-1").Should().ContainSingle();
            cut.FindAll("#edit-0-delete-field-presence-rule-button-1").Should().ContainSingle();
        });

    [Fact]
    public async Task ModifyFieldPresencePointRule_PrefillsThenPersistsChanges_KeepingOriginalCellName() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-field-presence-rule-button-0").Click();

            cut.Find("#edit-0-field-presence-rule-0-colonne-name-input").GetAttribute("value").Should().Be("RECEPTION DEBUT MAD");
            cut.Find("#edit-0-field-presence-rule-0-absolute-range-input").GetAttribute("value").Should().Be("H19:N19");

            cut.Find("#edit-0-field-presence-rule-0-colonne-name-input").Change("RECEPTION FIN MAD");
            cut.Find("#edit-0-field-presence-rule-0-absolute-range-input").Change("H21:N21");
            cut.Find("#edit-0-save-field-presence-rule-button-0").Click();

            var rule = await SaveSheetRuleAndProfileAndReloadAsync(cut, profile.Id);
            var modified = rule.FieldPresencePointRules.Single(r => r.ColonneName == "RECEPTION FIN MAD");
            modified.Cell.Name.Should().Be("PoseeLe");
            modified.Cell.ColumnRange.Should().Be("H:N");
            modified.Cell.RowOffsetStart.Should().Be(4);
            modified.Cell.RowOffsetEnd.Should().Be(4);
            rule.FieldPresencePointRules.Should().HaveCount(2);
        });

    [Fact]
    public async Task CancelFieldPresencePointRuleEdit_DiscardsChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-field-presence-rule-button-0").Click();
            cut.Find("#edit-0-field-presence-rule-0-colonne-name-input").Change("SOMETHING ELSE");
            cut.Find("#edit-0-cancel-field-presence-rule-button-0").Click();

            cut.FindAll("#edit-0-field-presence-rule-list .block-field-name")[0].TextContent.Should().Be("RECEPTION DEBUT MAD");
        });

    [Fact]
    public async Task DeleteFieldPresencePointRule_RemovesItFromPersistedRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-delete-field-presence-rule-button-0").Click();

            var rule = await SaveSheetRuleAndProfileAndReloadAsync(cut, profile.Id);
            rule.FieldPresencePointRules.Should().ContainSingle().Which.ColonneName.Should().Be("RECEPTION DEBUT REL");
        });

    [Fact]
    public async Task AddFieldPresencePointRule_PersistsColonneNameAndCellOffsets() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-field-presence-rule-colonne-name-input").Change("RECEPTION FIN REL");
            cut.Find("#edit-0-field-presence-rule-absolute-range-input").Change("H22:N22");
            cut.Find("#edit-0-add-field-presence-rule-button").Click();

            var rule = await SaveSheetRuleAndProfileAndReloadAsync(cut, profile.Id);
            rule.FieldPresencePointRules.Should().HaveCount(3);
            var added = rule.FieldPresencePointRules.Single(r => r.ColonneName == "RECEPTION FIN REL");
            added.Cell.ColumnRange.Should().Be("H:N");
            added.Cell.RowOffsetStart.Should().Be(5);
            added.Cell.RowOffsetEnd.Should().Be(5);
        });

    [Fact]
    public async Task AddFieldPresencePointRule_WithInvalidRange_ShowsErrorAndDoesNotAdd() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-field-presence-rule-colonne-name-input").Change("RECEPTION FIN REL");
            cut.Find("#edit-0-field-presence-rule-absolute-range-input").Change("not a range");
            cut.Find("#edit-0-add-field-presence-rule-button").Click();

            cut.FindAll("[role='alert']").Should().Contain(a => a.TextContent.Contains("Excel"));
            cut.FindAll("#edit-0-field-presence-rule-list .block-field-item").Should().HaveCount(2);
        });

    [Fact]
    public async Task AddFieldPresencePointRule_WithEmptyColonneName_ShowsLocalizedDomainError() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-field-presence-rule-absolute-range-input").Change("H22:N22");
            cut.Find("#edit-0-add-field-presence-rule-button").Click();

            cut.FindAll("[role='alert']").Should().Contain(a =>
                !string.IsNullOrWhiteSpace(a.TextContent) && !a.TextContent.Contains("FieldPresencePointRule_"));
            cut.FindAll("#edit-0-field-presence-rule-list .block-field-item").Should().HaveCount(2);
        });

    [Fact]
    public void AddSheetRuleForm_FieldPresencePointRuleInputs_HaveAssociatedLabels()
    {
        var cut = Render<ImportProfileEditor>();
        if (cut.FindAll("#sheet-rule-name-input").Count == 0)
        {
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
        }

        cut.FindAll("label[for='field-presence-rule-colonne-name-input']").Should().ContainSingle();
        cut.FindAll("label[for='field-presence-rule-absolute-range-input']").Should().ContainSingle();
    }

    [Fact]
    public async Task SaveProfile_WithoutTouchingSheetRules_PreservesFieldPresencePointRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#profile-name-input").Change("MAD OXO renamed");
            cut.Find("#save-profile-button").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.SheetRules.Single().FieldPresencePointRules.Should().HaveCount(2);
        });
}
