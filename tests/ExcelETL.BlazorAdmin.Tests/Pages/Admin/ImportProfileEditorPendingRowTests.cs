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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 075.3 (docs/tickets/tickets-tdd-lot-075-migration-editeur-import-brouillon.md), written red-first
// against the pre-migration ImportProfileEditor.razor: one test per defect A leak of constat 5 (a typed
// but never explicitly added/confirmed row silently dropped on profile save), plus the blocking and
// "nothing typed" cases. Every leak becomes structurally impossible once the whole draft tree is
// converted on save (ImportProfileDraftMapper.ToDomain), whichever form happens to be open.
public class ImportProfileEditorPendingRowTests : BunitContext
{
    public ImportProfileEditorPendingRowTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorPendingRowTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private IStringLocalizer<BlazorAdminMessages> Loc => Services.GetRequiredService<IStringLocalizer<BlazorAdminMessages>>();

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private async Task<IRenderedComponent<ImportProfileEditor>> RenderExistingProfileAsync()
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT", firstBlockStartRow: 19, step: 7,
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var sheetRule = new SheetExtractionRule("ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: [], [], []);
        var profile = new ImportProfile(
            "MAD OXO", "MAD TRAVAUX", ["TRAVAUX COMPLET"], ["PROGRESS"], [sheetRule],
            [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD")]);
        await Store.SaveAsync(profile);
        return Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
    }

    private async Task<ImportProfile> SingleSavedProfileAsync() => (await Store.GetAllAsync()).Should().ContainSingle().Subject;

    private static void PressCtrlEnter(IRenderedComponent<ImportProfileEditor> cut) =>
        cut.Find(".profile-editor-container").KeyDown(new KeyboardEventArgs { Key = "Enter", CtrlKey = true });

    // ------------------------------------------------------------------------------------------
    // L1/L2 -- root pending rows.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task L1_PendingTableauRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#default-tableau-name-input").Change("TRAVAUX DETAIL");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).DefaultTableaux.Should().Equal("TRAVAUX COMPLET", "TRAVAUX DETAIL");
        });

