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

// Lot 091 (docs/tickets/tickets-tdd-lot-091-export-plans-pdf.md), 091.2: the plan sheets printed to
// PDF, editable in the import profile editor -- same root-level list as Tableaux/Applications.
public class ImportProfileEditorLot091Tests : BunitContext
{
    public ImportProfileEditorLot091Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot091Tests_" + Guid.NewGuid());
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

    private static ImportProfile BuildProfile(IReadOnlyList<string> planSheetNames)
    {
        var locator = new RepeatingBlockLocator(9, 1, [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
        var rule = new SheetExtractionRule("PROCEDURE", locator, [], [], [], []);
        return new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [rule], [], planSheetNames);
    }

    private static void AddPlanSheet(IRenderedComponent<ImportProfileEditor> cut, string name)
    {
        cut.Find("#plan-sheet-name-input").Change(name);
        cut.Find("#add-plan-sheet-name-button").Click();
    }

    private static IReadOnlyList<string> ListedPlanSheets(IRenderedComponent<ImportProfileEditor> cut) =>
        cut.FindAll("#plan-sheet-names-list .block-field-item .block-field-name").Select(e => e.TextContent).ToList();

    [Fact]
    public void PlanSheetSection_HasHeadingHelpAndLabelledInput() => WithCulture("fr-FR", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Markup.Should().Contain("Onglets de plans imprimés en PDF");
        cut.Find("#plan-sheet-names-help").TextContent.Should().Contain("PDF");
        cut.Find("label[for='plan-sheet-name-input']").TextContent.Should().Be("Nom de l'onglet");
        cut.Find("#plan-sheet-name-input").GetAttribute("maxlength").Should().Be("31");
    });

    [Fact]
    public void AddPlanSheet_WithValidName_ListsItTrimmed_AndResetsTheField() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        AddPlanSheet(cut, "  PLANS ISOLEMENT (Paysage) ");

        ListedPlanSheets(cut).Should().Equal("PLANS ISOLEMENT (Paysage)");
        cut.Find("#plan-sheet-name-input").GetAttribute("value").Should().BeNullOrEmpty();
    });

    [Fact]
    public void AddPlanSheet_DuplicateIgnoringCase_IsRefused_WithALocalizedError() => WithCulture("fr-FR", () =>
    {
        var cut = Render<ImportProfileEditor>();
        AddPlanSheet(cut, "PLANS ISOLEMENT (Paysage)");

        AddPlanSheet(cut, "plans isolement (paysage)");

        ListedPlanSheets(cut).Should().ContainSingle();
        cut.Find(".alert-danger[role='alert']").TextContent.Should().Contain("déjà dans la liste");
    });

    [Fact]
    public void ModifyPlanSheet_PrefillsAndSavesInPlace() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        AddPlanSheet(cut, "PLANS ISOLEMENT (Paysage)");

        cut.Find("#edit-plan-sheet-name-button-0").Click();
        cut.Find("#plan-sheet-name-edit-input-0").GetAttribute("value").Should().Be("PLANS ISOLEMENT (Paysage)");
        cut.Find("#plan-sheet-name-edit-input-0").Change("PLANS ISOLEMENT (Portrait)");
        cut.Find("#save-plan-sheet-name-button-0").Click();

        ListedPlanSheets(cut).Should().Equal("PLANS ISOLEMENT (Portrait)");
    });

    [Fact]
    public void ModifyPlanSheet_Cancel_RestoresTheOriginalValue() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        AddPlanSheet(cut, "PLANS ISOLEMENT (Paysage)");

        cut.Find("#edit-plan-sheet-name-button-0").Click();
        cut.Find("#plan-sheet-name-edit-input-0").Change("Changed");
        cut.Find("#cancel-plan-sheet-name-edit-button-0").Click();

        ListedPlanSheets(cut).Should().Equal("PLANS ISOLEMENT (Paysage)");
    });

    [Fact]
    public void DeletePlanSheet_RemovesItFromTheList() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        AddPlanSheet(cut, "PLANS ISOLEMENT (Paysage)");

        cut.Find("#delete-plan-sheet-name-button-0").Click();

        cut.FindAll("#plan-sheet-names-list .block-field-item").Should().BeEmpty();
    });

    [Fact]
    public async Task EditingExistingProfile_PrefillsThePlanSheets_AndSavePersistsTheChange() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfile(["PLANS ISOLEMENT (Paysage)"]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            ListedPlanSheets(cut).Should().Equal("PLANS ISOLEMENT (Paysage)");

            AddPlanSheet(cut, "PLANS PLATINAGE & JOINTS");
            cut.Find("#save-profile-button").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.PlanSheetNames.Should().Equal("PLANS ISOLEMENT (Paysage)", "PLANS PLATINAGE & JOINTS");
        });

    // Defect A (recommandations-tickets-tdd.md §9): a typed but never "Add"-clicked name is saved too.
    [Fact]
    public async Task PendingPlanSheet_TypedButNotAdded_IsSavedWithTheProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfile([]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#plan-sheet-name-input").Change("PLANS PLATINAGE & JOINTS");
            cut.Find("#save-profile-button").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.PlanSheetNames.Should().Equal("PLANS PLATINAGE & JOINTS");
        });

    [Fact]
    public async Task PendingPlanSheet_DuplicateOfAListedOne_BlocksTheSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfile(["PLANS ISOLEMENT (Paysage)"]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#plan-sheet-name-input").Change("plans isolement (PAYSAGE)");
            cut.Find("#save-profile-button").Click();

            cut.FindAll(".alert-danger[role='alert']").Should().NotBeEmpty();
            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.PlanSheetNames.Should().Equal("PLANS ISOLEMENT (Paysage)");
        });
}
