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

// Lot 063 (63.7): ZeroEnergieExpectedValue exposed and editable in the import profile editor -- kept
// in its own file per this project's established convention (mirrors ImportProfileEditorLot056Tests.cs
// and siblings).
public class ImportProfileEditorLot063Tests : BunitContext
{
    public ImportProfileEditorLot063Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot063Tests_" + Guid.NewGuid());
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

    private static ImportProfile BuildProfileWithIsolementAndProcedureSheetRules(string name = "MAD OXO")
    {
        var isolementLocator = new RepeatingBlockLocator(
            "ISOLEMENT", 19, 7, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("HasZeroEnergie", "V", -1, 0)]);
        var isolementRule = new SheetExtractionRule(
            "ISOLEMENT", isolementLocator,
            [new ConditionalPointRule("HasZeroEnergie", ConditionOperator.Equals, "true", "ZÉRO ENERGIE EN PRESENCE EE (PS941)")],
            ["PROLOCK VANNES"], [], [], zeroEnergieExpectedValue: "ZERO ENERGIE");

        var procedureLocator = new RepeatingBlockLocator(
            "PROCEDURE", 9, 1, "Action", [new BlockFieldDefinition("Action", "C:L", 0, 0)]);
        var procedureRule = new SheetExtractionRule("PROCEDURE", procedureLocator, [], [], [], []);

        return new ImportProfile(name, "MAD TRAVAUX", [], [], [isolementRule, procedureRule]);
    }

    private static void OpenAddSheetRuleFormIfClosed(IRenderedComponent<ImportProfileEditor> cut)
    {
        if (cut.FindAll("#sheet-rule-name-input").Count == 0)
        {
            cut.Find("#toggle-add-sheet-rule-form-button").Click();
        }
    }

    [Fact]
    public void AddSheetRuleForm_ZeroEnergieExpectedValueField_HasAssociatedLabel() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        OpenAddSheetRuleFormIfClosed(cut);

        var label = cut.Find("label[for='sheet-rule-zero-energie-expected-value-input']");
        label.TextContent.Should().NotBeNullOrEmpty();
    });

    [Fact]
    public async Task AddSheetRule_WithZeroEnergieExpectedValue_RoundTripsThroughTheStore() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#sheet-rule-zero-energie-expected-value-input").Change("ZERO ENERGIE");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B19:E20");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.SheetRules.Single().ZeroEnergieExpectedValue.Should().Be("ZERO ENERGIE");
        });

    [Fact]
    public async Task AddSheetRule_WithZeroEnergieExpectedValueLeftEmpty_PersistsAsNullNotEmptyString() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = Render<ImportProfileEditor>();
            cut.Find("#profile-name-input").Change("MAD OXO");
            cut.Find("#profile-repere-prefix-input").Change("MAD-OXO-");
            cut.Find("#profile-equipement-type-element-nom-input").Change("MAD TRAVAUX");

            OpenAddSheetRuleFormIfClosed(cut);
            cut.Find("#sheet-rule-name-input").Change("ISOLEMENT");
            cut.Find("#sheet-rule-first-block-start-row-input").Change("19");
            cut.Find("#sheet-rule-step-input").Change("7");
            cut.Find("#sheet-rule-stop-field-name-input").Change("Identification");
            cut.Find("#block-field-name-input").Change("Identification");
            cut.Find("#block-field-absolute-range-input").Change("B19:E20");
            cut.Find("#add-block-field-button").Click();
            cut.Find("#add-sheet-rule-button").Click();

            cut.Find("#save-profile-button").Click();

            var all = await Store.GetAllAsync();
            var reloaded = all.Should().ContainSingle().Subject;
            reloaded.SheetRules.Single().ZeroEnergieExpectedValue.Should().BeNull();
        });

    [Fact]
    public async Task ModifySheetRule_PrefillsZeroEnergieExpectedValue_AndCancelRestoresOriginalWithoutPersisting() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementAndProcedureSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.Find("#edit-0-sheet-rule-zero-energie-expected-value-input").GetAttribute("value").Should().Be("ZERO ENERGIE");

            cut.Find("#edit-0-sheet-rule-zero-energie-expected-value-input").Change("0 ENERGIE");
            cut.Find("#cancel-sheet-rule-button-0").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.SheetRules.Single(r => r.SheetName == "ISOLEMENT").ZeroEnergieExpectedValue.Should().Be("ZERO ENERGIE");
        });

    [Fact]
    public async Task Summary_DisplaysZeroEnergieExpectedValue_OnlyForTheSheetThatConfiguresIt() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithIsolementAndProcedureSheetRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Markup.Should().Contain("ZERO ENERGIE");
            var cards = cut.FindAll(".sheet-rule-card");
            cards.Should().HaveCount(2);

            var isolementCardMarkup = cards[0].OuterHtml;
            var procedureCardMarkup = cards[1].OuterHtml;
            isolementCardMarkup.Should().Contain("ZERO ENERGIE");
            procedureCardMarkup.Should().NotContain("ZERO ENERGIE");
        });

    [Fact]
    public void AddSheetRuleForm_FillingOnlyZeroEnergieExpectedValue_MarksProfileAsChanged() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfileEditor>();
        OpenAddSheetRuleFormIfClosed(cut);

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeTrue();

        cut.Find("#sheet-rule-zero-energie-expected-value-input").Change("ZERO ENERGIE");

        cut.Find("#save-profile-button").HasAttribute("disabled").Should().BeFalse();
    });
}
