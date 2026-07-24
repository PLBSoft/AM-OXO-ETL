using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Tests.Layout;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class ImportProfileEditorTests : BunitContext
{
    public ImportProfileEditorTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static ImportProfile BuildProfileWithOneSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"]);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    // Mirrors the real ISOLEMENT sheet's client-reported example (ticket N0): FirstBlockStartRow=19,
    // Identification RowOffsetStart=0/End=1 (-> B19:E20), TypeElement RowOffsetStart=3/End=4 (-> B22:E23).
    private static ImportProfile BuildProfileWithIsolementSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 19,
            step: 7,
            stopFieldName: "Identification",
            fields:
            [
                new BlockFieldDefinition("Identification", "B:E", 0, 1),
                new BlockFieldDefinition("TypeElement", "B:E", 3, 4)
            ]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"]);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    private static ImportProfile BuildProfileWithTwoSheetRules(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var isolementLocator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var isolementRule = new SheetExtractionRule(
            "ISOLEMENT", isolementLocator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"]);

        var platinesLocator = new RepeatingBlockLocator(
            "PLATINES",
            firstBlockStartRow: 17,
            step: 8,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var platinesRule = new SheetExtractionRule(
            "PLATINES", platinesLocator, pointRules: [], unconditionalColonneNames: ["TROU D'HOMME"]);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [isolementRule, platinesRule]);
    }

    // Fills every field needed to pass the RepeatingBlockLocator/SheetExtractionRule build-up
    // (one block field, one unconditional colonne, one conditional point rule) and clicks
    // "add sheet rule", leaving the render handle positioned after the add.
    private static void AddValidSheetRule(IRenderedComponent<ImportProfileEditor> cut)
    {
        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("7");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
        cut.Find("#add-unconditional-colonne-button").Click();

        cut.Find("#point-rule-colonne-name-input").Change("ZÉRO ENERGIE...");
        cut.Find("#point-rule-source-field-name-input").Change("TypeElement");
        cut.Find("#point-rule-comparison-value-input").Change("ZERO ENERGIE");
        cut.Find("#add-point-rule-button").Click();

        cut.Find("#add-sheet-rule-button").Click();
    }

    [Fact]
    public void NewProfile_PrefillsReperePrefixWithDefault_AndLeavesEquipementTypeElementNomEmpty() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#profile-repere-prefix-input").GetAttribute("value").Should().Be(ImportProfile.DefaultReperePrefix);
            cut.Find("#profile-equipement-type-element-nom-input").GetAttribute("value").Should().BeNullOrEmpty();
        });

    [Fact]
    public async Task Save_WithEmptyName_DisplaysLocalizedErrorAndDoesNotPersist() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#save-profile-button").Click();

            cut.Markup.Should().Contain("Name must not be empty.");

            var all = await Store.GetAllAsync();
            all.Should().BeEmpty();
        });

    [Fact]
    public void Save_WithEmptyReperePrefix_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-repere-prefix-input").Change(string.Empty);

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Repere prefix must not be empty.");
    });

    [Fact]
    public void Save_WithEmptyEquipementTypeElementNom_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Equipement type element nom must not be empty.");
    });

    [Fact]
    public void Save_WithNoSheetRulesAdded_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Sheet rules must contain at least one rule.");
    });

    [Fact]
    public void AddSheetRule_WithValidInput_DisplaysSheetSummaryAndResetsForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        AddValidSheetRule(cut);

        cut.Markup.Should().Contain("ISOLEMENT");
        cut.Find("#sheet-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();

        // Lot R3: unconditional colonnes/conditional point rules are collapsed by default.
        cut.Markup.Should().NotContain("PROLOCK VANNES");
        cut.Find("#sheet-rule-details-toggle-0").Click();
        cut.Markup.Should().Contain("PROLOCK VANNES");
        cut.Markup.Should().Contain("ZÉRO ENERGIE...");
        cut.Markup.Should().Contain("TypeElement");
    });

    [Fact]
    public void AddSheetRule_WithNonPositiveStep_DisplaysLocalizedErrorAndDoesNotAddSheet() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("0");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#add-sheet-rule-button").Click();

        cut.Markup.Should().Contain("Step must be positive.");
        cut.Markup.Should().Contain("No sheet rules added yet.");
    });

    [Fact]
    public async Task Save_WithValidRootFieldsAndOneAddedSheetRule_PersistsProfileAndNavigatesToList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            AddValidSheetRule(cut);

            cut.Find("#save-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/import-profiles");

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Name.Should().Be("MAD OXO");
            saved.EquipementTypeElementNom.Should().Be("MAD TRAVAUX");
            saved.ReperePrefix.Should().Be(ImportProfile.DefaultReperePrefix);
            saved.SheetRules.Should().ContainSingle();

            var rule = saved.SheetRules.Single();
            rule.SheetName.Should().Be("ISOLEMENT");
            rule.Locator.FirstBlockStartRow.Should().Be(9);
            rule.Locator.Step.Should().Be(7);
            rule.Locator.StopFieldName.Should().Be("Identification");
            rule.Locator.Fields.Should().ContainSingle(f => f.Name == "Identification" && f.ColumnRange == "B:E");
            rule.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES");
            rule.PointRules.Should().ContainSingle(r => r.ColonneName == "ZÉRO ENERGIE..." && r.ComparisonValue == "ZERO ENERGIE");
        });

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public async Task EditRoute_WithExistingProfile_PrefillsRootFieldsAndSheetRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#profile-name-input").GetAttribute("value").Should().Be("MAD OXO");
            cut.Find("#profile-repere-prefix-input").GetAttribute("value").Should().Be(ImportProfile.DefaultReperePrefix);
            cut.Find("#profile-equipement-type-element-nom-input").GetAttribute("value").Should().Be("MAD TRAVAUX");
            cut.Markup.Should().Contain("ISOLEMENT");
            cut.Markup.Should().Contain("Identification");

            // Lot R3: unconditional colonnes are collapsed by default.
            cut.Markup.Should().NotContain("PROLOCK VANNES");
            cut.Find("#sheet-rule-details-toggle-0").Click();
            cut.Markup.Should().Contain("PROLOCK VANNES");
        });

    [Fact]
    public async Task EditRoute_SaveAfterModification_UsesSameProfileId() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-V2-");
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Id.Should().Be(profile.Id);
            saved.ReperePrefix.Should().Be("MAD-OXO-V2-");
        });

    [Fact]
    public void EditRoute_WithUnknownId_DisplaysErrorAndDoesNotRenderForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, Guid.NewGuid()));

        cut.Markup.Should().Contain("Import profile not found.");
        cut.FindAll("#profile-name-input").Should().BeEmpty();
        cut.FindAll("#save-profile-button").Should().BeEmpty();
    });

    [Fact]
    public void RootFields_HaveVisibleLabels_AssociatedByForAttribute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='profile-name-input']").TextContent.Should().Be("Profile name");
        cut.Find("label[for='profile-repere-prefix-input']").TextContent.Should().Be("Repere prefix");
        cut.Find("label[for='profile-equipement-type-element-nom-input']").TextContent.Should().Be("Equipement type element name");
    });

    [Fact]
    public async Task ExistingSheetRule_DisplaysModifyButton() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#modify-sheet-rule-button-0").Should().HaveCount(1);
        });

    [Fact]
    public async Task ClickingModify_SwitchesOnlyThatRuleIntoEditMode() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-sheet-rule-name-input").GetAttribute("value").Should().Be("ISOLEMENT");
            cut.FindAll("#modify-sheet-rule-button-1").Should().HaveCount(1);
            cut.FindAll("#edit-1-sheet-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public async Task EditMode_PrefillsRootLocatorFieldsWithExistingRuleValues() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-sheet-rule-name-input").GetAttribute("value").Should().Be("ISOLEMENT");
            cut.Find("#edit-0-sheet-rule-first-block-start-row-input").GetAttribute("value").Should().Be("9");
            cut.Find("#edit-0-sheet-rule-step-input").GetAttribute("value").Should().Be("7");
            cut.Find("#edit-0-sheet-rule-stop-field-name-input").GetAttribute("value").Should().Be("Identification");
            cut.Find(".block-field-name").TextContent.Should().Be("Identification");
            cut.Find(".block-field-range").TextContent.Should().Be("B9:E9");
            cut.Markup.Should().Contain("PROLOCK VANNES");
        });

    [Fact]
    public async Task ExistingSheetRule_DisplaysAbsoluteExcelRanges_NotRawRowOffsets() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Markup.Should().Contain("B19:E20");
            cut.Markup.Should().Contain("B22:E23");
            cut.Markup.Should().NotContain("0-1");
            cut.Markup.Should().NotContain("3-4");
        });

    // Client feedback (screenshot, 2026-07-22): the read-only sheet-rule summary's field ranges
    // must render with the same name/range two-level, monospace-range styling as the edit form
    // (SheetRuleForm's own block-field list) -- not the plain "Name (Range)" inline text it had
    // before, since the two were visually inconsistent side by side.
    [Fact]
    public async Task Summary_DisplaysFieldNameAndRangeAsSeparateElements_WithMonospaceRangeClass_LikeEditMode() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var names = cut.FindAll(".block-field-name");
            var ranges = cut.FindAll(".block-field-range");

            names.Should().Contain(e => e.TextContent == "Identification");
            names.Should().Contain(e => e.TextContent == "TypeElement");
            ranges.Should().Contain(e => e.TextContent == "B19:E20");
            ranges.Should().Contain(e => e.TextContent == "B22:E23");

            foreach (var range in ranges)
            {
                range.ClassList.Should().Contain("font-monospace");
            }
        });

    [Fact]
    public async Task EditMode_PrefillsConditionalPointRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "ISOLEMENT",
                firstBlockStartRow: 9,
                step: 7,
                stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var pointRule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE...");
            var sheetRule = new SheetExtractionRule(
                "ISOLEMENT", locator, pointRules: [pointRule], unconditionalColonneNames: ["PROLOCK VANNES"]);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ZÉRO ENERGIE...");
            cut.Markup.Should().Contain("TypeElement");
        });

    // Ticket O2: the read-only sheet-rule summary must show the same section labels as the edit
    // form above the unconditional-colonnes / conditional-point-rules lists -- and only when the
    // underlying collection is non-empty.
    [Fact]
    public async Task Summary_WithUnconditionalColonnesAndPointRules_ShowsBothLabelsAndBulletLists() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "ISOLEMENT",
                firstBlockStartRow: 9,
                step: 7,
                stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var pointRule = new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "ZERO ENERGIE", "ZÉRO ENERGIE...");
            var sheetRule = new SheetExtractionRule(
                "ISOLEMENT", locator, pointRules: [pointRule],
                unconditionalColonneNames: ["PROLOCK VANNES", "DEPROLOCK VANNES"]);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            // Scoped to the read-only summary <li>, not the always-visible "Add a sheet rule" card
            // below it -- that card's own SheetRuleForm renders these same two headings unconditionally.
            var summaryItem = cut.Find("li.sheet-rule-card");
            var headings = summaryItem.QuerySelectorAll("h5").Select(h => h.TextContent).ToList();
            headings.Should().Contain("Unconditional colonnes (always create the Point)");
            headings.Should().Contain("Conditional point rules");

            var unconditionalHeading = summaryItem.QuerySelectorAll("h5")
                .Single(h => h.TextContent == "Unconditional colonnes (always create the Point)");
            var unconditionalList = unconditionalHeading.NextElementSibling!;
            unconditionalList.TagName.Should().Be("UL");
            unconditionalList.Children.Select(li => li.TextContent).Should().BeEquivalentTo("PROLOCK VANNES", "DEPROLOCK VANNES");

            var pointRulesHeading = summaryItem.QuerySelectorAll("h5")
                .Single(h => h.TextContent == "Conditional point rules");
            var pointRulesList = pointRulesHeading.NextElementSibling!;
            pointRulesList.TagName.Should().Be("UL");
            pointRulesList.TextContent.Should().Contain("ZÉRO ENERGIE...");
        });

    [Fact]
    public async Task Summary_WithOnlyUnconditionalColonnes_DoesNotShowConditionalPointRulesLabel() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "PLATINES",
                firstBlockStartRow: 17,
                step: 8,
                stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "PLATINES", locator, pointRules: [], unconditionalColonneNames: ["TROU D'HOMME"]);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            var summaryItem = cut.Find("li.sheet-rule-card");
            var headings = summaryItem.QuerySelectorAll("h5").Select(h => h.TextContent).ToList();
            headings.Should().Contain("Unconditional colonnes (always create the Point)");
            headings.Should().NotContain("Conditional point rules");
        });

    [Fact]
    public async Task Summary_WithNeitherCollectionPopulated_ShowsNeitherLabel() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "PROCEDURE",
                firstBlockStartRow: 9,
                step: 1,
                stopFieldName: "Action",
                fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var summaryItem = cut.Find("li.sheet-rule-card");
            summaryItem.QuerySelectorAll("h5").Should().BeEmpty();
        });

    // Ticket P1: each non-editing sheet rule in the summary is wrapped in its own visually
    // distinct card (one container per rule, not a shared list-group border).
    [Fact]
    public async Task Summary_WithMultipleSheetRules_WrapsEachInADistinctCard() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("li.sheet-rule-card").Should().HaveCount(2);
        });

    // Lot R1: the sheet-rule cards' parent <ul> is a responsive CSS grid (auto-fill columns),
    // not a one-card-per-row flex stack -- so more of a 6-sheet profile is visible without
    // scrolling on a wide screen. Asserted on the class attribute only, per the ticket's own
    // instruction (bUnit doesn't compute real layout).
    [Fact]
    public async Task SheetRuleList_HasGridCssClass() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var list = cut.Find("ul.sheet-rule-list");
            list.ClassList.Should().Contain("sheet-rule-grid");

            // No regression on the number of cards or their content.
            cut.FindAll("li.sheet-rule-card").Should().HaveCount(2);
        });

    // Lot R2: the block-field list inside a read-only sheet-rule card is a compact multi-column
    // grid, not one field per full-width row.
    [Fact]
    public async Task BlockFieldList_InSummary_HasGridCssClass() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var fieldList = cut.Find("li.sheet-rule-card ul.block-field-list");
            fieldList.ClassList.Should().Contain("block-field-grid");

            // No regression on field content.
            cut.FindAll(".block-field-name").Select(e => e.TextContent).Should().BeEquivalentTo("Identification", "TypeElement");
        });

    // Lot R3: UnconditionalColonneNames/ConditionalPointRule are collapsed behind a details/summary,
    // closed by default -- the full list must be genuinely absent from the DOM, not just visually
    // hidden (same rule as L2/NavMenu: FindAll empty, not a display:none check).
    [Fact]
    public async Task SheetRuleSublistDetails_CollapsedByDefault_FullListIsAbsentFromDom() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
            cut.Find("li.sheet-rule-card").QuerySelectorAll("h5").Should().BeEmpty();

            var summary = cut.Find("#sheet-rule-details-toggle-0");
            summary.TextContent.Should().Contain("1");
        });

    [Fact]
    public async Task SheetRuleSublistDetails_ClickingSummary_ExpandsFullListWithSameValues() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
            cut.Markup.Should().Contain("PROLOCK VANNES");
        });

    // Ticket R3 (correctif): clicking the summary a second time must collapse the sublist again --
    // no prior test exercised this bidirectional toggle explicitly.
    [Fact]
    public async Task SheetRuleSublistDetails_ClickingSummaryTwice_CollapsesAgain() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var toggle = cut.Find("#sheet-rule-details-toggle-0");

            toggle.Click();
            cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);

            toggle.Click();
            cut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
        });

    // Ticket R3 (correctif): expanding one card's sublist must not affect any other card's state.
    [Fact]
    public async Task SheetRuleSublistDetails_ExpandingOneCard_DoesNotAffectOtherCards() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
            cut.FindAll("#sheet-rule-details-content-1").Should().BeEmpty();
        });

    // Ticket R3 (correctif): a sheet rule with neither unconditional colonnes nor conditional point
    // rules must still expose a working, clickable toggle -- previously the whole <details> block
    // was omitted for this case, so there was nothing to click at all.
    [Fact]
    public async Task SheetRuleSublistDetails_WithEmptySublist_StillRendersToggle() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithEmptySublistSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("0");
        });

    [Fact]
    public async Task SheetRuleSublistDetails_WithEmptySublist_ClickingShowsCoherentEmptyState() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithEmptySublistSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#sheet-rule-details-toggle-0").Click();

            cut.Find("#sheet-rule-details-content-0").TextContent
                .Should().Contain("No unconditional colonnes or conditional point rules for this sheet.");
            cut.Find("li.sheet-rule-card").QuerySelectorAll("h5").Should().BeEmpty();
        });

    // Ticket R3 (correctif), Refactor step: the native <details> element's `open` attribute must
    // reflect the C# expansion state so assistive technology gets correct disclosure semantics --
    // the click handler prevents the browser's own native toggle (@onclick:preventDefault), so
    // without this binding `open` would never be set by anything.
    [Fact]
    public async Task SheetRuleSublistDetails_OpenAttribute_ReflectsExpandedState() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var details = cut.Find("details.sheet-rule-sublist-details");
            details.HasAttribute("open").Should().BeFalse();

            cut.Find("#sheet-rule-details-toggle-0").Click();
            details = cut.Find("details.sheet-rule-sublist-details");
            details.HasAttribute("open").Should().BeTrue();
        });

    private static ImportProfile BuildProfileWithEmptySublistSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "PROCEDURE",
            firstBlockStartRow: 9,
            step: 1,
            stopFieldName: "Action",
            fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    // Ticket's own requirement: the count shown in the (still-collapsed) summary must reflect
    // whatever the list currently holds, not a value captured at first render.
    [Fact]
    public void SheetRuleSublistDetails_SummaryCount_ReflectsCurrentListSize_NotFirstRenderValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B9:E9");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("1");

            // Add a second unconditional colonne via edit mode, save -- summary count must update.
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-unconditional-colonne-name-input").Change("DEPROLOCK VANNES");
            cut.Find("#edit-0-add-unconditional-colonne-button").Click();
            cut.Find("#save-sheet-rule-button-0").Click();

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("2");
        });

    [Fact]
    public async Task Summary_SheetNameAndMetadata_AreSeparateElements() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find(".sheet-rule-card-title").TextContent.Should().Be("ISOLEMENT");
            var meta = cut.Find(".sheet-rule-card-meta").TextContent;
            meta.Should().Contain("9");
            meta.Should().Contain("7");
            meta.Should().Contain("Identification");
            meta.Should().NotContain("ISOLEMENT");
        });

    // Ticket P1's own open question, resolved by reading the code before implementing: a rule
    // being edited is replaced in place by SheetRuleForm, not duplicated -- so it never shows up
    // as a second summary card while its edit panel is open.
    [Fact]
    public async Task EditingRule_IsNotRenderedAsACardOrDuplicatedInTheSummary() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.FindAll("li.sheet-rule-card").Should().HaveCount(1);
            cut.FindAll(".sheet-rule-card-title").Should().ContainSingle(e => e.TextContent == "PLATINES");
            cut.FindAll("#edit-0-sheet-rule-name-input").Should().HaveCount(1);
        });

    // Ticket P2: save-profile-button belongs to the root form, not a sheet-rule card, but is
    // governed by the same convention-ui-blazor-alignement-boutons.md rule.
    [Fact]
    public void SaveProfileButton_IsInRightAlignedContainer() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#save-profile-button").ParentElement!.GetAttribute("class")
            .Should().Contain("right-aligned-actions");
    });

    [Fact]
    public async Task SaveChanges_UpdatesTheRuleInPlace_AndClosesEditMode_WithoutDuplicating() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("9");
            cut.Find("#save-sheet-rule-button-0").Click();

            cut.FindAll("#edit-0-sheet-rule-step-input").Should().BeEmpty();
            cut.FindAll("#modify-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#modify-sheet-rule-button-1").Should().BeEmpty();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var rule = all.Single().SheetRules.Should().ContainSingle().Subject;
            rule.Locator.Step.Should().Be(9);
        });

    [Fact]
    public async Task Cancel_ExitsEditMode_WithoutApplyingChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("999");
            cut.Find("#cancel-sheet-rule-button-0").Click();

            cut.FindAll("#edit-0-sheet-rule-step-input").Should().BeEmpty();

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").GetAttribute("value").Should().Be("7");
        });

    // Client feedback (screenshot, 2026-07-22): deleting a sheet rule (unlike a single block field)
    // discards the whole rule's nested state, so it now requires an explicit confirmation step
    // instead of removing on the first click.
    [Fact]
    public async Task Delete_FirstClick_DoesNotRemoveTheRule_ShowsConfirmationInstead() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ISOLEMENT");
            cut.Markup.Should().Contain("Delete this sheet rule? This cannot be undone.");
            cut.FindAll("#confirm-delete-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#cancel-delete-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#modify-sheet-rule-button-0").Should().BeEmpty();
        });

    [Fact]
    public async Task Delete_Confirm_RemovesTheRuleFromTheInMemoryList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();
            cut.Find("#confirm-delete-sheet-rule-button-0").Click();

            cut.Markup.Should().NotContain("ISOLEMENT");
            cut.Markup.Should().Contain("PLATINES");
        });

    [Fact]
    public async Task Delete_Cancel_KeepsTheRuleAndRestoresTheOriginalButtons() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();
            cut.Find("#cancel-delete-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ISOLEMENT");
            cut.Markup.Should().Contain("PLATINES");
            cut.FindAll("#modify-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#delete-sheet-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#confirm-delete-sheet-rule-button-0").Should().BeEmpty();
        });

    [Fact]
    public async Task SaveProfile_PersistsEditedSheetRule_VisibleAfterReload() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("9");
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            reloaded.Find("#modify-sheet-rule-button-0").Click();

            reloaded.Find("#edit-0-sheet-rule-step-input").GetAttribute("value").Should().Be("9");
        });

    [Fact]
    public async Task AddingNewSheetRule_StillWorks_AfterEditingAnExistingRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("9");
            cut.Find("#save-sheet-rule-button-0").Click();

            AddValidSheetRule(cut);

            cut.FindAll("#modify-sheet-rule-button-1").Should().HaveCount(1);
        });

    [Fact]
    public void SheetRuleForm_RootLocatorFields_HaveVisibleLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='sheet-rule-name-input']").TextContent.Should().Be("Sheet name");
        cut.Find("label[for='sheet-rule-first-block-start-row-input']").TextContent.Should().Be("First block start row");
        cut.Find("label[for='sheet-rule-step-input']").TextContent.Should().Be("Step");
        cut.Find("label[for='sheet-rule-stop-field-name-input']").TextContent.Should().Be("Stop field name");
    });

    // Client feedback (screenshot, 2026-07-22): every input in the "Add a sheet rule" section
    // should have a visible label, not just a placeholder -- covers the field name (BlockFieldForm),
    // the unconditional-colonne name, and the 4 conditional-point-rule inputs.
    [Fact]
    public void SheetRuleForm_RemainingInputs_HaveVisibleLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='block-field-name-input']").TextContent.Should().Be("Field name");
        cut.Find("label[for='unconditional-colonne-name-input']").TextContent.Should().Be("Colonne name");
        cut.Find("label[for='point-rule-colonne-name-input']").TextContent.Should().Be("Colonne name");
        cut.Find("label[for='point-rule-source-field-name-input']").TextContent.Should().Be("Source field name");
        cut.Find("label[for='point-rule-operator-select']").TextContent.Should().Be("Operator");
        cut.Find("label[for='point-rule-comparison-value-input']").TextContent.Should().Be("Comparison value");
    });

    [Fact]
    public async Task SheetRuleForm_EditMode_RootLocatorFields_HaveVisibleLabels() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("label[for='edit-0-sheet-rule-name-input']").TextContent.Should().Be("Sheet name");
            cut.Find("label[for='edit-0-sheet-rule-first-block-start-row-input']").TextContent.Should().Be("First block start row");
            cut.Find("label[for='edit-0-sheet-rule-step-input']").TextContent.Should().Be("Step");
            cut.Find("label[for='edit-0-sheet-rule-stop-field-name-input']").TextContent.Should().Be("Stop field name");
        });

    // Client feedback (screenshot, 2026-07-22): the Save/Cancel (or Add) buttons at the bottom of
    // a sheet-rule form were left-aligned, inconsistent with the right-aligned per-field icon
    // buttons above them -- wrapped in a shared right-aligned container (app.css
    // .right-aligned-actions) for both the "Add a sheet rule" card and edit mode.
    [Fact]
    public void SheetRuleForm_AddModeActionButton_IsInRightAlignedContainer() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#add-sheet-rule-button").ParentElement!.GetAttribute("class")
            .Should().Contain("right-aligned-actions");
    });

    [Fact]
    public async Task SheetRuleForm_EditModeActionButtons_AreInRightAlignedContainer() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").ParentElement!.GetAttribute("class")
                .Should().Contain("right-aligned-actions");
            cut.Find("#cancel-sheet-rule-button-0").ParentElement!.GetAttribute("class")
                .Should().Contain("right-aligned-actions");
        });

    [Fact]
    public void BlockField_AfterAdding_DisplaysModifyAndDeleteButtons() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.FindAll("#modify-block-field-button-0").Should().HaveCount(1);
        cut.FindAll("#delete-block-field-button-0").Should().HaveCount(1);
    });

    [Fact]
    public void BlockFieldForm_AbsoluteRangeInput_HasVisibleLabel() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='block-field-absolute-range-input']").TextContent.Should().Be("Excel range of the 1st block");
    });

    // Ticket example (N3): editing TypeElement (FirstBlockStartRow=19, RowOffsetStart=3/End=4) must
    // prefill the text field with "B22:E23", not the raw offsets.
    [Fact]
    public void BlockField_ClickingModify_PrefillsEditFormWithAbsoluteExcelRange() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
        cut.Find("#block-field-name-input").Change("TypeElement");
        cut.Find("#block-field-absolute-range-input").Change("B22:E23");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").Click();

        cut.Find("#block-field-0-name-input").GetAttribute("value").Should().Be("TypeElement");
        cut.Find("#block-field-0-absolute-range-input").GetAttribute("value").Should().Be("B22:E23");
    });

    [Fact]
    public void BlockField_SaveChanges_UpdatesFieldInPlace_AndClosesEditMode() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").Click();
        cut.Find("#block-field-0-absolute-range-input").Change("C9:F9");
        cut.Find("#save-block-field-button-0").Click();

        cut.FindAll("#block-field-0-name-input").Should().BeEmpty();
        cut.Find(".block-field-name").TextContent.Should().Be("Identification");
        cut.Find(".block-field-range").TextContent.Should().Be("C9:F9");
        cut.FindAll("#modify-block-field-button-1").Should().BeEmpty();
    });

    [Fact]
    public void BlockField_Cancel_DiscardsChanges() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").Click();
        cut.Find("#block-field-0-absolute-range-input").Change("C9:F9");
        cut.Find("#cancel-block-field-button-0").Click();

        cut.Find(".block-field-name").TextContent.Should().Be("Identification");
        cut.Find(".block-field-range").TextContent.Should().Be("B9:E9");
    });

    [Fact]
    public void BlockField_Delete_RemovesFieldFromList() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#block-field-name-input").Change("Designation");
        cut.Find("#block-field-absolute-range-input").Change("H9:U9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#delete-block-field-button-0").Click();

        cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "Designation");
        cut.FindAll(".block-field-range").Should().ContainSingle(e => e.TextContent == "H9:U9");
    });

    [Fact]
    public async Task BlockField_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-modify-block-field-button-0").Click();
            cut.Find("#edit-0-block-field-0-absolute-range-input").Change("C9:F9");
            cut.Find("#edit-0-save-block-field-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var rule = all.Single().SheetRules.Single();
            rule.Locator.Fields.Should().ContainSingle(f => f.Name == "Identification" && f.ColumnRange == "C:F");
        });

    // Ticket example (N3): typing "B19:E20" against FirstBlockStartRow=19 must produce
    // RowOffsetStart=0/RowOffsetEnd=1 on the persisted BlockFieldDefinition.
    [Fact]
    public async Task AddBlockField_WithAbsoluteExcelRange_ComputesCorrectRowOffsets() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

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
            var field = all.Single().SheetRules.Single().Locator.Fields.Single();
            field.ColumnRange.Should().Be("B:E");
            field.RowOffsetStart.Should().Be(0);
            field.RowOffsetEnd.Should().Be(1);
        });

    [Theory]
    [InlineData("abc")]
    [InlineData("E20:B19")]
    public void AddBlockField_WithInvalidAbsoluteRange_DisplaysErrorAndDoesNotCreateField(string invalidRange) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change(invalidRange);
            cut.Find("#add-block-field-button").Click();

            cut.Markup.Should().Contain("Enter a valid Excel range");
            cut.FindAll("#modify-block-field-button-0").Should().BeEmpty();
        });

    [Fact]
    public void AddBlockField_WithRowBeyondRealExcelBounds_DisplaysBlockingErrorAndDoesNotCreateField() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-first-block-start-row-input").Change("1");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B2000000");
            cut.Find("#add-block-field-button").Click();

            cut.Markup.Should().Contain("Enter a valid Excel range");
            cut.FindAll("#modify-block-field-button-0").Should().BeEmpty();
        });

    [Fact]
    public void AddBlockField_BeyondPracticalPlausibilityThreshold_DisplaysWarning_ButStillCreatesField() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#sheet-rule-first-block-start-row-input").Change("1");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("BA1");
            cut.Find("#add-block-field-button").Click();

            cut.Markup.Should().Contain("far beyond the columns/rows");
            cut.FindAll("#modify-block-field-button-0").Should().HaveCount(1);
        });

    // Ticket O1: field name and Excel range must be two distinct elements (not one concatenated
    // string), and the range must carry the monospace styling class -- checked by class, not by a
    // computed font value, per the project's "no selection by text or position" test convention.
    [Fact]
    public async Task BlockField_DisplaysNameAndRangeAsSeparateElements_WithMonospaceRangeClass() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            var names = cut.FindAll(".block-field-name");
            var ranges = cut.FindAll(".block-field-range");

            names.Should().Contain(e => e.TextContent == "Identification");
            names.Should().Contain(e => e.TextContent == "TypeElement");
            ranges.Should().Contain(e => e.TextContent == "B19:E20");
            ranges.Should().Contain(e => e.TextContent == "B22:E23");

            foreach (var range in ranges)
            {
                range.ClassList.Should().Contain("font-monospace");
            }
        });

    [Fact]
    public void BlockField_IconButtons_HaveAriaLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#modify-block-field-button-0").GetAttribute("aria-label").Should().Be("Modify");
        cut.Find("#delete-block-field-button-0").GetAttribute("aria-label").Should().Be("Delete");
    });

    // X11 (Lot X): back link now lives in the shared top-row banner via SectionContent/
    // SectionOutlet -- see ImportProfileTestTests' identical comment/host for the rationale.
    private IRenderedComponent<SectionOutletTestHost> RenderWithBackNavHost(Guid? id = null)
        => Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (RenderFragment)(b =>
            {
                b.OpenComponent<ImportProfileEditor>(0);
                if (id.HasValue)
                {
                    b.AddComponentParameter(1, nameof(ImportProfileEditor.Id), id.Value);
                }

                b.CloseComponent();
            })));

    [Fact]
    public void BackToListButton_NavigatesToProfileList() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-import-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/import-profiles");
    });

    [Fact]
    public void BackToListButton_IsStillShown_WhenProfileNotFound() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost(Guid.NewGuid());

        cut.FindAll("#back-to-import-profiles-button").Should().HaveCount(1);
    });

    [Fact]
    public void BackToListButton_LivesInsideTheSharedTopRow_AlongsideTheBrandLink() => WithCulture("en-US", () =>
    {
        var cut = RenderWithBackNavHost();

        var topRow = cut.Find(".top-row");
        topRow.QuerySelector("#back-to-import-profiles-button").Should().NotBeNull();
        topRow.QuerySelector(".navbar-brand").Should().NotBeNull();
    });

    // Lot W: edit/delete of an already-added UnconditionalColonneName.

    [Fact]
    public void UnconditionalColonne_ClickingModify_ShowsPrefilledEditInput_AndRemovesStaticText() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();

            cut.Find("#unconditional-colonne-edit-input-0").GetAttribute("value").Should().Be("PROLOCK VANNES");
            cut.FindAll(".block-field-name").Should().NotContain(e => e.TextContent == "PROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_SaveChanges_UpdatesValueInPlace_AndClosesEditMode() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();
            cut.Find("#unconditional-colonne-edit-input-0").Change("DEPROLOCK VANNES");
            cut.Find("#save-unconditional-colonne-button-0").Click();

            cut.FindAll("#unconditional-colonne-edit-input-0").Should().BeEmpty();
            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "DEPROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_SaveWithEmptyValue_ShowsError_AndKeepsEditModeOpen() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();
            cut.Find("#unconditional-colonne-edit-input-0").Change("   ");
            cut.Find("#save-unconditional-colonne-button-0").Click();

            cut.Markup.Should().Contain("Colonne name must not be empty.");
            cut.FindAll("#unconditional-colonne-edit-input-0").Should().HaveCount(1);
        });

    [Fact]
    public void UnconditionalColonne_Cancel_DiscardsChanges_RestoresOriginalValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-0").Click();
            cut.Find("#unconditional-colonne-edit-input-0").Change("DEPROLOCK VANNES");
            cut.Find("#cancel-unconditional-colonne-edit-button-0").Click();

            cut.FindAll("#unconditional-colonne-edit-input-0").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("PROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_EditingOneItem_DoesNotAffectOtherItemInSameList() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();
            cut.Find("#unconditional-colonne-name-input").Change("DEPROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#edit-unconditional-colonne-button-1").Click();

            cut.FindAll("#unconditional-colonne-edit-input-0").Should().BeEmpty();
            cut.Find("#unconditional-colonne-edit-input-1").GetAttribute("value").Should().Be("DEPROLOCK VANNES");
            cut.Find(".block-field-name").TextContent.Should().Be("PROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_Delete_RemovesFromList_WithoutAffectingOtherItems() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();
            cut.Find("#unconditional-colonne-name-input").Change("DEPROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#delete-unconditional-colonne-button-0").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "DEPROLOCK VANNES");
        });

    [Fact]
    public void UnconditionalColonne_DeletingLastRemainingItem_LeavesEmptyListWithNoError() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
            cut.Find("#add-unconditional-colonne-button").Click();

            cut.Find("#delete-unconditional-colonne-button-0").Click();

            cut.FindAll(".block-field-name").Should().BeEmpty();
        });

    [Fact]
    public async Task UnconditionalColonne_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-edit-unconditional-colonne-button-0").Click();
            cut.Find("#edit-0-unconditional-colonne-edit-input-0").Change("DEPROLOCK VANNES");
            cut.Find("#edit-0-save-unconditional-colonne-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().UnconditionalColonneNames.Should().ContainSingle("DEPROLOCK VANNES");
        });

    [Fact]
    public async Task UnconditionalColonne_DeleteWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-delete-unconditional-colonne-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().UnconditionalColonneNames.Should().BeEmpty();
        });

    // Lot W: edit/delete of an already-added ConditionalPointRule.

    private static void AddPointRule(IRenderedComponent<ImportProfileEditor> cut, string colonneName, string sourceFieldName,
        string operatorValue, string comparisonValue)
    {
        cut.Find("#point-rule-colonne-name-input").Change(colonneName);
        cut.Find("#point-rule-source-field-name-input").Change(sourceFieldName);
        cut.Find("#point-rule-operator-select").Change(operatorValue);
        cut.Find("#point-rule-comparison-value-input").Change(comparisonValue);
        cut.Find("#add-point-rule-button").Click();
    }

    [Fact]
    public void ConditionalPointRule_ClickingModify_ShowsPrefilledEditFields_IncludingOperatorSelect() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "NotEquals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();

            cut.Find("#conditional-point-rule-edit-colonne-name-input-0").GetAttribute("value").Should().Be("ZERO ENERGIE");
            cut.Find("#conditional-point-rule-edit-source-field-input-0").GetAttribute("value").Should().Be("TypeElement");
            cut.Find("#conditional-point-rule-edit-operator-select-0").GetAttribute("value").Should().Be("NotEquals");
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").GetAttribute("value").Should().Be("TUBING");
        });

    [Fact]
    public void ConditionalPointRule_SaveChanges_WithOnlyOneFieldModified_UpdatesOnlyThatField() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").Change("TUYAUTERIE");
            cut.Find("#save-conditional-point-rule-button-0").Click();

            cut.FindAll("#conditional-point-rule-edit-colonne-name-input-0").Should().BeEmpty();
            cut.Find(".block-field-name").TextContent.Should().Be("ZERO ENERGIE");
            cut.Find(".block-field-range").TextContent.Should().Be("TypeElement Equals TUYAUTERIE");
        });

    [Theory]
    [InlineData("", "TypeElement", "TUBING")]
    [InlineData("ZERO ENERGIE", "", "TUBING")]
    [InlineData("ZERO ENERGIE", "TypeElement", "")]
    public void ConditionalPointRule_SaveWithAnyFieldEmpty_ShowsError_AndKeepsEditModeOpen(
        string colonneName, string sourceFieldName, string comparisonValue) =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();
            cut.Find("#conditional-point-rule-edit-colonne-name-input-0").Change(colonneName);
            cut.Find("#conditional-point-rule-edit-source-field-input-0").Change(sourceFieldName);
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").Change(comparisonValue);
            cut.Find("#save-conditional-point-rule-button-0").Click();

            cut.FindAll(".alert-danger").Should().HaveCount(1);
            cut.FindAll("#conditional-point-rule-edit-colonne-name-input-0").Should().HaveCount(1);
        });

    [Fact]
    public void ConditionalPointRule_Cancel_DiscardsChanges_RestoresOriginalValues() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#edit-conditional-point-rule-button-0").Click();
            cut.Find("#conditional-point-rule-edit-comparison-value-input-0").Change("TUYAUTERIE");
            cut.Find("#cancel-conditional-point-rule-edit-button-0").Click();

            cut.FindAll("#conditional-point-rule-edit-comparison-value-input-0").Should().BeEmpty();
            cut.Find(".block-field-range").TextContent.Should().Be("TypeElement Equals TUBING");
        });

    [Fact]
    public void ConditionalPointRule_Delete_RemovesFromList_WithoutAffectingOtherItems() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");
            AddPointRule(cut, "SOUPAPE", "TypeElement", "Equals", "SOUPAPE");

            cut.Find("#delete-conditional-point-rule-button-0").Click();

            cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "SOUPAPE");
        });

    [Fact]
    public void ConditionalPointRule_DeletingLastRemainingItem_LeavesEmptyListWithNoError() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();
            AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

            cut.Find("#delete-conditional-point-rule-button-0").Click();

            cut.FindAll(".block-field-name").Should().BeEmpty();
        });

    [Fact]
    public async Task ConditionalPointRule_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "DIVERS", firstBlockStartRow: 9, step: 3, stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "DIVERS", locator, pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "TUBING", "ZERO ENERGIE")],
                unconditionalColonneNames: []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-edit-conditional-point-rule-button-0").Click();
            cut.Find("#edit-0-conditional-point-rule-edit-comparison-value-input-0").Change("TUYAUTERIE");
            cut.Find("#edit-0-save-conditional-point-rule-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().PointRules.Single().ComparisonValue.Should().Be("TUYAUTERIE");
        });

    [Fact]
    public async Task ConditionalPointRule_DeleteWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var locator = new RepeatingBlockLocator(
                "DIVERS", firstBlockStartRow: 9, step: 3, stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var sheetRule = new SheetExtractionRule(
                "DIVERS", locator, pointRules: [new ConditionalPointRule("TypeElement", ConditionOperator.Equals, "TUBING", "ZERO ENERGIE")],
                unconditionalColonneNames: []);
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-delete-conditional-point-rule-button-0").Click();

            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().PointRules.Should().BeEmpty();
        });

    [Fact]
    public void ConditionalPointRule_IconButtons_HaveAriaLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        AddPointRule(cut, "ZERO ENERGIE", "TypeElement", "Equals", "TUBING");

        cut.Find("#edit-conditional-point-rule-button-0").GetAttribute("aria-label").Should().Be("Modify");
        cut.Find("#delete-conditional-point-rule-button-0").GetAttribute("aria-label").Should().Be("Delete");
    });
}
