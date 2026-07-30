using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 059 (59.4): export-side mirror of ImportProfileEditorLot059Tests.cs.
public class ExportProfileEditorLot059Tests : BunitContext
{
    public ExportProfileEditorLot059Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorLot059Tests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IExportProfileStore Store => Services.GetRequiredService<IExportProfileStore>();

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

    [Fact]
    public async Task EditRoute_NoInteraction_SaveButtonIsDisabled_WithNoChangesTitle() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var button = cut.Find("#save-export-profile-button");
            button.HasAttribute("disabled").Should().BeTrue();
            button.GetAttribute("title").Should().NotBeNullOrEmpty();
        });

    [Fact]
    public async Task EditRoute_AfterModifyingName_SaveButtonIsEnabled_WithShortcutTitle() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#export-profile-name-input").Change("Profil export OXO renamed");

            var button = cut.Find("#save-export-profile-button");
            button.HasAttribute("disabled").Should().BeFalse();
            button.GetAttribute("title").Should().Contain("Ctrl+Enter");
        });

    [Fact]
    public async Task EditRoute_AfterModifyingFieldInsideAnOpenSheetRuleForm_WithoutSubmitting_SaveButtonIsEnabled() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Enfants");

            cut.Find("#save-export-profile-button").HasAttribute("disabled").Should().BeFalse();
        });

    [Fact]
    public async Task EditRoute_OpeningSheetRuleFormWithoutModifyingAnything_SaveButtonStaysDisabled() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.Find("#save-export-profile-button").HasAttribute("disabled").Should().BeTrue();
        });

    [Fact]
    public async Task CtrlEnter_WithoutPendingChanges_NeverCallsSaveAsync() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find(".profile-editor-container").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs
            {
                Key = "Enter",
                CtrlKey = true,
            });

            (await Store.GetAllAsync()).Should().ContainSingle(p => p.Name == "Profil export OXO");
        });

    [Fact]
    public async Task CtrlEnter_WithPendingChanges_CallsSaveAsyncOnce() =>
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
    public void NewRoute_OnLoad_SaveButtonIsDisabled_ThenEnabledAfterTypingName() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#save-export-profile-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#export-profile-name-input").Change("Profil export OXO standard");

        cut.Find("#save-export-profile-button").HasAttribute("disabled").Should().BeFalse();
    });

    // ------------------------------------------------------------------------------------------
    // 59.6: toggle-add-sheet-generation-rule-form-button matches the add-button gabarit.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AddSheetRuleToggle_CarriesBtnSecondaryAndW100_NeitherBtnSmNorOutline() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var toggle = cut.Find("#toggle-add-sheet-generation-rule-form-button");
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

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var closedClass = cut.Find("#toggle-add-sheet-generation-rule-form-button").GetAttribute("class");

            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            var openClass = cut.Find("#toggle-add-sheet-generation-rule-form-button").GetAttribute("class");

            openClass.Should().Be(closedClass);
        });
}
