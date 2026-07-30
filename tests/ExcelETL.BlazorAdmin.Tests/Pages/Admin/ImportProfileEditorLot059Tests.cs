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

// Lot 059 (59.4): #save-profile-button is disabled if and only if _hasUnsavedChanges is false -- a
// flag already exact since Lot 056.3, only consumed here, not reconstructed. Kept in its own file per
// this project's established convention (mirrors ImportProfileEditorLot056Tests.cs).
public class ImportProfileEditorLot059Tests : BunitContext
{
    public ImportProfileEditorLot059Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot059Tests_" + Guid.NewGuid());
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

    [Fact]
    public async Task EditRoute_NoInteraction_SaveButtonIsDisabled_WithNoChangesTitle() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var button = cut.Find("#save-profile-button");
            button.HasAttribute("disabled").Should().BeTrue();
            button.GetAttribute("title").Should().NotBeNullOrEmpty();
        });

    [Fact]
    public async Task EditRoute_AfterModifyingName_SaveButtonIsEnabled_WithShortcutTitle() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#profile-name-input").Change("MAD OXO renamed");

            var button = cut.Find("#save-profile-button");
            button.HasAttribute("disabled").Should().BeFalse();
            button.GetAttribute("title").Should().Contain("Ctrl+Enter");
        });

    [Fact]
    public async Task EditRoute_AfterModifyingFieldInsideAnOpenSheetRuleForm_WithoutSubmitting_SaveButtonIsEnabled() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-stop-field-name-input").Change("Autre");

            cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
        });

    [Fact]
    public async Task EditRoute_OpeningSheetRuleFormWithoutModifyingAnything_SaveButtonStaysDisabled() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeTrue();
        });

    [Fact]
    public async Task CtrlEnter_WithoutPendingChanges_NeverCallsSaveAsync() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = true,
            });

            (await Store.GetAllAsync()).Should().ContainSingle(p => p.Name == "MAD OXO");
        });

    [Fact]
    public async Task CtrlEnter_WithPendingChanges_CallsSaveAsyncOnce() =>
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
    public void NewRoute_OnLoad_SaveButtonIsDisabled_ThenEnabledAfterTypingName() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#profile-name-input").Change("Profil OXO standard");

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
    });

    // ------------------------------------------------------------------------------------------
    // 59.6: toggle-add-sheet-rule-form-button now shares the gabarit of every other "Add" button.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AddSheetRuleToggle_CarriesBtnSecondaryAndW100_NeitherBtnSmNorOutline() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            // Edit route: toggle starts closed (None), per Lot 057's own default.
            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var toggle = cut.Find("#toggle-add-sheet-rule-form-button");
            toggle.ClassList.Should().Contain("btn-secondary");
            toggle.ClassList.Should().Contain("w-100");
            toggle.ClassList.Should().NotContain("btn-sm");
            toggle.ClassList.Should().NotContain("btn-outline-secondary");
        });

    [Fact]
    public async Task AddSheetRuleToggle_CssClass_IsIdenticalWhetherOpenOrClosed() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var closedClass = cut.Find("#toggle-add-sheet-rule-form-button").GetAttribute("class");

            cut.Find("#toggle-add-sheet-rule-form-button").Click();
            var openClass = cut.Find("#toggle-add-sheet-rule-form-button").GetAttribute("class");

            openClass.Should().Be(closedClass);
        });

    // ------------------------------------------------------------------------------------------
    // 59.3: Tableaux/Applications side by side above 768px.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void TableauxAndApplications_BlockContainers_CarryColMd6_AndAreDirectChildrenOfSameRow() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            var tableauColumn = cut.Find("#default-tableau-name-input").Closest("div.col-12.col-md-6")!;
            var applicationColumn = cut.Find("#default-application-name-input").Closest("div.col-12.col-md-6")!;

            tableauColumn.GetAttribute("class").Should().Be("col-12 col-md-6");
            applicationColumn.GetAttribute("class").Should().Be("col-12 col-md-6");
            tableauColumn.ParentElement.Should().Be(applicationColumn.ParentElement);
            tableauColumn.ParentElement!.ClassList.Should().Contain("row");
        });

    [Fact]
    public void TableauxAndApplications_Heading_IsDescendantOfItsOwnColumn() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        var tableauColumn = cut.Find("#default-tableau-name-input").Closest("div.col-12.col-md-6")!;
        var applicationColumn = cut.Find("#default-application-name-input").Closest("div.col-12.col-md-6")!;

        tableauColumn.QuerySelectorAll("h2").Should().ContainSingle();
        applicationColumn.QuerySelectorAll("h2").Should().ContainSingle();
    });

    [Fact]
    public void TableauxAndApplications_BlockContainers_NeverCarryColMd6WithoutCol12() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            foreach (var column in cut.FindAll(".row.g-3 > .col-md-6"))
            {
                column.ClassList.Should().Contain("col-12");
            }
        });
}
