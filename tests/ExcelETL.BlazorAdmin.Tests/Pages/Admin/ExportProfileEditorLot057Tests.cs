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

// Lot 057: export-side mirror of ImportProfileEditorLot057Tests.cs (57.1 and 57.2).
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

    private static ExportProfile BuildProfileWithTwoSheetRules(string name = "Profil export OXO") =>
        new(name,
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")],
                    []),
                new SheetGenerationRule(
                    "Enfants",
                    PivotSource.Isolement,
                    [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)],
                    [],
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

    // ------------------------------------------------------------------------------------------
    // 57.2: mutual exclusion.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ModifyingAnotherSheet_ClosesTheCurrentlyEditedOne_AndCommitsItsChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents modifié");

            cut.Find("#modify-sheet-generation-rule-button-1").Click();

            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().BeEmpty();
            cut.FindAll("#edit-1-sheet-generation-rule-name-input").Should().HaveCount(1);
            cut.FindAll("li.sheet-rule-card").Should().Contain(li => li.TextContent.Contains("Parents modifié"));
        });

    [Fact]
    public async Task ModifyingAnotherSheet_WhenCurrentIsInvalid_KeepsCurrentOpenAndDoesNotSwitch() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change(string.Empty);
            cut.Find("#save-sheet-generation-rule-button-0").Click();

            cut.Find("#modify-sheet-generation-rule-button-1").Click();

            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().HaveCount(1);
            cut.FindAll("#edit-1-sheet-generation-rule-name-input").Should().BeEmpty();
            cut.FindAll(".alert-danger").Should().NotBeEmpty();
        });

    [Fact]
    public async Task ValidAddFormOpen_ThenModifyingAnExistingSheet_AddsTheNewSheet_AndOpensEdit() =>
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

            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.Markup.Should().Contain("Enfants");
            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().HaveCount(1);
        });

    [Fact]
    public async Task OpeningEdit_ThenClickingAddToggle_CommitsTheEditAndOpensAdd() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents modifié");

            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();

            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().BeEmpty();
            cut.FindAll("#sheet-generation-rule-name-input").Should().HaveCount(1);
        });

    [Fact]
    public async Task CancelOnModifiedEditForm_DiscardsChanges_NoCommit_NoOtherFormOpens() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents modifié");

            cut.Find("#cancel-sheet-generation-rule-button-0").Click();

            cut.Find("#save-export-profile-button").Click();
            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().SheetName.Should().Be("Parents");
            cut.FindAll("#sheet-generation-rule-name-input").Should().BeEmpty();
            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public async Task PendingDeleteConfirmation_ThenModifyingAnotherSheet_ClosesConfirmation_DeletesNothing() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-generation-rule-button-1").Click();
            cut.FindAll("#confirm-delete-sheet-generation-rule-button-1").Should().HaveCount(1);

            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.FindAll("#confirm-delete-sheet-generation-rule-button-1").Should().BeEmpty();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Should().HaveCount(2);
        });

    [Fact]
    public async Task AtMostOneSheetRuleFormRendered_AcrossMultipleOpenActions() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.FindAll("input[id*='sheet-generation-rule-name-input']").Should().HaveCount(1);

            cut.Find("#modify-sheet-generation-rule-button-1").Click();
            cut.FindAll("input[id*='sheet-generation-rule-name-input']").Should().HaveCount(1);

            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            cut.FindAll("input[id*='sheet-generation-rule-name-input']").Should().HaveCount(1);
        });

    [Fact]
    public async Task EditingCard_CarriesEditingItemClass_OthersDoNot() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-1").Click();

            var items = cut.FindAll("li.sheet-rule-editing-item, li.sheet-rule-card");
            items.Should().HaveCount(2);
            items[0].ClassList.Should().Contain("sheet-rule-card");
            items[1].ClassList.Should().Contain("sheet-rule-editing-item");
        });
}
