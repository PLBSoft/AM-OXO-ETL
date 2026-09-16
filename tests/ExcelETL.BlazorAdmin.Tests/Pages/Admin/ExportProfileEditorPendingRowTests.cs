using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 074.3 (docs/tickets/tickets-tdd-lot-074-pilote-brouillon-editeur-profil-export.md), written
// red-first against the pre-migration ExportProfileEditor.razor -- these tests prove constat 4 (L4,
// "defect A" residual leak on the export side: a fully-typed but never-explicitly-"Add"-ed pending
// row is silently dropped on save) and its symmetric counterpart on an already-existing item
// (emptied in place, or modified then cancelled). Both are structurally impossible once the P3
// draft architecture lands (074.2's ExportProfileDraftMapper.ToDomain walks the whole draft tree
// regardless of which sub-form happens to be visually "open").
public class ExportProfileEditorPendingRowTests : BunitContext
{
    public ExportProfileEditorPendingRowTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorPendingRowTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IExportProfileStore Store => Services.GetRequiredService<IExportProfileStore>();

    private IStringLocalizer<BlazorAdminMessages> Loc => Services.GetRequiredService<IStringLocalizer<BlazorAdminMessages>>();

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private static ExportProfile BuildProfileWithOneSheetRule(string name = "Profil export OXO") =>
        new(name,
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [],
                    [])
            ]);

    // ------------------------------------------------------------------------------------------
    // L4: a fully-typed but never-explicitly-"Add"-ed pending row, on each of the 3 sub-lists.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PendingColumnRow_FilledButNotAdded_IsPersistedOnSaveExportProfileButtonClick() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            cut.Find("#column-header-input").Change("Repère");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
            // Deliberately never click #add-column-definition-button.

            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            saved.SheetRules.Should().ContainSingle();
            saved.SheetRules.Single().ColumnDefinitions.Should()
                .ContainSingle(c => c.Header == "Repère" && c.Source == PivotFieldRef.EquipementRepere);
        });

    [Fact]
    public async Task PendingPointColumnRow_FilledButNotAdded_IsPersistedOnSaveExportProfileButtonClick() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));

            cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
            cut.Find("#point-column-header-input").Change("Travaux complet");
            // Deliberately never click #add-point-column-definition-button.

            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            saved.SheetRules.Single().PointColumnDefinitions.Should()
                .ContainSingle(p => p.ColonneNom == "TRAVAUX COMPLET" && p.Header == "Travaux complet");
        });

    [Fact]
    public async Task PendingApplicationColumnRow_FilledButNotAdded_IsPersistedOnSaveExportProfileButtonClick() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            cut.Find("#application-column-nom-input").Change("PROGRESS");
            cut.Find("#application-column-header-input").Change("PROGRESS");
            // Deliberately never click #add-application-column-definition-button.

            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            saved.SheetRules.Single().ApplicationColumnDefinitions.Should()
                .ContainSingle(a => a.ApplicationNom == "PROGRESS" && a.Header == "PROGRESS");
        });

    [Fact]
    public async Task PendingColumnRow_FilledButNotAdded_IsPersistedViaCtrlEnter() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            cut.Find("#column-header-input").Change("Repère");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = true,
            });

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            saved.SheetRules.Single().ColumnDefinitions.Should()
                .ContainSingle(c => c.Header == "Repère" && c.Source == PivotFieldRef.EquipementRepere);
        });

    // ------------------------------------------------------------------------------------------
    // Partial pending row -> blocked save, row-level + global error, input preserved.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PendingPointColumnRow_PartiallyFilled_BlocksSave_ShowsRowAndGlobalError_PreservesInput() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            // Header filled, ColonneNom deliberately left blank.
            cut.Find("#point-column-header-input").Change("Travaux complet");

            cut.Find("#save-export-profile-button").Click();

            (await Store.GetAllAsync()).Should().BeEmpty();
            cut.Find("#point-column-header-input").GetAttribute("value").Should().Be("Travaux complet");

            var globalError = Loc["ExportProfileEditor_PendingRowInvalidError"].Value;
            cut.Markup.Should().Contain(globalError);
        });

    // ------------------------------------------------------------------------------------------
    // The overwhelmingly common case: every pending row left completely empty -> normal save.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PendingRows_LeftCompletelyEmpty_DoesNotBlockNormalSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));
            cut.Find("#column-header-input").Change("Repère");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
            cut.Find("#add-column-definition-button").Click();
            // Point/Application pending rows left untouched.

            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            saved.SheetRules.Single().PointColumnDefinitions.Should().BeEmpty();
            saved.SheetRules.Single().ApplicationColumnDefinitions.Should().BeEmpty();
        });

    // ------------------------------------------------------------------------------------------
    // An already-existing item emptied in place -> blocked save, no silent revert.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExistingColumn_ClearedInPlace_ThenSaveExportProfile_BlocksSave_DoesNotSilentlyRevert() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-column-definition-button-0").Click();
            cut.Find("#edit-0-column-0-header-input").Change(string.Empty);

            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle(p => p.SheetRules.Single().ColumnDefinitions.Single().Header == "Repère");
            cut.Find("#edit-0-column-0-header-input").GetAttribute("value").Should().BeNullOrEmpty();
        });

    // ------------------------------------------------------------------------------------------
    // An already-existing item modified then cancelled -> old value on save.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExistingColumn_ModifiedThenCancelled_ThenSaveExportProfile_KeepsOldValue() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-column-definition-button-0").Click();
            cut.Find("#edit-0-column-0-header-input").Change("Ne sera jamais sauvegardé");
            cut.Find("#edit-0-cancel-column-definition-button-0").Click();

            cut.Find("#save-sheet-generation-rule-button-0").Click();
            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Single().SheetRules.Single().ColumnDefinitions.Single().Header.Should().Be("Repère");
        });
}
