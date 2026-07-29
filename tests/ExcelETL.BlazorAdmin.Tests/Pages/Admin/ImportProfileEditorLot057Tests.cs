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

// Lot 057: a single formulaire-de-feuille open at a time -- 57.1 (add form repli/toggle), 57.2
// (mutual exclusion). Kept in its own file, mirroring the project's established convention.
public class ImportProfileEditorLot057Tests : BunitContext
{
    public ImportProfileEditorLot057Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot057Tests_" + Guid.NewGuid());
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
            "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    private static ImportProfile BuildProfileWithTwoSheetRules(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var isolementLocator = new RepeatingBlockLocator(
            "ISOLEMENT", firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var isolementRule = new SheetExtractionRule(
            "ISOLEMENT", isolementLocator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

        var platinesLocator = new RepeatingBlockLocator(
            "PLATINES", firstBlockStartRow: 17, step: 8, stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var platinesRule = new SheetExtractionRule(
            "PLATINES", platinesLocator, pointRules: [], unconditionalColonneNames: ["TROU D'HOMME"], [], []);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [isolementRule, platinesRule]);
    }

    // ------------------------------------------------------------------------------------------
    // 57.1
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task EditMode_OnLoad_AddFormFieldsAreAbsent_ToggleButtonPresent() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#sheet-rule-name-input").Should().BeEmpty();
            cut.FindAll("#add-sheet-rule-button").Should().BeEmpty();
            cut.FindAll("#toggle-add-sheet-rule-form-button").Should().HaveCount(1);
        });

    [Fact]
    public async Task EditMode_ClickingToggle_RendersAddFormFields() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#toggle-add-sheet-rule-form-button").Click();

            cut.FindAll("#sheet-rule-name-input").Should().HaveCount(1);
        });

    [Fact]
    public async Task EditMode_ClickingToggleTwice_HidesAddFormFieldsAgain() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var toggle = cut.Find("#toggle-add-sheet-rule-form-button");
            toggle.Click();
            cut.FindAll("#sheet-rule-name-input").Should().HaveCount(1);

            cut.Find("#toggle-add-sheet-rule-form-button").Click();
            cut.FindAll("#sheet-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public void CreateMode_OnLoad_AddFormFieldsArePresent()
    {
        var cut = Render<ImportProfileEditor>();

        cut.FindAll("#sheet-rule-name-input").Should().HaveCount(1);
    }

    [Fact]
    public async Task EditMode_SuccessfulSubmission_ClosesTheFormAndTogglesBackToClosed() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#toggle-add-sheet-rule-form-button").Click();

            cut.Find("#sheet-rule-name-input").Change("PLATINES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("8");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B17:E17");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.FindAll("#sheet-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public async Task ReopeningAfterPartialInput_FieldsAreEmpty_ProvingRemount() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            var toggle = cut.Find("#toggle-add-sheet-rule-form-button");
            toggle.Click();
            cut.Find("#sheet-rule-name-input").Change("Partial input, never submitted");

            cut.Find("#toggle-add-sheet-rule-form-button").Click();
            cut.Find("#toggle-add-sheet-rule-form-button").Click();

            cut.Find("#sheet-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();
        });

    [Fact]
    public async Task ClosedToggle_HasIconAndNonEmptyLabel_OpenToggle_HasNoIcon() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var closedToggle = cut.Find("#toggle-add-sheet-rule-form-button");
            closedToggle.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
            closedToggle.TextContent.Trim().Should().NotBeEmpty();

            closedToggle.Click();

            var openToggle = cut.Find("#toggle-add-sheet-rule-form-button");
            openToggle.QuerySelector("svg").Should().BeNull();
            openToggle.TextContent.Trim().Should().NotBeEmpty();
        });

    [Fact]
    public async Task EndToEnd_AddSheetThroughToggle_ThenSaveProfile_PersistsNewSheet() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
            cut.Find("#sheet-rule-name-input").Change("PLATINES");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("17");
            cut.Find("#sheet-rule-step-input").Change("8");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B17:E17");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Should().Contain(r => r.SheetName == "PLATINES");
        });
}
