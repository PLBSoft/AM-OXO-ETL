using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class ImportProfileEditorTests : BunitContext
{
    public ImportProfileEditorTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
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

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    // Fills every field needed to pass the RepeatingBlockLocator/SheetExtractionRule build-up
    // (one block field, one unconditional colonne, one conditional point rule) and clicks
    // "add sheet rule", leaving the render handle positioned after the add.
    private static void AddValidSheetRule(IRenderedComponent<ImportProfileEditor> cut)
    {
        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("7");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-column-range-input").Change("B:E");
        cut.Find("#block-field-row-offset-start-input").Change("0");
        cut.Find("#block-field-row-offset-end-input").Change("0");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#unconditional-colonne-name-input").Change("PROLOCK VANNES");
        cut.Find("#add-unconditional-colonne-button").Click();

        cut.Find("#point-rule-colonne-name-input").Change("ZÉRO ENERGIE...");
        cut.Find("#point-rule-source-field-name-input").Change("TypeElement");
        cut.Find("#point-rule-comparison-value-input").Change("ZERO ENERGIE");
        cut.Find("#add-point-rule-button").Click();

        cut.Find("#add-sheet-rule-button").Click();
    }

    [Fact]
    public void NewProfile_PrefillsReperePrefixWithDefault_AndLeavesEquipementTypeElementNomEmpty() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#profile-repere-prefix-input").GetAttribute("value").Should().Be(ImportProfile.DefaultReperePrefix);
            cut.Find("#profile-equipement-type-element-nom-input").GetAttribute("value").Should().BeNullOrEmpty();
        });

    [Fact]
    public async Task Save_WithEmptyName_DisplaysLocalizedErrorAndDoesNotPersist() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();

            cut.Find("#save-profile-button").Click();

            cut.Markup.Should().Contain("Name must not be empty.");

            var all = await Store.GetAllAsync();
            all.Should().BeEmpty();
        });

    [Fact]
    public void Save_WithEmptyReperePrefix_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-repere-prefix-input").Change(string.Empty);

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Repere prefix must not be empty.");
    });

    [Fact]
    public void Save_WithEmptyEquipementTypeElementNom_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Equipement type element nom must not be empty.");
    });

    [Fact]
    public void Save_WithNoSheetRulesAdded_DisplaysLocalizedError() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#profile-name-input").Change("MAD OXO");
        cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

        cut.Find("#save-profile-button").Click();

        cut.Markup.Should().Contain("Sheet rules must contain at least one rule.");
    });

    [Fact]
    public void AddSheetRule_WithValidInput_DisplaysSheetSummaryAndResetsForm() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        AddValidSheetRule(cut);

        cut.Markup.Should().Contain("ISOLEMENT");
        cut.Markup.Should().Contain("PROLOCK VANNES");
        cut.Markup.Should().Contain("ZÉRO ENERGIE...");
        cut.Markup.Should().Contain("TypeElement");
        cut.Find("#sheet-rule-name-input").GetAttribute("value").Should().BeNullOrEmpty();
    });

    [Fact]
    public void AddSheetRule_WithNonPositiveStep_DisplaysLocalizedErrorAndDoesNotAddSheet() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
        cut.Find("#sheet-rule-first-block-start-row-input").Change("9");
        cut.Find("#sheet-rule-step-input").Change("0");
        cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");

        cut.Find("#block-field-name-input").Change("Identification");
        cut.Find("#block-field-column-range-input").Change("B:E");
        cut.Find("#add-block-field-button").Click();

        cut.Find("#add-sheet-rule-button").Click();

        cut.Markup.Should().Contain("Step must be positive.");
        cut.Markup.Should().Contain("No sheet rules added yet.");
    });

    [Fact]
    public async Task Save_WithValidRootFieldsAndOneAddedSheetRule_PersistsProfileAndNavigatesToList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            var navigationManager = Services.GetRequiredService<NavigationManager>();

            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");
            AddValidSheetRule(cut);

            cut.Find("#save-profile-button").Click();

            navigationManager.Uri.Should().EndWith("/import-profiles");

            var all = await Store.GetAllAsync();
            all.Should().ContainSingle();
            var saved = all.Single();
            saved.Name.Should().Be("MAD OXO");
            saved.EquipementTypeElementNom.Should().Be("MAD TRAVAUX");
            saved.ReperePrefix.Should().Be(ImportProfile.DefaultReperePrefix);
            saved.SheetRules.Should().ContainSingle();

            var rule = saved.SheetRules.Single();
            rule.SheetName.Should().Be("ISOLEMENT");
            rule.Locator.FirstBlockStartRow.Should().Be(9);
            rule.Locator.Step.Should().Be(7);
            rule.Locator.StopFieldName.Should().Be("Identification");
            rule.Locator.Fields.Should().ContainSingle(f => f.Name == "Identification" && f.ColumnRange == "B:E");
            rule.UnconditionalColonneNames.Should().Equal("PROLOCK VANNES");
            rule.PointRules.Should().ContainSingle(r => r.ColonneName == "ZÉRO ENERGIE..." && r.ComparisonValue == "ZERO ENERGIE");
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
