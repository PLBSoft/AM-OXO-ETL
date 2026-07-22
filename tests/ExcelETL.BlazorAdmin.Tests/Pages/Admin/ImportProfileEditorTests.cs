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

        return new ImportProfile(name, equipementTypeElementNom, [sheetRule]);
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

        return new ImportProfile(name, equipementTypeElementNom, [sheetRule]);
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

        return new ImportProfile(name, equipementTypeElementNom, [isolementRule, platinesRule]);
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
        cut.Markup.Should().Contain("PROLOCK VANNES");
        cut.Markup.Should().Contain("ZÉRO ENERGIE...");
        cut.Markup.Should().Contain("TypeElement");
        cut.Find("#sheet-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();
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
            cut.Markup.Should().Contain("PROLOCK VANNES");
            cut.Markup.Should().Contain("Identification");
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
            cut.Markup.Should().Contain("Identification (B9:E9)");
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
            var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Markup.Should().Contain("ZÉRO ENERGIE...");
            cut.Markup.Should().Contain("TypeElement");
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

    [Fact]
    public async Task Delete_RemovesTheRuleFromTheInMemoryList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-rule-button-0").Click();

            cut.Markup.Should().NotContain("ISOLEMENT");
            cut.Markup.Should().Contain("PLATINES");
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
        cut.Markup.Should().Contain("Identification (C9:F9)");
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

        cut.Markup.Should().Contain("Identification (B9:E9)");
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

        cut.Markup.Should().NotContain("Identification (B9:E9)");
        cut.Markup.Should().Contain("Designation (H9:U9)");
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
}
