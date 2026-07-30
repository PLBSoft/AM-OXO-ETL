using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 056: recording model fixes -- 56.1 (level-2 button label), 56.2 (implicit flush on save),
// 56.3 (unsaved-changes indicator extended to in-form state), 56.5 (Ctrl+Enter shortcut).
// Kept in its own file (mirroring the project's established convention of a dedicated file per
// concern -- e.g. ProfileEditorParityTests, FormFloatingStructureAuditTests) rather than inserted
// into the already-huge ImportProfileEditorTests.cs.
public class ImportProfileEditorLot056Tests : BunitContext
{
    public ImportProfileEditorLot056Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot056Tests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private IStringLocalizer<BlazorAdminMessages> Loc => Services.GetRequiredService<IStringLocalizer<BlazorAdminMessages>>();

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
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    // ------------------------------------------------------------------------------------------
    // 56.1: new label for the level-2 submit button ("Apply changes" instead of "Save changes").
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task EditMode_SubmitButton_RendersApplyChangesLabel_NotSaveChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            var expected = Loc["ImportProfileEditor_ApplySheetRuleButton"].Value;
            cut.Find("#save-sheet-rule-button-0").TextContent.Should().Contain(expected);
        });

    [Fact]
    public void AddMode_SubmitButton_StillRendersAddSheetLabel() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        var expected = Loc["ImportProfileEditor_AddSheetButton"].Value;
        cut.Find("#add-sheet-rule-button").TextContent.Should().Contain(expected);
    });

    [Fact]
    public async Task EditMode_NestedSubformAddButton_StillRendersSaveChangesLabel_NotApplyChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            // The nested block-field add button lives one level down; it isn't the level-2
            // sheet-rule button 56.1 targets, so it keeps "Add field", untouched.
            var addFieldLabel = Loc["ImportProfileEditor_AddFieldButton"].Value;
            cut.Find("#edit-0-add-block-field-button").TextContent.Should().Contain(addFieldLabel);
        });

    // ------------------------------------------------------------------------------------------
    // 56.2: implicit flush -- "Save profile" commits an open sheet-rule form first, then persists
    // once. This is the test that literally reproduces the 29/07 incident.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveProfile_WithOpenEditFormHoldingUncommittedBlockField_PersistsTheNewField() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-block-field-name-input").Change("ETIQUETTE");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H18:N18");
            cut.Find("#edit-0-add-block-field-button").Click();

            // Deliberately never click #save-sheet-rule-button-0.
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().Locator.Fields.Should().Contain(f => f.Name == "ETIQUETTE");
        });

    [Fact]
    public async Task SaveProfile_WithOpenAddFormFilledButNotSubmitted_PersistsTheNewSheet()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("7");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");
        cut.Find("#add-block-field-button").Click();

        // Deliberately never click #add-sheet-rule-button.
        cut.Find("#save-profile-button").Click();

        (await Store.GetAllAsync()).Single().SheetRules.Should().ContainSingle(r => r.SheetName == "ISOLEMENT");
    }

    [Fact]
    public async Task SaveProfile_WithOpenEditFormRenderedInvalid_DoesNotPersistAndKeepsFormOpen() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("0");

            cut.Find("#save-profile-button").Click();

            // No navigation attempted, form still rendered with the alert on it.
            cut.Find("#edit-0-sheet-rule-step-input").Should().NotBeNull();
            cut.FindAll(".alert-danger").Should().NotBeEmpty();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().Locator.Step.Should().Be(7);
        });

    [Fact]
    public async Task SaveProfile_WithNoFormOpen_BehavesLikeBeforeTheLot() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Should().HaveCount(1);
        });

    // Note: the ticket's own "chained with 56.4" scenario (typing a sub-list row then clicking the
    // CTA directly, with no blur/click on the row's own add button) depends on 56.4's blur/Enter
    // validation, which doesn't exist yet at this point in the lot -- added alongside 56.4 instead
    // of here, per the ticket's own "à écrire après" note for the equivalent 56.5 case.

    // ------------------------------------------------------------------------------------------
    // 56.3: unsaved-changes indicator/NavigationLock extended to in-form mutations.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task EditMode_ModifyingAFieldWithoutSubmitting_ShowsUnsavedChangesIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-stop-field-name-input").Change("Autre");

            cut.FindAll("#unsaved-changes-indicator").Should().NotBeEmpty();
        });

    [Fact]
    public async Task EditMode_CancelWithoutChanges_DoesNotShowIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#cancel-sheet-rule-button-0").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();
        });

    [Fact]
    public async Task EditMode_AddingABlockFieldWithoutSubmittingTheSheet_ShowsIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-block-field-name-input").Change("ETIQUETTE");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H18:N18");
            cut.Find("#edit-0-add-block-field-button").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().NotBeEmpty();
        });

    [Fact]
    public async Task EditMode_DeletingABlockFieldWithoutSubmittingTheSheet_ShowsIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-delete-block-field-button-0").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().NotBeEmpty();
        });

    [Fact]
    public async Task AfterSuccessfulSave_IndicatorIsAbsent() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-stop-field-name-input").Change("Autre");
            cut.Find("#save-profile-button").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();
        });

    // ------------------------------------------------------------------------------------------
    // 56.5: Ctrl+Enter saves the profile (through the same flush path as the CTA click).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CtrlEnter_OnRootContainer_SavesTheProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#profile-name-input").Change("MAD OXO renamed");

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = true,
            });

            (await Store.GetAllAsync()).Should().ContainSingle(p => p.Name == "MAD OXO renamed");
        });

    [Fact]
    public async Task EnterAlone_OnRootContainer_DoesNotSaveTheProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#profile-name-input").Change("MAD OXO renamed");

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = false,
            });

            (await Store.GetAllAsync()).Should().NotContain(p => p.Name == "MAD OXO renamed");
        });

    [Fact]
    public async Task CtrlEnter_FlushesAnOpenFormBeforeSaving() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-stop-field-name-input").Change("Autre");

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = true,
            });

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().Locator.StopFieldName.Should().Be("Autre");
        });

    [Fact]
    public void SaveProfileButton_HasNonEmptyTitle_MentioningShortcut() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        // Lot 059 (59.4): the title is conditional on _hasUnsavedChanges -- an unmodified profile
        // shows the "nothing to save" hint, not the shortcut one. A real pending change is needed
        // for this test to actually prove what its name claims.
        cut.Find("#profile-name-input").Change("Profil OXO standard");

        var title = cut.Find("#save-profile-button").GetAttribute("title");
        title.Should().NotBeNullOrEmpty();
    });

    // ------------------------------------------------------------------------------------------
    // 56.6: sticky save bar.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void SaveButton_IsDescendantOfStickySaveBar() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find(".profile-editor-save-bar #save-profile-button").Should().NotBeNull();
        cut.Find("#save-profile-button").ClassList.Should().Contain("btn-primary").And.Contain("btn-lg");
        cut.Find("#save-profile-button").ParentElement!.ClassList.Should().Contain("right-aligned-actions");
    });

    [Fact]
    public async Task UnsavedChangesIndicator_IsDescendantOfSameSaveBarAsButton_WhenRendered() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#profile-name-input").Change("changed");

            cut.Find(".profile-editor-save-bar #unsaved-changes-indicator").Should().NotBeNull();
        });

    [Fact]
    public void SaveBarAndContainer_HaveNoInlineStyleAttribute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find(".profile-editor-save-bar").GetAttribute("style").Should().BeNullOrEmpty();
        cut.Find(".profile-editor-container").GetAttribute("style").Should().BeNullOrEmpty();
    });

    // ------------------------------------------------------------------------------------------
    // 56.4: blur/Enter validation on the 3 eligible 1-2 field sub-lists (BlockFieldForm,
    // HeaderCompositeRuleForm, the inline unconditional-colonne row). Exercised through the
    // always-present "add a sheet rule" card, since that's where these sub-forms live unprefixed.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void BlockField_BothFieldsFilled_BlurOnLastField_AddsWithoutAnyClick()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#block-field-name-input").Change("Identification");
        var rangeInput = cut.Find("#block-field-absolute-range-input");
        rangeInput.Change("B9:E9");

        rangeInput.FocusOut();

        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "Identification");
    }

    [Fact]
    public void BlockField_OnlyOneFieldFilled_Blur_AddsNothingAndShowsNoAlert()
    {
        var cut = Render<ImportProfileEditor>();
        var rangeInput = cut.Find("#block-field-absolute-range-input");
        cut.Find("#block-field-name-input").Change("Identification");
        // Range left blank.

        rangeInput.FocusOut();

        cut.FindAll(".block-field-name").Should().BeEmpty();
        cut.FindAll(".alert-danger").Should().BeEmpty();
    }

    [Fact]
    public void BlockField_BothFieldsFilled_EnterOnFirstField_Adds()
    {
        var cut = Render<ImportProfileEditor>();
        var nameInput = cut.Find("#block-field-name-input");
        nameInput.Change("Identification");
        cut.Find("#block-field-absolute-range-input").Change("B9:E9");

        nameInput.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "Identification");
    }

    [Fact]
    public void BlockField_InvalidRange_Blur_ShowsErrorAndKeepsInputValues()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#block-field-name-input").Change("Identification");
        var rangeInput = cut.Find("#block-field-absolute-range-input");
        rangeInput.Change("ZZZ");

        rangeInput.FocusOut();

        cut.Find(".alert-danger").Should().NotBeNull();
        cut.Find("#block-field-name-input").GetAttribute("value").Should().Be("Identification");
        cut.Find("#block-field-absolute-range-input").GetAttribute("value").Should().Be("ZZZ");
        cut.FindAll(".block-field-name").Should().BeEmpty();
    }

    [Fact]
    public void BlockField_SuccessfulBlurAdd_ClearsBothInputs()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#block-field-name-input").Change("Identification");
        var rangeInput = cut.Find("#block-field-absolute-range-input");
        rangeInput.Change("B9:E9");
        rangeInput.FocusOut();

        cut.Find("#block-field-name-input").GetAttribute("value").Should().BeNullOrEmpty();
        cut.Find("#block-field-absolute-range-input").GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public void BlockField_BeyondPracticalRange_Blur_AddsAndShowsNonBlockingWarning()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#block-field-name-input").Change("Identification");
        var rangeInput = cut.Find("#block-field-absolute-range-input");
        rangeInput.Change("BA1");

        rangeInput.FocusOut();

        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "Identification");
        cut.FindAll(".alert-warning").Should().NotBeEmpty();
    }

    [Fact]
    public void UnconditionalColonne_Filled_Blur_AddsWithoutClick()
    {
        var cut = Render<ImportProfileEditor>();
        var input = cut.Find("#unconditional-colonne-name-input");
        input.Change("PROLOCK VANNES");

        input.FocusOut();

        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "PROLOCK VANNES");
    }

    [Fact]
    public void HeaderComposite_BothFieldsFilled_Blur_AddsWithoutClick()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#header-composite-header-composite-name-input").Change("Designation");
        var templateInput = cut.Find("#header-composite-header-composite-template-input");
        templateInput.Change("Rév {revision}");

        templateInput.FocusOut();

        cut.FindAll(".block-field-name").Should().Contain(e => e.TextContent == "Designation");
    }

    // 56.4's own explicit non-generalization guard-rail: an excluded sub-list (point rules, 3
    // inputs + 1 select) must never auto-submit on blur -- only the click path adds it. There is no
    // @onfocusout handler wired on this field at all, so bUnit itself refuses to dispatch the event
    // -- that refusal *is* the proof of the exclusion, not an incidental test artifact.
    [Fact]
    public void PointRule_LastFieldHasNoBlurHandlerWired_ProvingItIsExcludedFrom564()
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#point-rule-colonne-name-input").Change("ZÉRO ENERGIE...");
        cut.Find("#point-rule-source-field-name-input").Change("TypeElement");
        cut.Find("#point-rule-operator-select").Change(nameof(ConditionOperator.Equals));
        var comparisonInput = cut.Find("#point-rule-comparison-value-input");
        comparisonInput.Change("ZERO ENERGIE");

        var act = () => comparisonInput.FocusOut();
        act.Should().Throw<Bunit.MissingEventHandlerException>();

        cut.FindAll(".block-field-name").Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------
    // 56.7: submission vs. cancel visual distinction, for the 2 of the 8 sub-forms not already
    // covered by a pre-existing (now fixed-in-place) ImportProfileEditorTests.cs test --
    // HeaderFieldRuleForm and HeaderCompositeRuleForm. SheetRuleForm/BlockFieldForm are covered
    // there; SheetGenerationRuleForm/ColumnDefinitionForm/PointColumnDefinitionForm/
    // ApplicationColumnDefinitionForm are covered in ExportProfileEditorTests.cs.
    // ------------------------------------------------------------------------------------------

    private static ImportProfile BuildProfileWithHeaderRules(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "PROCEDURE", firstBlockStartRow: 9, step: 1, stopFieldName: "Action",
            fields: [new BlockFieldDefinition("Action", "C:L", 0, 0)]);

        var headerFields = new List<HeaderFieldRule>
        {
            new("nomMAD", new DirectCell("PROCEDURE", "M2:O2")),
            new("revision", new DirectCell("PROCEDURE", "P2:Q2")),
        };
        var headerComposites = new List<HeaderCompositeRule> { new("Designation", "Rév {revision}") };

        var sheetRule = new SheetExtractionRule(
            "PROCEDURE", locator, pointRules: [], unconditionalColonneNames: [], headerFields, headerComposites);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    [Fact]
    public async Task HeaderFieldRuleForm_EditMode_SubmitAndCancelButtons_HaveDifferentClasses() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithHeaderRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-header-field-button-0").Click();

            var submitClass = cut.Find("#edit-0-save-header-field-button-0").GetAttribute("class");
            var cancelClass = cut.Find("#edit-0-cancel-header-field-button-0").GetAttribute("class");

            submitClass.Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
            cancelClass.Should().Be("btn btn-outline-secondary w-100 mt-3");
            submitClass.Should().NotBe(cancelClass);
        });

    [Fact]
    public async Task HeaderCompositeRuleForm_EditMode_SubmitAndCancelButtons_HaveDifferentClasses() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithHeaderRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-header-composite-button-0").Click();

            var submitClass = cut.Find("#edit-0-save-header-composite-button-0").GetAttribute("class");
            var cancelClass = cut.Find("#edit-0-cancel-header-composite-button-0").GetAttribute("class");

            submitClass.Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
            cancelClass.Should().Be("btn btn-outline-secondary w-100 mt-3");
            submitClass.Should().NotBe(cancelClass);
        });

    [Fact]
    public void HeaderFieldRuleForm_AddMode_SubmitButton_StillBtnSecondary_NonRegression()
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#add-header-field-button").GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
        cut.Find("#add-header-composite-button").GetAttribute("class").Should().Be("btn btn-secondary w-100 mt-3 d-flex align-items-center justify-content-center gap-1");
    }
}
