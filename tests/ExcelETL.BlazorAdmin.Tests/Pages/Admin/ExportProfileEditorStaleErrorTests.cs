using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 076 (docs/tickets/tickets-tdd-lot-076-alignement-editeur-export-outils-brouillon.md), written
// red-first: the export editor never cleared the errors of a draft tree before converting it again, so
// an error the user had since fixed stayed on screen next to the new, real one. Covers the 3 places a
// whole draft subtree is re-converted: the rule's own submit button, the lot 057 form switch, the save.
public class ExportProfileEditorStaleErrorTests : BunitContext
{
    public ExportProfileEditorStaleErrorTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorStaleErrorTests_" + Guid.NewGuid());
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

    private async Task<ExportProfile> SeedProfileAsync()
    {
        var profile = new ExportProfile("Profil export OXO",
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [],
                    [])
            ]);
        await Store.SaveAsync(profile);
        return profile;
    }

    private static List<string> AlertTexts(IRenderedComponent<ExportProfileEditor> cut) =>
        [.. cut.FindAll(".alert-danger").Select(a => a.TextContent.Trim())];

    [Fact]
    public async Task SubmittingRuleAgain_AfterFixingSheetName_NoLongerShowsTheSheetNameError() =>
        await WithCultureAsync("en-US", () =>
        {
            var cut = Render<ExportProfileEditor>();

            cut.Find("#add-sheet-generation-rule-button").Click();
            var sheetNameError = AlertTexts(cut).Should().ContainSingle().Subject;

            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            // A pending column with a source but no header: the rule now fails on the column instead.
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
            cut.Find("#add-sheet-generation-rule-button").Click();

            var alerts = AlertTexts(cut);
            alerts.Should().NotContain(sheetNameError);
            alerts.Should().ContainSingle();
            return Task.CompletedTask;
        });

    [Fact]
    public async Task SwitchingFormsAgain_AfterFixingColumn_NoLongerShowsTheColumnError() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedProfileAsync();
            var cut = Render<ExportProfileEditor>(p => p.Add(c => c.Id, profile.Id));

            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-column-definition-button-0").Click();
            cut.Find("#edit-0-column-0-header-input").Change(string.Empty);
            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();
            var columnError = AlertTexts(cut).Should().ContainSingle().Subject;

            cut.Find("#edit-0-column-0-header-input").Change("Repère");
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change(string.Empty);
            cut.Find("#toggle-add-sheet-generation-rule-form-button").Click();

            var alerts = AlertTexts(cut);
            alerts.Should().NotContain(columnError);
            alerts.Should().ContainSingle();
        });

    [Fact]
    public async Task SavingAgain_AfterFixingColumn_NoLongerShowsTheColumnError() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = await SeedProfileAsync();
            var cut = Render<ExportProfileEditor>(p => p.Add(c => c.Id, profile.Id));

            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-column-definition-button-0").Click();
            cut.Find("#edit-0-column-0-header-input").Change(string.Empty);
            cut.Find("#save-export-profile-button").Click();
            var columnError = cut.Find("li.block-field-item-editing .alert-danger").TextContent.Trim();

            cut.Find("#edit-0-column-0-header-input").Change("Repère");
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change(string.Empty);
            cut.Find("#save-export-profile-button").Click();

            cut.FindAll("li.block-field-item-editing .alert-danger").Should().BeEmpty();
            AlertTexts(cut).Should().NotContain(columnError);
        });
}
