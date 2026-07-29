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

// Lot 057: export-side mirror of ImportProfileEditorLot057Tests.cs (57.1 only in this pass; 57.2
// tests are added alongside its own production code).
public class ExportProfileEditorLot057Tests : BunitContext
{
    public ExportProfileEditorLot057Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorLot057Tests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IExportProfileStore Store => Services.GetRequiredService<IExportProfileStore>();

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
    public async Task EditMode_OnLoad_AddFormFieldsAreAbsent_ToggleButtonPresent() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#sheet-generation-rule-name-input").Should().BeEmpty();
            cut.FindAll("#add-sheet-generation-rule-button").Should().BeEmpty();
            cut.FindAll("#toggle-add-sheet-generation-rule-form-button").Should().HaveCount(1);
        });

    [Fact]
    public async Task EditMode_ClickingToggle_RendersAddFormFields() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();

            cut.FindAll("#sheet-generation-rule-name-input").Should().HaveCount(1);
        });

    [Fact]
    public async Task EditMode_ClickingToggleTwice_HidesAddFormFieldsAgain() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var toggle = cut.Find("#toggle-add-sheet-generation-rule-form-button");
            toggle.Click();
            cut.FindAll("#sheet-generation-rule-name-input").Should().HaveCount(1);

            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            cut.FindAll("#sheet-generation-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public void CreateMode_OnLoad_AddFormFieldsArePresent()
    {
        var cut = Render<ExportProfileEditor>();

        cut.FindAll("#sheet-generation-rule-name-input").Should().HaveCount(1);
    }

    [Fact]
    public async Task EditMode_SuccessfulSubmission_ClosesTheForm() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();

            cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
            cut.Find("#column-header-input").Change("Numéro");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.IsolementRepere));
            cut.Find("#add-column-definition-button").Click();
            cut.Find("#add-sheet-generation-rule-button").Click();

            cut.FindAll("#sheet-generation-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public async Task ReopeningAfterPartialInput_FieldsAreEmpty_ProvingRemount() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var toggle = cut.Find("#toggle-add-sheet-generation-rule-form-button");
            toggle.Click();
            cut.Find("#sheet-generation-rule-name-input").Change("Partial input, never submitted");

            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();

            cut.Find("#sheet-generation-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();
        });

    [Fact]
    public async Task ClosedToggle_HasIconAndNonEmptyLabel_OpenToggle_HasNoIcon() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var closedToggle = cut.Find("#toggle-add-sheet-generation-rule-form-button");
            closedToggle.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            closedToggle.TextContent.Trim().Should().NotBeEmpty();

            closedToggle.Click();

            var openToggle = cut.Find("#toggle-add-sheet-generation-rule-form-button");
            openToggle.QuerySelector("svg").Should().BeNull();
            openToggle.TextContent.Trim().Should().NotBeEmpty();
        });

    [Fact]
    public async Task EndToEnd_AddSheetThroughToggle_ThenSaveProfile_PersistsNewSheet() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
            cut.Find("#column-header-input").Change("Numéro");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.IsolementRepere));
            cut.Find("#add-column-definition-button").Click();
            cut.Find("#add-sheet-generation-rule-button").Click();
            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Should().Contain(r => r.SheetName == "Enfants");
        });
}
