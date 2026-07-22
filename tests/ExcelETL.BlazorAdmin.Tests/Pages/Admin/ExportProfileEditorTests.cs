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

    // Parity ticket Q1: every input in ExportProfileEditor.razor gets a visible <label>, matching
    // ImportProfileEditor.razor's convention (placeholder-only was the pre-existing gap).
    [Fact]
    public void RootAndAddSheetInputs_HaveVisibleLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("label[for='export-profile-name-input']").TextContent.Should().Be("Profile name");
        cut.Find("label[for='sheet-generation-rule-name-input']").TextContent.Should().Be("Sheet name");
        cut.Find("label[for='sheet-generation-rule-pivot-source-select']").TextContent.Should().Be("Pivot source");
        cut.Find("label[for='column-header-input']").TextContent.Should().Be("Header");
        cut.Find("label[for='column-source-select']").TextContent.Should().Be("Source field");
        cut.Find("label[for='point-column-nom-input']").TextContent.Should().Be("Colonne name");
        cut.Find("label[for='point-column-header-input']").TextContent.Should().Be("Header");
        cut.Find("label[for='point-column-mark-value-input']").TextContent.Should().Be("Mark value");
    });

    // Parity ticket Q2: each already-added SheetGenerationRule is wrapped in the same
    // .sheet-rule-card container as ImportProfileEditor.razor's summary, reusing the global CSS
    // from app.css rather than introducing export-specific rules.
    [Fact]
    public void Summary_WithMultipleSheetRules_WrapsEachInADistinctCard() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);

        cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
        cut.Find("#column-header-input").Change("Numéro");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.IsolementRepere));
        cut.Find("#add-column-definition-button").Click();
        cut.Find("#add-sheet-generation-rule-button").Click();

        cut.FindAll("li.sheet-rule-card").Should().HaveCount(2);
    });

    [Fact]
    public void Summary_SheetNameAndMetadata_AreSeparateElements() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);

        cut.Find(".sheet-rule-card-title").TextContent.Should().Be("Parents");
        cut.Find(".sheet-rule-card-meta").TextContent.Should().Be("Source: Equipement");
    });

    // Parity ticket Q3: columns/point columns render as separate name/value elements (reusing
    // .block-field-name/.block-field-range, shared verbatim with the import side) instead of one
    // concatenated text run.
    [Fact]
    public void Summary_ColumnAndPointColumn_DisplayNameAndValueAsSeparateElements() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);

        var names = cut.FindAll(".block-field-name").Select(e => e.TextContent).ToList();
        var values = cut.FindAll(".block-field-range").Select(e => e.TextContent).ToList();

        names.Should().Contain("Repère");
        values.Should().Contain(nameof(PivotFieldRef.EquipementRepere));
        names.Should().Contain("Travaux complet");
        values.Should().Contain($"TRAVAUX COMPLET · {PointColumnDefinition.DefaultMarkValue}");
    });

    [Fact]
    public void Summary_ColumnWithNoSource_DisplaysNotMappedAsSeparateElement() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-name-input").Change("Parents");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));
        cut.Find("#column-header-input").Change("Commentaires");
        cut.Find("#column-source-select").Change(string.Empty);
        cut.Find("#add-column-definition-button").Click();
        cut.Find("#add-sheet-generation-rule-button").Click();

        cut.Find(".block-field-name").TextContent.Should().Be("Commentaires");
        cut.Find(".block-field-range").TextContent.Should().Be("Not mapped yet");
    });

    // Parity ticket Q6 (convention-ui-blazor-alignement-boutons.md): the two bottom-of-card/
    // section buttons are right-aligned, reusing .right-aligned-actions verbatim. The in-row
    // "Add column"/"Add point column" buttons are deliberately not wrapped -- already at the end
    // of their row by construction, same carve-out documented for the import side.
    [Fact]
    public void AddSheetGenerationRuleButton_IsInRightAlignedContainer() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#add-sheet-generation-rule-button").ParentElement!.GetAttribute("class")
            .Should().Contain("right-aligned-actions");
    });

    [Fact]
    public void SaveExportProfileButton_IsInRightAlignedContainer() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#save-export-profile-button").ParentElement!.GetAttribute("class")
            .Should().Contain("right-aligned-actions");
    });
}
