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
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class ExportProfileEditorTests : BunitContext
{
    public ExportProfileEditorTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfileEditorTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

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

    private IExportProfileStore Store => Services.GetRequiredService<IExportProfileStore>();

    // Fills SheetName + PivotSource, one mapped ColumnDefinition, one PointColumnDefinition,
    // then clicks "add sheet rule", leaving the render handle positioned after the add.
    private static void AddValidSheetRule(IRenderedComponent<ExportProfileEditor> cut)
    {
        cut.Find("#sheet-generation-rule-name-input").Change("Parents");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();

        cut.Find("#add-sheet-generation-rule-button").Click();
    }

    private static ExportProfile BuildProfileWithOneSheetRule(string name = "Profil export OXO") =>
        new(name,
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [new PointColumnDefinition("TRAVAUX COMPLET", "Travaux complet")])
            ]);

    [Fact]
    public async Task Save_WithEmptyName_DisplaysLocalizedErrorAndDoesNotPersist() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();

            cut.Find("#save-export-profile-button").Click();

            cut.Markup.Should().Contain("Name must not be empty.");

            var all = await Store.GetAllAsync();
            all.Should().BeEmpty();
        });

    [Fact]
    public void Save_WithNoSheetRulesAdded_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#export-profile-name-input").Change("Profil export OXO");

        cut.Find("#save-export-profile-button").Click();

        cut.Markup.Should().Contain("Sheet rules must contain at least one rule.");
    });

    [Fact]
    public void AddSheetRule_WithValidInput_DisplaysSheetSummaryAndResetsForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);

        cut.Markup.Should().Contain("Parents");
        cut.Markup.Should().Contain("Repère");
        cut.Markup.Should().Contain("TRAVAUX COMPLET");
        cut.Find("#sheet-generation-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();
    });

    [Fact]
    public void AddSheetRule_WithDuplicateHeader_DisplaysLocalizedErrorAndDoesNotAddSheet() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-name-input").Change("Parents");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementDesignation));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#add-sheet-generation-rule-button").Click();

        cut.Markup.Should().Contain("is used more than once");
        cut.Markup.Should().Contain("No sheet rules added yet.");
    });

    [Fact]
    public async Task AddColumnDefinition_WithSourceNotMapped_BuildsColumnWithNullSource_SavedWithoutError() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#export-profile-name-input").Change("Profil export OXO");

            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            cut.Find("#column-header-input").Change("Commentaires");
            cut.Find("#column-source-select").Change(string.Empty);
            cut.Find("#add-column-definition-button").Click();

            cut.Find("#add-sheet-generation-rule-button").Click();

            cut.Find("#save-export-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/export-profiles");

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            var column = saved.SheetRules.Single().ColumnDefinitions.Single();
            column.Header.Should().Be("Commentaires");
            column.Source.Should().BeNull();
        });

    [Fact]
    public void PivotSourceSelect_WhenChanged_FiltersColumnSourceOptions() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));
        var equipementOptions = cut.Find("#column-source-select").QuerySelectorAll("option");
        equipementOptions.Select(o => o.GetAttribute("value")).Should().Contain(nameof(PivotFieldRef.EquipementRepere));
        equipementOptions.Select(o => o.GetAttribute("value")).Should().NotContain(nameof(PivotFieldRef.IsolementRepere));

        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
        var isolementOptions = cut.Find("#column-source-select").QuerySelectorAll("option");
        isolementOptions.Select(o => o.GetAttribute("value")).Should().Contain(nameof(PivotFieldRef.IsolementRepere));
        isolementOptions.Select(o => o.GetAttribute("value")).Should().NotContain(nameof(PivotFieldRef.EquipementRepere));
    });

    [Fact]
    public async Task Save_WithValidRootFieldsAndOneAddedSheetRule_PersistsProfileAndNavigatesToList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            AddValidSheetRule(cut);

            cut.Find("#save-export-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/export-profiles");

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Name.Should().Be("Profil export OXO");
            saved.SheetRules.Should().ContainSingle();

            var rule = saved.SheetRules.Single();
            rule.SheetName.Should().Be("Parents");
            rule.PivotSource.Should().Be(PivotSource.Equipement);
            rule.ColumnDefinitions.Should().ContainSingle(c => c.Header == "Repère" && c.Source == PivotFieldRef.EquipementRepere);
            rule.PointColumnDefinitions.Should().ContainSingle(p => p.ColonneNom == "TRAVAUX COMPLET" && p.Header == "Travaux complet");
        });

    [Fact]
    public async Task EditRoute_WithExistingProfile_PrefillsNameAndSheetRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#export-profile-name-input").GetAttribute("value").Should().Be("Profil export OXO");
            cut.Markup.Should().Contain("Parents");
            cut.Markup.Should().Contain("Repère");
            cut.Markup.Should().Contain("TRAVAUX COMPLET");
        });

    [Fact]
    public async Task EditRoute_SaveAfterModification_UsesSameProfileId() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#export-profile-name-input").Change("Profil export OXO modifié");
            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Id.Should().Be(profile.Id);
            saved.Name.Should().Be("Profil export OXO modifié");
        });

    [Fact]
    public void EditRoute_WithUnknownId_DisplaysErrorAndDoesNotRenderForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, Guid.NewGuid()));

        cut.Markup.Should().Contain("Export profile not found.");
        cut.FindAll("#export-profile-name-input").Should().BeEmpty();
        cut.FindAll("#save-export-profile-button").Should().BeEmpty();
    });
}
