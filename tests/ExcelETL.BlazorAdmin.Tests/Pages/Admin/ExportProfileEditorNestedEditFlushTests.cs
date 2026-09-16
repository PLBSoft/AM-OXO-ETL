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

// Export-side mirror of ImportProfileEditorNestedEditFlushTests.cs -- see that file's own header
// comment for the full 2026-09-16 incident writeup this guards against. Also covers the second,
// related data-loss bug found while fixing the first: SheetGenerationRuleForm never carried
// ConstantColumnDefinitions through when rebuilding an edited sheet rule, silently dropping a
// TM_PROC_*-style sheet's constant columns on every save.
public class ExportProfileEditorNestedEditFlushTests : BunitContext
{
    public ExportProfileEditorNestedEditFlushTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorNestedEditFlushTests_" + Guid.NewGuid());
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

    [Fact]
    public async Task EditingAnExistingColumn_ThenSavingProfileDirectly_PersistsTheNewHeader() =>
        await WithCultureAsync("en-US", async () =>
        {
            var sheetRule = new SheetGenerationRule(
                "Parents", PivotSource.Equipement,
                [new ColumnDefinition("Repere", PivotFieldRef.EquipementRepere)], [], []);
            var profile = new ExportProfile("Profil export OXO", [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-column-definition-button-0").Click();
            cut.Find("#edit-0-column-0-header-input").Change("Repere renomme");

            // Deliberately never click #edit-0-save-column-definition-button-0.
            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().ColumnDefinitions.Single().Header.Should().Be("Repere renomme");
        });

    [Fact]
    public async Task EditingAnExistingPointColumn_ThenSavingProfileDirectly_PersistsTheNewHeader() =>
        await WithCultureAsync("en-US", async () =>
        {
            var sheetRule = new SheetGenerationRule(
                "Enfants", PivotSource.Isolement, [],
                [new PointColumnDefinition("PROLOCK VANNES", "Prolock vannes")], []);
            var profile = new ExportProfile("Profil export OXO", [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-point-column-definition-button-0").Click();
            cut.Find("#edit-0-point-column-0-header-input").Change("Prolock vannes renomme");

            // Deliberately never click #edit-0-save-point-column-definition-button-0.
            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().PointColumnDefinitions.Single().Header
                .Should().Be("Prolock vannes renomme");
        });

    [Fact]
    public async Task EditingAnExistingApplicationColumn_ThenSavingProfileDirectly_PersistsTheNewHeader() =>
        await WithCultureAsync("en-US", async () =>
        {
            var sheetRule = new SheetGenerationRule(
                "Parents", PivotSource.Equipement, [], [],
                [new ApplicationColumnDefinition("PROGRESS", "Progress")]);
            var profile = new ExportProfile("Profil export OXO", [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-modify-application-column-definition-button-0").Click();
            cut.Find("#edit-0-application-column-0-header-input").Change("Progress renomme");

            // Deliberately never click #edit-0-save-application-column-definition-button-0.
            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().ApplicationColumnDefinitions.Single().Header
                .Should().Be("Progress renomme");
        });

    [Fact]
    public async Task ModifyingAndSavingATacheMultipleSheetRule_PreservesItsConstantColumns() =>
        await WithCultureAsync("en-US", async () =>
        {
            // Reproduces the second, related bug found while fixing the nested-edit-flush one:
            // SheetGenerationRuleForm never hydrated/re-passed ConstantColumnDefinitions on its own
            // commit, so editing *any* field of a TM_PROC_*-style sheet rule and clicking its own
            // level-2 "Save changes" button (not a level-3 nested edit) silently dropped the sheet's
            // constant columns (CRITERE/AVANCEMENT/SUPPRESSION) on every save.
            var sheetRule = new SheetGenerationRule(
                "Taches multiples", PivotSource.TacheMultiple,
                [new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction)], [], [],
                constantColumnDefinitions: [new ConstantColumnDefinition("SUPPRESSION", "N")]);
            var profile = new ExportProfile("Profil export OXO", [sheetRule]);
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Taches multiples renommees");
            cut.Find("#save-sheet-generation-rule-button-0").Click();

            cut.Find("#save-export-profile-button").Click();

            var reloaded = (await Store.GetAllAsync()).Single();
            reloaded.SheetRules.Single().ConstantColumnDefinitions.Should()
                .ContainSingle(c => c.Header == "SUPPRESSION" && c.Value == "N");
        });
}
