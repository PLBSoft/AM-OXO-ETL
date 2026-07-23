using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot R: densification de l'affichage des profils (import + export). The ticket's own explicit
// requirement is a *dedicated* test comparing the two editors' generated CSS class strings
// directly, rather than each editor's own test file separately asserting "has a non-empty class"
// -- a guard-rail against the two screens silently drifting apart in the future. One test class,
// rendering both ImportProfileEditor and ExportProfileEditor side by side, covers R1/R2/R3.
public class ProfileEditorParityTests : BunitContext
{
    public ProfileEditorParityTests()
    {
        var dbContextFactory = new TestDbContextFactory("ProfileEditorParityTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore ImportStore => Services.GetRequiredService<IImportProfileStore>();

    private IExportProfileStore ExportStore => Services.GetRequiredService<IExportProfileStore>();

    private static ImportProfile BuildImportProfileWithTwoSheetRules()
    {
        var isolementLocator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var isolementRule = new SheetExtractionRule(
            "ISOLEMENT", isolementLocator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"]);

        var platinesLocator = new RepeatingBlockLocator(
            "PLATINES",
            firstBlockStartRow: 17,
            step: 8,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
        var platinesRule = new SheetExtractionRule(
            "PLATINES", platinesLocator, pointRules: [], unconditionalColonneNames: ["TROU D'HOMME"]);

        return new ImportProfile("MAD OXO", "MAD TRAVAUX", [isolementRule, platinesRule]);
    }

    private static ExportProfile BuildExportProfileWithTwoSheetRules() =>
        new("Profil export OXO",
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")]),
                new SheetGenerationRule(
                    "Enfants",
                    PivotSource.Isolement,
                    [new ColumnDefinition("Numéro", PivotFieldRef.IsolementRepere)],
                    [])
            ]);

    // R1: same number of sheet rules on both sides -- the container class string must match
    // character-for-character, not merely "both non-empty".
    [Fact]
    public async Task SheetRuleGrid_CssClass_IsIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));

            var importListClass = importCut.Find("ul.sheet-rule-list").GetAttribute("class");
            var exportListClass = exportCut.Find("ul.sheet-rule-list").GetAttribute("class");

            importListClass.Should().Be(exportListClass);
            importListClass.Should().Contain("sheet-rule-grid");
        });

    // R2: the field/column grid inside a read-only sheet-rule card -- same class string on both
    // sides, reusing the same global CSS rather than each screen inventing its own.
    [Fact]
    public async Task BlockFieldGrid_CssClass_IsIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));
            exportCut.Find("#sheet-rule-details-toggle-0").Click();

            var importFieldListClass = importCut.Find("li.sheet-rule-card ul.block-field-list").GetAttribute("class");
            var exportFieldListClass = exportCut.Find("li.sheet-rule-card ul.block-field-list").GetAttribute("class");

            importFieldListClass.Should().Be(exportFieldListClass);
            importFieldListClass.Should().Contain("block-field-grid");
        });

    // R3: both screens collapse their unbounded-size sub-list behind the same
    // .sheet-rule-sublist-details structure, closed by default, expanding on click.
    [Fact]
    public async Task SheetRuleSublistDetails_CollapsedByDefaultBehavior_IsIdenticalBetweenImportAndExportEditors() =>
        await WithCultureAsync("en-US", async () =>
        {
            var importProfile = BuildImportProfileWithTwoSheetRules();
            await ImportStore.SaveAsync(importProfile);
            var exportProfile = BuildExportProfileWithTwoSheetRules();
            await ExportStore.SaveAsync(exportProfile);

            var importCut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, importProfile.Id));
            var exportCut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, exportProfile.Id));

            // Both start collapsed: the same class on the wrapping <details>, and the full-list
            // content div is absent from the DOM on both sides.
            var importDetailsClass = importCut.Find("details.sheet-rule-sublist-details").GetAttribute("class");
            var exportDetailsClass = exportCut.Find("details.sheet-rule-sublist-details").GetAttribute("class");
            importDetailsClass.Should().Be(exportDetailsClass);

            importCut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
            exportCut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();

            // Clicking the summary reveals the content div on both sides.
            importCut.Find("#sheet-rule-details-toggle-0").Click();
            exportCut.Find("#sheet-rule-details-toggle-0").Click();

            importCut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
            exportCut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
        });

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
