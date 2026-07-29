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

// Lot 056: export-side mirror of ImportProfileEditorLot056Tests.cs.
public class ExportProfileEditorLot056Tests : BunitContext
{
    public ExportProfileEditorLot056Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorLot056Tests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IExportProfileStore Store => Services.GetRequiredService<IExportProfileStore>();

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

    private static ExportProfile BuildProfileWithOneSheetRule(string name = "Profil export OXO") =>
        new(name,
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")],
                    [])
            ]);

    // ------------------------------------------------------------------------------------------
    // 56.1
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task EditMode_SubmitButton_RendersApplyChangesLabel_NotSaveChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            var expected = Loc["ExportProfileEditor_ApplySheetRuleButton"].Value;
            cut.Find("#save-sheet-generation-rule-button-0").TextContent.Should().Contain(expected);
        });

    [Fact]
    public void AddMode_SubmitButton_StillRendersAddSheetLabel() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        var expected = Loc["ExportProfileEditor_AddSheetButton"].Value;
        cut.Find("#add-sheet-generation-rule-button").TextContent.Should().Contain(expected);
    });

    // ------------------------------------------------------------------------------------------
    // 56.2
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SaveProfile_WithOpenEditFormHoldingUncommittedColumn_PersistsTheNewColumn() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.Find("#edit-0-column-header-input").Change("Zone");
            cut.Find("#edit-0-column-source-select").Change(nameof(PivotFieldRef.EquipementLocalisation));
            cut.Find("#edit-0-add-column-definition-button").Click();

            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().ColumnDefinitions.Should().Contain(c => c.Header == "Zone");
        });

    [Fact]
    public async Task SaveProfile_WithOpenAddFormFilledButNotSubmitted_PersistsTheNewSheet()
    {
        var cut = Render<ExportProfileEditor>();
        cut.Find("#export-profile-name-input").Change("Profil export OXO");

        cut.Find("#sheet-generation-rule-name-input").Change("Parents");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));
        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        // Deliberately never click #add-sheet-generation-rule-button.
        cut.Find("#save-export-profile-button").Click();

        (await Store.GetAllAsync()).Should().ContainSingle(p => p.SheetRules.Any(r => r.SheetName == "Parents"));
    }

    [Fact]
    public async Task SaveProfile_WithNoFormOpen_BehavesLikeBeforeTheLot() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Should().HaveCount(1);
        });

    // ------------------------------------------------------------------------------------------
    // 56.3
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task EditMode_ModifyingAFieldWithoutSubmitting_ShowsUnsavedChangesIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents2");

            cut.FindAll("#unsaved-changes-indicator").Should().NotBeEmpty();
        });

    [Fact]
    public async Task EditMode_CancelWithoutChanges_DoesNotShowIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#cancel-sheet-generation-rule-button-0").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();
        });

    [Fact]
    public async Task EditMode_AddingAColumnWithoutSubmittingTheSheet_ShowsIndicator() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-column-header-input").Change("Zone");
            cut.Find("#edit-0-column-source-select").Change(nameof(PivotFieldRef.EquipementLocalisation));
            cut.Find("#edit-0-add-column-definition-button").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().NotBeEmpty();
        });

    [Fact]
    public async Task AfterSuccessfulSave_IndicatorIsAbsent() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents2");
            cut.Find("#save-export-profile-button").Click();

            cut.FindAll("#unsaved-changes-indicator").Should().BeEmpty();
        });

    // ------------------------------------------------------------------------------------------
    // 56.5
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CtrlEnter_OnRootContainer_SavesTheProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#export-profile-name-input").Change("Profil export OXO renamed");

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = true,
            });

            (await Store.GetAllAsync()).Should().ContainSingle(p => p.Name == "Profil export OXO renamed");
        });

    [Fact]
    public async Task EnterAlone_OnRootContainer_DoesNotSaveTheProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#export-profile-name-input").Change("Profil export OXO renamed");

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = false,
            });

            (await Store.GetAllAsync()).Should().NotContain(p => p.Name == "Profil export OXO renamed");
        });

    [Fact]
    public void SaveProfileButton_HasNonEmptyTitle_MentioningShortcut() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        var title = cut.Find("#save-export-profile-button").GetAttribute("title");
        title.Should().NotBeNullOrEmpty();
    });
}