    [Fact]
    public async Task L1_PendingApplicationRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#default-application-name-input").Change("SAP");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).DefaultApplicationNames.Should().Equal("PROGRESS", "SAP");
        });

    [Fact]
    public async Task L2_PendingTacheMultipleTypeLabelRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_REL");
            cut.Find("#tache-multiple-type-label-label-input").Change("Procédure REL");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).TacheMultipleTypeLabels.Select(l => l.Code).Should().Equal("TM_PROC_MAD", "TM_PROC_REL");
        });

    // ------------------------------------------------------------------------------------------
    // L3 -- root in-line edits left open.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task L3_TableauEditLeftOpen_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change("TRAVAUX RENOMMES");
            // Deliberately never click #save-default-tableau-button-0.

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).DefaultTableaux.Should().Equal("TRAVAUX RENOMMES");
        });

    [Fact]
    public async Task L3_TacheMultipleTypeLabelEditLeftOpen_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#edit-tache-multiple-type-label-button-0").Click();
            cut.Find("#tache-multiple-type-label-edit-label-input-0").Change("Procédure de mise à disposition");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).TacheMultipleTypeLabels.Single().Label.Should().Be("Procédure de mise à disposition");
        });

    // ------------------------------------------------------------------------------------------
    // L4/L5 -- pending rows inside an open sheet rule.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task L4_PendingBlockFieldRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-block-field-name-input").Change("Designation");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H18:U19");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).SheetRules.Single().Locator.Fields.Select(f => f.Name)
                .Should().Equal("Identification", "Designation");
        });

    [Fact]
    public async Task L4_PendingHeaderFieldRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-header-field-header-field-name-input").Change("repereEcho");
            cut.Find("#edit-0-header-field-header-field-range-input").Change("N6");

            cut.Find("#save-profile-button").Click();

            var headerField = (await SingleSavedProfileAsync()).SheetRules.Single().HeaderFields.Should().ContainSingle().Subject;
            headerField.Name.Should().Be("repereEcho");
            headerField.Cell.Sheet.Should().Be("ISOLEMENT");
        });

    [Fact]
    public async Task L4_PendingHeaderCompositeRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-header-composite-header-composite-name-input").Change("Libelle");
            cut.Find("#edit-0-header-composite-header-composite-template-input").Change("Isolement");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).SheetRules.Single().HeaderComposites.Should().ContainSingle(c => c.Name == "Libelle");
        });

    [Fact]
    public async Task L5_PendingPointRuleRow_FilledButNotAdded_IsPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-point-rule-colonne-name-input").Change("POSE ETIQUETTES");
            // Lot 084.1: the source must be a field of the block (only Identification here).
            cut.Find("#edit-0-point-rule-source-field-name-input").Change("Identification");
            cut.Find("#edit-0-point-rule-comparison-value-input").Change("TUBING");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).SheetRules.Single().PointRules
                .Should().ContainSingle(r => r.ColonneName == "POSE ETIQUETTES" && r.ComparisonValue == "TUBING");
        });

    [Fact]
    public async Task L5_PendingUnconditionalColonneRow_FilledButNotAdded_IsPersistedViaCtrlEnter() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#profile-name-input").Change("MAD OXO renommé");
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-unconditional-colonne-name-input").Change("PROLOCK VANNES");

            PressCtrlEnter(cut);

            (await SingleSavedProfileAsync()).SheetRules.Single().UnconditionalColonneNames.Should().Equal("PROLOCK VANNES");
        });

    // ------------------------------------------------------------------------------------------
    // L6 -- an add-sheet-rule form holding only a pending row is not "blank".
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task L6_AddSheetRuleForm_WithOnlyAPendingRowTyped_BlocksSave_InsteadOfSilentlyIgnoringIt() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
            cut.Find("#point-rule-colonne-name-input").Change("POSE ETIQUETTES");

            cut.Find("#save-profile-button").Click();

            cut.Markup.Should().Contain(Loc["ImportProfileEditor_PendingRowInvalidError"].Value);
            Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith("import-profiles");
            cut.Find("#point-rule-colonne-name-input").GetAttribute("value").Should().Be("POSE ETIQUETTES");
        });

    // ------------------------------------------------------------------------------------------
    // Partial pending row -> blocked save, row-level + global error, input preserved.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PendingHeaderFieldRow_PartiallyFilled_BlocksSave_ShowsRowAndGlobalError_PreservesInput() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            // Name filled, range deliberately left blank.
            cut.Find("#edit-0-header-field-header-field-name-input").Change("repereEcho");

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).SheetRules.Single().HeaderFields.Should().BeEmpty();
            Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith("import-profiles");
            cut.Markup.Should().Contain(Loc["ImportProfileEditor_PendingRowInvalidError"].Value);
            cut.Find("#edit-0-header-field-header-field-name-input").GetAttribute("value").Should().Be("repereEcho");
            cut.FindAll(".alert-danger").Should().HaveCountGreaterThanOrEqualTo(2);
        });

    [Fact]
    public async Task PendingRows_LeftCompletelyEmpty_DoNotBlockNormalSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-step-input").Change("8");

            cut.Find("#save-profile-button").Click();

            var saved = await SingleSavedProfileAsync();
            saved.SheetRules.Single().Locator.Step.Should().Be(8);
            saved.SheetRules.Single().Locator.Fields.Should().ContainSingle();
            saved.DefaultTableaux.Should().Equal("TRAVAUX COMPLET");
            Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("import-profiles");
        });

    // ------------------------------------------------------------------------------------------
    // Already-existing items.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExistingTableau_ClearedInPlace_ThenSave_BlocksSave_DoesNotSilentlyRevert() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#edit-default-tableau-button-0").Click();
            cut.Find("#default-tableau-edit-input-0").Change(string.Empty);

            cut.Find("#save-profile-button").Click();

            Services.GetRequiredService<NavigationManager>().Uri.Should().NotEndWith("import-profiles");
            cut.Find("#default-tableau-edit-input-0").GetAttribute("value").Should().BeNullOrEmpty();
            cut.Markup.Should().Contain("Tableau name must not be empty.");
        });

    [Fact]
    public async Task ExistingBlockField_ModifiedThenCancelled_ThenSave_KeepsOldValue() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-modify-block-field-button-0").Click();
            cut.Find("#edit-0-block-field-0-name-input").Change("Ne sera jamais sauvegardé");
            cut.Find("#edit-0-cancel-block-field-button-0").Click();

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).SheetRules.Single().Locator.Fields.Single().Name.Should().Be("Identification");
        });

    // ------------------------------------------------------------------------------------------
    // What a pending row holds is part of the profile: typing in it is a change to save.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task TypingOnlyInAPendingRow_ShowsTheUnsavedChangesIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();

            cut.Find("#default-tableau-name-input").Change("TRAVAUX DETAIL");

            cut.FindAll("#unsaved-changes-indicator").Should().ContainSingle();
            cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
        });

    // ------------------------------------------------------------------------------------------
    // Lot 084.5 -- pending rows of the new shapes.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Lot084_PendingOptionalBlockFieldAndIsNotBlankRule_FilledButNotAdded_AreBothPersistedOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-block-field-name-input").Change("HasDebMad");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H21:N21");
            cut.Find("#edit-0-point-rule-colonne-name-input").Change("RECEPTION DEBUT MAD");
            cut.Find("#edit-0-point-rule-source-field-name-input").Change("HasDebMad");
            cut.Find("#edit-0-point-rule-operator-select").Change("IsNotBlank");

            cut.Find("#save-profile-button").Click();

            var rule = (await SingleSavedProfileAsync()).SheetRules.Single();
            rule.Locator.Fields.Single(f => f.Name == "HasDebMad").IsRequired.Should().BeFalse();
            rule.PointRules.Should().ContainSingle(r =>
                r.SourceFieldName == "HasDebMad" && r.Operator == ConditionOperator.IsNotBlank && r.ComparisonValue == null);
        });

    [Fact]
    public async Task Lot084_PendingRequiredBlockField_FilledButNotAdded_IsPersistedRequired() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-block-field-name-input").Change("TypeElement");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("B22:E23");
            cut.Find("#edit-0-block-field-is-required-checkbox").Change(true);

            cut.Find("#save-profile-button").Click();

            (await SingleSavedProfileAsync()).SheetRules.Single().Locator.Fields.Single(f => f.Name == "TypeElement")
                .IsRequired.Should().BeTrue();
        });
}
