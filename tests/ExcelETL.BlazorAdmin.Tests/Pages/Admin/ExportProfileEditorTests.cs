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
        cut.Find("#sheet-generation-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();

        // Lot R3: columns/point columns are collapsed by default.
        cut.Markup.Should().NotContain("Repère");
        cut.Find("#sheet-rule-details-toggle-0").Click();
        cut.Markup.Should().Contain("Repère");
        cut.Markup.Should().Contain("TRAVAUX COMPLET");
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
    public void PivotSourceSelect_WhenChangedToTacheMultiple_FiltersColumnSourceOptionsToTacheMultipleFields() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ExportProfileEditor>();

            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.TacheMultiple));
            var options = cut.Find("#column-source-select").QuerySelectorAll("option");

            options.Select(o => o.GetAttribute("value")).Should().Contain(nameof(PivotFieldRef.TacheMultipleAction));
            options.Select(o => o.GetAttribute("value")).Should().NotContain(nameof(PivotFieldRef.EquipementRepere));
            options.Select(o => o.GetAttribute("value")).Should().NotContain(nameof(PivotFieldRef.IsolementRepere));
        });

    [Fact]
    public void PivotSourceSelect_WhenTacheMultipleSelected_HidesPointColumnSubform() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.TacheMultiple));

        cut.FindAll("#add-point-column-definition-button").Should().BeEmpty();
        cut.FindAll("#point-column-nom-input").Should().BeEmpty();
    });

    [Fact]
    public void PivotSourceSelect_SwitchedBackFromTacheMultiple_RestoresPointColumnSubform() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.TacheMultiple));
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

        cut.FindAll("#add-point-column-definition-button").Should().ContainSingle();
    });

    [Fact]
    public async Task Save_WithTacheMultipleSheetRuleAndColumns_PersistsProfileAndNavigatesToList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#export-profile-name-input").Change("Profil export OXO");

            cut.Find("#sheet-generation-rule-name-input").Change("Tâches multiples");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.TacheMultiple));

            cut.Find("#column-header-input").Change("Action");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.TacheMultipleAction));
            cut.Find("#add-column-definition-button").Click();

            cut.Find("#add-sheet-generation-rule-button").Click();
            cut.Find("#save-export-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/export-profiles");

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            var rule = saved.SheetRules.Should().ContainSingle().Which;

            rule.SheetName.Should().Be("Tâches multiples");
            rule.PivotSource.Should().Be(PivotSource.TacheMultiple);
            rule.ColumnDefinitions.Should().ContainSingle(c => c.Header == "Action" && c.Source == PivotFieldRef.TacheMultipleAction);
            rule.PointColumnDefinitions.Should().BeEmpty();
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

    // Lot U (docs/tickets-tdd-pivot-tableaux-applications-export.md), out-of-scope note: minimal form
    // fields to enter ApplicationColumnDefinition on the always-present "Add a sheet rule" card.
    [Fact]
    public async Task Save_WithAddedApplicationColumn_PersistsApplicationColumnDefinition() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ExportProfileEditor>();

            cut.Find("#export-profile-name-input").Change("Profil export OXO");
            cut.Find("#sheet-generation-rule-name-input").Change("Parents");
            cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Equipement));

            cut.Find("#column-header-input").Change("Repère");
            cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
            cut.Find("#add-column-definition-button").Click();

            cut.Find("#application-column-nom-input").Change("PROGRESS");
            cut.Find("#application-column-header-input").Change("PROGRESS");
            cut.Find("#application-column-mark-value-input").Change("O");
            cut.Find("#add-application-column-definition-button").Click();

            cut.Find("#add-sheet-generation-rule-button").Click();
            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            var saved = all.Should().ContainSingle().Subject;
            var rule = saved.SheetRules.Should().ContainSingle().Which;

            var applicationColumn = rule.ApplicationColumnDefinitions.Should().ContainSingle().Which;
            applicationColumn.ApplicationNom.Should().Be("PROGRESS");
            applicationColumn.Header.Should().Be("PROGRESS");
            applicationColumn.MarkValue.Should().Be("O");
        });

    [Fact]
    public void ApplicationColumnInputs_HaveVisibleLabels() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("label[for='application-column-nom-input']").TextContent.Should().Be("Application name");
        cut.Find("label[for='application-column-header-input']").TextContent.Should().Be("Header");
        cut.Find("label[for='application-column-mark-value-input']").TextContent.Should().Be("Mark value");
    });

    [Fact]
    public void PivotSourceSelect_WhenTacheMultipleSelected_HidesApplicationColumnSubform() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.TacheMultiple));

        cut.FindAll("#add-application-column-definition-button").Should().BeEmpty();
        cut.FindAll("#application-column-nom-input").Should().BeEmpty();
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

            // Lot R3: columns/point columns are collapsed by default.
            cut.Markup.Should().NotContain("Repère");
            cut.Find("#sheet-rule-details-toggle-0").Click();
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

    // Lot R1: same responsive grid class as the import side (app.css, shared verbatim).
    [Fact]
    public void SheetRuleList_HasGridCssClass() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);
        cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
        cut.Find("#column-header-input").Change("Numéro");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.IsolementRepere));
        cut.Find("#add-column-definition-button").Click();
        cut.Find("#add-sheet-generation-rule-button").Click();

        var list = cut.Find("ul.sheet-rule-list");
        list.ClassList.Should().Contain("sheet-rule-grid");
        cut.FindAll("li.sheet-rule-card").Should().HaveCount(2);
    });

    // Lot R2: the column/point-column list inside a read-only sheet-rule card is a compact
    // multi-column grid, same class as the import side's block-field list.
    [Fact]
    public void BlockFieldList_InSummary_HasGridCssClass() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);
        cut.Find("#sheet-rule-details-toggle-0").Click();

        var fieldList = cut.Find("li.sheet-rule-card ul.block-field-list");
        fieldList.ClassList.Should().Contain("block-field-grid");
    });

    // Lot R3: ColumnDefinition/PointColumnDefinition are collapsed behind a details/summary,
    // closed by default -- the full list must be genuinely absent from the DOM.
    [Fact]
    public void SheetRuleSublistDetails_CollapsedByDefault_FullListIsAbsentFromDom() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);

        cut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
        cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("1");
    });

    [Fact]
    public void SheetRuleSublistDetails_ClickingSummary_ExpandsFullListWithSameValues() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);
        cut.Find("#sheet-rule-details-toggle-0").Click();

        cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
        cut.Markup.Should().Contain("Repère");
        cut.Markup.Should().Contain("TRAVAUX COMPLET");
    });

    // Ticket R3 (correctif): clicking the summary a second time must collapse the sublist again.
    [Fact]
    public void SheetRuleSublistDetails_ClickingSummaryTwice_CollapsesAgain() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);
        var toggle = cut.Find("#sheet-rule-details-toggle-0");

        toggle.Click();
        cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);

        toggle.Click();
        cut.FindAll("#sheet-rule-details-content-0").Should().BeEmpty();
    });

    // Ticket R3 (correctif): expanding one card's sublist must not affect any other card's state.
    [Fact]
    public void SheetRuleSublistDetails_ExpandingOneCard_DoesNotAffectOtherCards() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);
        cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
        cut.Find("#column-header-input").Change("Numéro");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.IsolementRepere));
        cut.Find("#add-column-definition-button").Click();
        cut.Find("#add-sheet-generation-rule-button").Click();

        cut.Find("#sheet-rule-details-toggle-0").Click();

        cut.FindAll("#sheet-rule-details-content-0").Should().HaveCount(1);
        cut.FindAll("#sheet-rule-details-content-1").Should().BeEmpty();
    });

    // Ticket R3 (correctif): a sheet rule with neither columns nor point columns must still expose
    // a working, clickable toggle -- previously the whole <details> block was omitted entirely.
    [Fact]
    public void SheetRuleSublistDetails_WithEmptySublist_StillRendersToggle() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
        cut.Find("#add-sheet-generation-rule-button").Click();

        cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("0");
    });

    [Fact]
    public void SheetRuleSublistDetails_WithEmptySublist_ClickingShowsCoherentEmptyState() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#sheet-generation-rule-name-input").Change("Enfants");
        cut.Find("#sheet-generation-rule-pivot-source-select").Change(nameof(PivotSource.Isolement));
        cut.Find("#add-sheet-generation-rule-button").Click();

        cut.Find("#sheet-rule-details-toggle-0").Click();

        cut.Find("#sheet-rule-details-content-0").TextContent
            .Should().Contain("No columns or point columns for this sheet.");
    });

    // Ticket R3 (correctif), Refactor step: the native <details> element's `open` attribute must
    // reflect the C# expansion state so assistive technology gets correct disclosure semantics.
    [Fact]
    public void SheetRuleSublistDetails_OpenAttribute_ReflectsExpandedState() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        AddValidSheetRule(cut);
        var details = cut.Find("details.sheet-rule-sublist-details");
        details.HasAttribute("open").Should().BeFalse();

        cut.Find("#sheet-rule-details-toggle-0").Click();
        details = cut.Find("details.sheet-rule-sublist-details");
        details.HasAttribute("open").Should().BeTrue();
    });

    // Ticket's own requirement: the count shown in the (still-collapsed) summary must reflect
    // whatever the list currently holds, not a value captured at first render.
    [Fact]
    public void SheetRuleSublistDetails_SummaryCount_ReflectsCurrentListSize_NotFirstRenderValue() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ExportProfileEditor>();

            // AddValidSheetRule adds 1 column + 1 point column.
            AddValidSheetRule(cut);

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("1 columns").And.Contain("1 point columns");

            // Add a second column via edit mode, save -- summary count must update.
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-column-header-input").Change("Désignation");
            cut.Find("#edit-0-column-source-select").Change(nameof(PivotFieldRef.EquipementDesignation));
            cut.Find("#edit-0-add-column-definition-button").Click();
            cut.Find("#save-sheet-generation-rule-button-0").Click();

            cut.Find("#sheet-rule-details-toggle-0").TextContent.Should().Contain("2 columns").And.Contain("1 point columns");
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
        cut.Find("#sheet-rule-details-toggle-0").Click();

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
        cut.Find("#sheet-rule-details-toggle-0").Click();

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

    // Functional-parity ticket (Q4): modifying/deleting an already-added SheetGenerationRule,
    // mirroring ImportProfileEditor.razor's SheetRuleForm-based edit-in-place flow exactly.

    [Fact]
    public async Task ExistingSheetRule_DisplaysModifyButton() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.FindAll("#modify-sheet-generation-rule-button-0").Should().HaveCount(1);
        });

    [Fact]
    public async Task ClickingModify_SwitchesOnlyThatRuleIntoEditMode() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.Find("#edit-0-sheet-generation-rule-name-input").GetAttribute("value").Should().Be("Parents");
            cut.FindAll("#modify-sheet-generation-rule-button-1").Should().HaveCount(1);
            cut.FindAll("#edit-1-sheet-generation-rule-name-input").Should().BeEmpty();
        });

    [Fact]
    public async Task EditMode_PrefillsRootFieldsAndColumnsWithExistingRuleValues() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.Find("#edit-0-sheet-generation-rule-name-input").GetAttribute("value").Should().Be("Parents");
            cut.Find("#edit-0-sheet-generation-rule-pivot-source-select").GetAttribute("value").Should().Be(nameof(PivotSource.Equipement));
            cut.Find(".block-field-name").TextContent.Should().Be("Repère");
            cut.Markup.Should().Contain("TRAVAUX COMPLET");
        });

    [Fact]
    public async Task SaveChanges_UpdatesTheRuleInPlace_AndClosesEditMode_WithoutDuplicating() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents modifié");
            cut.Find("#save-sheet-generation-rule-button-0").Click();

            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().BeEmpty();
            cut.FindAll("#modify-sheet-generation-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#modify-sheet-generation-rule-button-1").Should().BeEmpty();
            cut.Markup.Should().Contain("Parents modifié");

            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var rule = all.Single().SheetRules.Should().ContainSingle().Subject;
            rule.SheetName.Should().Be("Parents modifié");
        });

    [Fact]
    public async Task Cancel_ExitsEditMode_WithoutApplyingChanges() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Ne sera jamais sauvegardé");
            cut.Find("#cancel-sheet-generation-rule-button-0").Click();

            cut.FindAll("#edit-0-sheet-generation-rule-name-input").Should().BeEmpty();

            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").GetAttribute("value").Should().Be("Parents");
        });

    [Fact]
    public async Task Delete_FirstClick_DoesNotRemoveTheRule_ShowsConfirmationInstead() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-generation-rule-button-0").Click();

            cut.Markup.Should().Contain("Parents");
            cut.Markup.Should().Contain("Delete this sheet rule? This cannot be undone.");
            cut.FindAll("#confirm-delete-sheet-generation-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#cancel-delete-sheet-generation-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#modify-sheet-generation-rule-button-0").Should().BeEmpty();
        });

    [Fact]
    public async Task Delete_Confirm_RemovesTheRuleFromTheInMemoryList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-generation-rule-button-0").Click();
            cut.Find("#confirm-delete-sheet-generation-rule-button-0").Click();

            cut.Markup.Should().NotContain("Parents");
            cut.Markup.Should().Contain("Enfants");
        });

    [Fact]
    public async Task Delete_Cancel_KeepsTheRuleAndRestoresTheOriginalButtons() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithTwoSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#delete-sheet-generation-rule-button-0").Click();
            cut.Find("#cancel-delete-sheet-generation-rule-button-0").Click();

            cut.Markup.Should().Contain("Parents");
            cut.Markup.Should().Contain("Enfants");
            cut.FindAll("#modify-sheet-generation-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#delete-sheet-generation-rule-button-0").Should().HaveCount(1);
            cut.FindAll("#confirm-delete-sheet-generation-rule-button-0").Should().BeEmpty();
        });

    [Fact]
    public async Task SaveProfile_PersistsEditedSheetRule_VisibleAfterReload() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents modifié");
            cut.Find("#save-sheet-generation-rule-button-0").Click();
            cut.Find("#save-export-profile-button").Click();

            var reloaded = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            reloaded.Markup.Should().Contain("Parents modifié");
        });

    [Fact]
    public async Task AddingNewSheetRule_StillWorks_AfterEditingAnExistingRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();
            cut.Find("#edit-0-sheet-generation-rule-name-input").Change("Parents modifié");
            cut.Find("#save-sheet-generation-rule-button-0").Click();

            AddValidSheetRule(cut);

            cut.FindAll("#modify-sheet-generation-rule-button-1").Should().HaveCount(1);
        });

    // Functional-parity ticket (Q4): modifying/deleting an already-added ColumnDefinition, within
    // the always-present "Add a sheet rule" card -- mirrors ImportProfileEditor.razor's BlockField
    // modify/delete tests.

    [Fact]
    public void Column_AfterAdding_DisplaysModifyAndDeleteButtons() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.FindAll("#modify-column-definition-button-0").Should().HaveCount(1);
        cut.FindAll("#delete-column-definition-button-0").Should().HaveCount(1);
    });

    [Fact]
    public void Column_ClickingModify_PrefillsEditFormWithExistingValues() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#modify-column-definition-button-0").Click();

        cut.Find("#column-0-header-input").GetAttribute("value").Should().Be("Repère");
        cut.Find("#column-0-source-select").GetAttribute("value").Should().Be(nameof(PivotFieldRef.EquipementRepere));
    });

    [Fact]
    public void Column_SaveChanges_UpdatesColumnInPlace_AndClosesEditMode() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#modify-column-definition-button-0").Click();
        cut.Find("#column-0-header-input").Change("Repère modifié");
        cut.Find("#save-column-definition-button-0").Click();

        cut.FindAll("#column-0-header-input").Should().BeEmpty();
        cut.Find(".block-field-name").TextContent.Should().Be("Repère modifié");
        cut.FindAll("#modify-column-definition-button-1").Should().BeEmpty();
    });

    [Fact]
    public void Column_Cancel_DiscardsChanges() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#modify-column-definition-button-0").Click();
        cut.Find("#column-0-header-input").Change("Ne sera jamais sauvegardé");
        cut.Find("#cancel-column-definition-button-0").Click();

        cut.Find(".block-field-name").TextContent.Should().Be("Repère");
    });

    [Fact]
    public void Column_Delete_RemovesColumnFromList() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#column-header-input").Change("Repère");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementRepere));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#column-header-input").Change("Désignation");
        cut.Find("#column-source-select").Change(nameof(PivotFieldRef.EquipementDesignation));
        cut.Find("#add-column-definition-button").Click();

        cut.Find("#delete-column-definition-button-0").Click();

        cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "Désignation");
    });

    [Fact]
    public async Task Column_EditWithinExistingSheetRule_PersistsAfterSavingRuleAndProfile() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await Store.SaveAsync(profile);

            var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-generation-rule-button-0").Click();

            cut.Find("#edit-0-modify-column-definition-button-0").Click();
            cut.Find("#edit-0-column-0-header-input").Change("Repère modifié");
            cut.Find("#edit-0-save-column-definition-button-0").Click();

            cut.Find("#save-sheet-generation-rule-button-0").Click();
            cut.Find("#save-export-profile-button").Click();

            var all = await Store.GetAllAsync();
            var rule = all.Single().SheetRules.Single();
            rule.ColumnDefinitions.Should().ContainSingle(c => c.Header == "Repère modifié" && c.Source == PivotFieldRef.EquipementRepere);
        });

    // Functional-parity ticket (Q4): same modify/delete treatment for PointColumnDefinition.

    [Fact]
    public void PointColumn_AfterAdding_DisplaysModifyAndDeleteButtons() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();

        cut.FindAll("#modify-point-column-definition-button-0").Should().HaveCount(1);
        cut.FindAll("#delete-point-column-definition-button-0").Should().HaveCount(1);
    });

    [Fact]
    public void PointColumn_ClickingModify_PrefillsEditFormWithExistingValues() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();

        cut.Find("#modify-point-column-definition-button-0").Click();

        cut.Find("#point-column-0-nom-input").GetAttribute("value").Should().Be("TRAVAUX COMPLET");
        cut.Find("#point-column-0-header-input").GetAttribute("value").Should().Be("Travaux complet");
        cut.Find("#point-column-0-mark-value-input").GetAttribute("value").Should().Be(PointColumnDefinition.DefaultMarkValue);
    });

    [Fact]
    public void PointColumn_SaveChanges_UpdatesPointColumnInPlace_AndClosesEditMode() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();

        cut.Find("#modify-point-column-definition-button-0").Click();
        cut.Find("#point-column-0-header-input").Change("Travaux complet modifié");
        cut.Find("#save-point-column-definition-button-0").Click();

        cut.FindAll("#point-column-0-header-input").Should().BeEmpty();
        cut.Find(".block-field-name").TextContent.Should().Be("Travaux complet modifié");
        cut.FindAll("#modify-point-column-definition-button-1").Should().BeEmpty();
    });

    [Fact]
    public void PointColumn_Cancel_DiscardsChanges() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();

        cut.Find("#modify-point-column-definition-button-0").Click();
        cut.Find("#point-column-0-header-input").Change("Ne sera jamais sauvegardé");
        cut.Find("#cancel-point-column-definition-button-0").Click();

        cut.Find(".block-field-name").TextContent.Should().Be("Travaux complet");
    });

    [Fact]
    public void PointColumn_Delete_RemovesPointColumnFromList() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();

        cut.Find("#point-column-nom-input").Change("TRAVAUX COMPLET");
        cut.Find("#point-column-header-input").Change("Travaux complet");
        cut.Find("#add-point-column-definition-button").Click();

        cut.Find("#point-column-nom-input").Change("DEPROLOCK VANNES");
        cut.Find("#point-column-header-input").Change("Deprolock vannes");
        cut.Find("#add-point-column-definition-button").Click();

        cut.Find("#delete-point-column-definition-button-0").Click();

        cut.FindAll(".block-field-name").Should().ContainSingle(e => e.TextContent == "Deprolock vannes");
    });

    [Fact]
    public void BackToListButton_NavigatesToProfileList() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>();
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        cut.Find("#back-to-export-profiles-button").Click();

        navigationManager.Uri.Should().EndWith("/export-profiles");
    });

    [Fact]
    public void BackToListButton_IsStillShown_WhenProfileNotFound() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfileEditor>(parameters => parameters.Add(p => p.Id, Guid.NewGuid()));

        cut.FindAll("#back-to-export-profiles-button").Should().HaveCount(1);
    });
}
