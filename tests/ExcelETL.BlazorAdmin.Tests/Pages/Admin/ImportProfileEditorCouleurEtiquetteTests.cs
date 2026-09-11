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

// Client feedback (2026-09-11): DefaultCouleurEtiquette (fixed value for the whole sheet, e.g.
// AUTRES JOINTS TOUCHES = "BLEUE") and CouleurEtiquetteCell (per-block absolute range, e.g.
// PLATINES/ORIFICES CAPACITES = H18:N18) are now editable, closing the deliberate deferral both
// fields had since Lot 068/their own follow-up commit ("only settable via DefaultProfileSeeder.cs").
// Kept in its own file per this project's established convention (mirrors ImportProfileEditorLot063Tests.cs).
public class ImportProfileEditorCouleurEtiquetteTests : BunitContext
{
    public ImportProfileEditorCouleurEtiquetteTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorCouleurEtiquetteTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private static ImportProfile BuildProfileWithAjtAndPlatinesSheetRules(string name = "MAD OXO")
    {
        var ajtLocator = new RepeatingBlockLocator(
            "AUTRES JOINTS TOUCHES", 17, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var ajtRule = new SheetExtractionRule(
            "AUTRES JOINTS TOUCHES", ajtLocator, [], ["POSE ÉTIQUETTES"], [], [],
            defaultCouleurEtiquette: "BLEUE");

        var platinesLocator = new RepeatingBlockLocator(
            "PLATINES", 17, 8, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var platinesRule = new SheetExtractionRule(
            "PLATINES", platinesLocator, [], ["POSE ÉTIQUETTES"], [], [],
            couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1));

        return new ImportProfile(name, "MAD TRAVAUX", [], [], [ajtRule, platinesRule]);
    }

    private static void OpenAddSheetRuleFormIfClosed(IRenderedComponent<ImportProfileEditor> cut)
    {
        if (cut.FindAll("#sheet-rule-name-input").Count == 0)
        {
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
        }
    }

    [Fact]
    public void AddSheetRuleForm_DefaultCouleurEtiquetteAndCouleurEtiquetteCellFields_HaveAssociatedLabels() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            OpenAddSheetRuleFormIfClosed(cut);

            cut.Find("label[for='sheet-rule-default-couleur-etiquette-input']").TextContent.Should().NotBeNullOrEmpty();
            cut.Find("label[for='sheet-rule-couleur-etiquette-cell-input']").TextContent.Should().NotBeNullOrEmpty();
        });

    [Fact]
    public async Task AddSheetRule_WithDefaultCouleurEtiquette_RoundTripsThroughTheStore() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("AUTRES JOINTS TOUCHES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#sheet-rule-default-couleur-etiquette-input").Change("BLEUE");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B17:E18");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.SheetRules.Single().DefaultCouleurEtiquette.Should().Be("BLEUE");
            reloaded.SheetRules.Single().CouleurEtiquetteCell.Should().BeNull();
        });

    [Fact]
    public async Task AddSheetRule_WithCouleurEtiquetteCell_RoundTripsThroughTheStore() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("PLATINES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("8");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#sheet-rule-couleur-etiquette-cell-input").Change("H18:N18");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B17:E18");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            var cell = reloaded.SheetRules.Single().CouleurEtiquetteCell;
            cell.Should().NotBeNull();
            cell!.ColumnRange.Should().Be("H:N");
            cell.RowOffsetStart.Should().Be(1);
            cell.RowOffsetEnd.Should().Be(1);
            reloaded.SheetRules.Single().DefaultCouleurEtiquette.Should().BeNull();
        });

    [Fact]
    public async Task AddSheetRule_WithBothFieldsLeftEmpty_PersistsBothAsNull() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B19:E20");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.SheetRules.Single().DefaultCouleurEtiquette.Should().BeNull();
            reloaded.SheetRules.Single().CouleurEtiquetteCell.Should().BeNull();
        });

    [Fact]
    public async Task AddSheetRule_WithInvalidCouleurEtiquetteCellRange_ShowsErrorAndDoesNotSubmit() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("PLATINES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("8");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#sheet-rule-couleur-etiquette-cell-input").Change("not a range");
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find(".alert-danger").TextContent.Should().NotBeNullOrEmpty();
            cut.FindAll(".sheet-rule-card").Should().BeEmpty();
        });

    [Fact]
    public async Task ModifySheetRule_PrefillsDefaultCouleurEtiquetteAndCouleurEtiquetteCell_AndCancelRestoresOriginalWithoutPersisting() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithAjtAndPlatinesSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-default-couleur-etiquette-input").GetAttribute("value").Should().Be("BLEUE");
            cut.Find("#edit-0-sheet-rule-default-couleur-etiquette-input").Change("ROUGE");
            cut.Find("#cancel-sheet-rule-button-0").Click();

            cut.Find("#modify-sheet-rule-button-1").Click();
            cut.Find("#edit-1-sheet-rule-couleur-etiquette-cell-input").GetAttribute("value").Should().Be("H18:N18");
            cut.Find("#edit-1-sheet-rule-couleur-etiquette-cell-input").Change("B1");
            cut.Find("#cancel-sheet-rule-button-1").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.SheetRules.Single(r => r.SheetName == "AUTRES JOINTS TOUCHES").DefaultCouleurEtiquette.Should().Be("BLEUE");
            var platinesCell = reloaded.SheetRules.Single(r => r.SheetName == "PLATINES").CouleurEtiquetteCell;
            platinesCell!.ColumnRange.Should().Be("H:N");
            platinesCell.RowOffsetStart.Should().Be(1);
        });

    [Fact]
    public async Task Summary_DisplaysDefaultCouleurEtiquetteAndCouleurEtiquetteCell_EachOnlyForItsOwnSheet() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithAjtAndPlatinesSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var cards = cut.FindAll(".sheet-rule-card");
            cards.Should().HaveCount(2);

            var ajtCardMarkup = cards[0].OuterHtml;
            var platinesCardMarkup = cards[1].OuterHtml;

            ajtCardMarkup.Should().Contain("BLEUE");
            ajtCardMarkup.Should().NotContain("H18:N18");

            platinesCardMarkup.Should().Contain("H18:N18");
            platinesCardMarkup.Should().NotContain("BLEUE");
        });

    [Fact]
    public void AddSheetRuleForm_FillingOnlyDefaultCouleurEtiquette_MarksProfileAsChanged() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        OpenAddSheetRuleFormIfClosed(cut);

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#sheet-rule-default-couleur-etiquette-input").Change("BLEUE");

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
    });

    // Client feedback (2026-09-11): whitelist replacing the earlier hardcoded "DATE" blacklist --
    // AllowedCouleursEtiquette, exposed as a single comma-separated field rather than a full
    // add/edit/delete sub-list (this list is short and rarely edited, unlike UnconditionalColonneNames).
    [Fact]
    public void AddSheetRuleForm_AllowedCouleursEtiquetteField_HasAssociatedLabel() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        OpenAddSheetRuleFormIfClosed(cut);

        cut.Find("label[for='sheet-rule-allowed-couleurs-etiquette-input']").TextContent.Should().NotBeNullOrEmpty();
    });

    [Fact]
    public async Task AddSheetRule_WithAllowedCouleursEtiquette_SplitsTrimsAndRoundTripsThroughTheStore() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("PLATINES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("8");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#sheet-rule-couleur-etiquette-cell-input").Change("H18:N18");
            cut.Find("#sheet-rule-allowed-couleurs-etiquette-input").Change(" ROUGE, BLEUE ,JAUNE");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B17:E18");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.SheetRules.Single().AllowedCouleursEtiquette.Should().BeEquivalentTo(["ROUGE", "BLEUE", "JAUNE"]);
        });

    [Fact]
    public async Task AddSheetRule_WithAllowedCouleursEtiquetteLeftEmpty_PersistsAsNull() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("PLATINES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("8");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#sheet-rule-couleur-etiquette-cell-input").Change("H18:N18");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B17:E18");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.SheetRules.Single().AllowedCouleursEtiquette.Should().BeNull();
        });

    [Fact]
    public async Task ModifySheetRule_PrefillsAllowedCouleursEtiquetteAsCommaSeparatedText_AndCancelRestoresOriginalWithoutPersisting() =>
        await WithCultureAsync("en-US", async () =>
        {
            var platinesLocator = new RepeatingBlockLocator(
                "PLATINES", 17, 8, "Identification", [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
            var platinesRule = new SheetExtractionRule(
                "PLATINES", platinesLocator, [], ["POSE ÉTIQUETTES"], [], [],
                couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1),
                allowedCouleursEtiquette: ["ROUGE", "BLEUE", "JAUNE"]);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [platinesRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-allowed-couleurs-etiquette-input").GetAttribute("value").Should().Be("ROUGE, BLEUE, JAUNE");
            cut.Find("#edit-0-sheet-rule-allowed-couleurs-etiquette-input").Change("ROUGE");
            cut.Find("#cancel-sheet-rule-button-0").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.SheetRules.Single().AllowedCouleursEtiquette.Should().BeEquivalentTo(["ROUGE", "BLEUE", "JAUNE"]);
        });

    [Fact]
    public async Task Summary_DisplaysAllowedCouleursEtiquette_OnlyForTheSheetThatConfiguresIt() =>
        await WithCultureAsync("en-US", async () =>
        {
            var platinesLocator = new RepeatingBlockLocator(
                "PLATINES", 17, 8, "Identification", [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
            var platinesRule = new SheetExtractionRule(
                "PLATINES", platinesLocator, [], ["POSE ÉTIQUETTES"], [], [],
                couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1),
                allowedCouleursEtiquette: ["ROUGE", "BLEUE", "JAUNE"]);
            var isolementLocator = new RepeatingBlockLocator(
                "ISOLEMENT", 19, 7, "Identification", [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
            var isolementRule = new SheetExtractionRule("ISOLEMENT", isolementLocator, [], [], [], []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [platinesRule, isolementRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var cards = cut.FindAll(".sheet-rule-card");
            cards.Should().HaveCount(2);

            cards[0].OuterHtml.Should().Contain("ROUGE, BLEUE, JAUNE");
            cards[1].OuterHtml.Should().NotContain("ROUGE, BLEUE, JAUNE");
        });

    [Fact]
    public void AddSheetRuleForm_FillingOnlyAllowedCouleursEtiquette_MarksProfileAsChanged() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        OpenAddSheetRuleFormIfClosed(cut);

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#sheet-rule-allowed-couleurs-etiquette-input").Change("ROUGE, BLEUE, JAUNE");

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
    });
}
