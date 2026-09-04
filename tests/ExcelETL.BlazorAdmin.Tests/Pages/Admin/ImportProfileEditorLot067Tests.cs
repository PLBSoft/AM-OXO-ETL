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

// Lot 067 (docs/tickets/tickets-tdd-lot-067-tache-multiple-repere-type-colonne-travaux.md), 67.5:
// TacheMultipleTypeLabels ("Colonne Travaux" mapping) exposed and editable in the import profile
// editor -- kept in its own file per this project's established convention (mirrors
// ImportProfileEditorLot063Tests.cs and siblings).
public class ImportProfileEditorLot067Tests : BunitContext
{
    public ImportProfileEditorLot067Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot067Tests_" + Guid.NewGuid());
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

    private static ImportProfile BuildProfileWithOneSheetRuleAndTacheMultipleTypeLabels(
        string name = "MAD OXO", IReadOnlyList<TacheMultipleTypeLabel>? tacheMultipleTypeLabels = null)
    {
        var locator = new RepeatingBlockLocator(
            "PROCEDURE", 9, 1, "Action", [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
        var rule = new SheetExtractionRule("PROCEDURE", locator, [], [], [], []);

        return new ImportProfile(
            name, "MAD TRAVAUX", [], [], [rule],
            tacheMultipleTypeLabels ?? []);
    }

    private static void OpenAddSheetRuleFormIfClosed(IRenderedComponent<ImportProfileEditor> cut)
    {
        if (cut.FindAll("#sheet-rule-name-input").Count == 0)
        {
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
        }
    }

    // The one sheet rule ImportProfile's own constructor requires (ImportProfile_NoSheetRules) --
    // filled via the UI, same field sequence as ImportProfileEditorLot063Tests' round-trip test.
    private static void AddMinimalSheetRuleThroughTheForm(IRenderedComponent<ImportProfileEditor> cut)
    {
        OpenAddSheetRuleFormIfClosed(cut);
        cut.Find("#sheet-rule-name-input").Change("PROCEDURE");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("1");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Action");
        cut.Find("#block-field-name-input").Change("Action");
        cut.Find("#block-field-absolute-range-input").Change("C9:L9");
        cut.Find("#add-block-field-button").Click();
        cut.Find("#add-sheet-rule-button").Click();
    }

    [Fact]
    public void AddTacheMultipleTypeLabelForm_CodeAndLabelFields_HaveAssociatedLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("label[for='tache-multiple-type-label-code-input']").TextContent.Should().NotBeNullOrEmpty();
        cut.Find("label[for='tache-multiple-type-label-label-input']").TextContent.Should().NotBeNullOrEmpty();
    });

    [Fact]
    public void AddTacheMultipleTypeLabel_WithValidCodeAndLabel_DisplaysItInTheListAndResetsTheForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.Find("#tache-multiple-type-label-code-input").GetAttribute("value").Should().BeNullOrEmpty();
        var items = cut.FindAll("#tache-multiple-type-labels-list .block-field-item");
        items.Should().ContainSingle();
        items[0].TextContent.Should().Contain("TM_PROC_MAD").And.Contain("Procédure MAD");
    });

    [Fact]
    public void AddTacheMultipleTypeLabel_WithEmptyCode_ShowsLocalizedErrorAndDoesNotAdd() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.FindAll("#tache-multiple-type-labels-list .block-field-item").Should().BeEmpty();
        cut.Markup.Should().Contain("alert-danger");
    });

    [Fact]
    public void AddTacheMultipleTypeLabel_WithDuplicateCode_ShowsLocalizedErrorAndDoesNotAddASecondEntry() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.Find("#tache-multiple-type-label-code-input").Change(" tm_proc_mad ");
        cut.Find("#tache-multiple-type-label-label-input").Change("Autre libellé");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.FindAll("#tache-multiple-type-labels-list .block-field-item").Should().ContainSingle();
        cut.Markup.Should().Contain("alert-danger");
    });

    [Fact]
    public void ModifyTacheMultipleTypeLabel_PrefillsCodeAndLabel_AndSaveUpdatesInPlaceWithoutDuplicating() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.Find("#edit-tache-multiple-type-label-button-0").Click();
        cut.Find("#tache-multiple-type-label-edit-code-input-0").GetAttribute("value").Should().Be("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-edit-label-input-0").GetAttribute("value").Should().Be("Procédure MAD");

        cut.Find("#tache-multiple-type-label-edit-label-input-0").Change("Procédure MAD (révisée)");
        cut.Find("#save-tache-multiple-type-label-button-0").Click();

        var items = cut.FindAll("#tache-multiple-type-labels-list .block-field-item");
        items.Should().ContainSingle();
        items[0].TextContent.Should().Contain("Procédure MAD (révisée)");
    });

    [Fact]
    public void ModifyTacheMultipleTypeLabel_CancelDiscardsChanges_LeavesOriginalValueUnmodified() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.Find("#edit-tache-multiple-type-label-button-0").Click();
        cut.Find("#tache-multiple-type-label-edit-label-input-0").Change("Changed");
        cut.Find("#cancel-tache-multiple-type-label-edit-button-0").Click();

        cut.FindAll("#tache-multiple-type-labels-list .block-field-item")[0].TextContent.Should().Contain("Procédure MAD");
        cut.FindAll("#tache-multiple-type-label-edit-code-input-0").Should().BeEmpty();
    });

    [Fact]
    public void DeleteTacheMultipleTypeLabel_RemovesItFromTheList() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.Find("#delete-tache-multiple-type-label-button-0").Click();

        cut.FindAll("#tache-multiple-type-labels-list .block-field-item").Should().BeEmpty();
    });

    [Fact]
    public async Task SaveProfile_PersistsTacheMultipleTypeLabels_RoundTripsThroughTheStore() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
            cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
            cut.Find("#add-tache-multiple-type-label-button").Click();
            cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_REL");
            cut.Find("#tache-multiple-type-label-label-input").Change("Procédure REL");
            cut.Find("#add-tache-multiple-type-label-button").Click();

            AddMinimalSheetRuleThroughTheForm(cut);

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.TacheMultipleTypeLabels.Should().HaveCount(2);
            reloaded.TacheMultipleTypeLabels.Should().Contain(l => l.Code == "TM_PROC_MAD" && l.Label == "Procédure MAD");
            reloaded.TacheMultipleTypeLabels.Should().Contain(l => l.Code == "TM_PROC_REL" && l.Label == "Procédure REL");
        });

    [Fact]
    public async Task EditingExistingProfile_PrefillsTacheMultipleTypeLabelsFromTheStore() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRuleAndTacheMultipleTypeLabels(
                tacheMultipleTypeLabels: [new TacheMultipleTypeLabel("TM_PROC_MAD", "Procédure MAD")]);
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            var items = cut.FindAll("#tache-multiple-type-labels-list .block-field-item");
            items.Should().ContainSingle();
            items[0].TextContent.Should().Contain("TM_PROC_MAD").And.Contain("Procédure MAD");
        });

    [Fact]
    public void AddTacheMultipleTypeLabel_MarksProfileAsChanged() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#tache-multiple-type-label-code-input").Change("TM_PROC_MAD");
        cut.Find("#tache-multiple-type-label-label-input").Change("Procédure MAD");
        cut.Find("#add-tache-multiple-type-label-button").Click();

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
    });
}
