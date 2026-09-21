using System.Globalization;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

// Lot 084.5 (docs/tickets/tickets-tdd-lot-084-moteur-generique-feuilles-elements.md): the three new
// element-sheet settings in the import editor -- "required" per block field, the closed list of source
// fields with the IsNotBlank operator, and the per-sheet warning check box.
public class ImportProfileEditorLot084Tests : BunitContext
{
    public ImportProfileEditorLot084Tests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorLot084Tests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();
    }

    private IImportProfileStore Store => Services.GetRequiredService<IImportProfileStore>();

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { await action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private async Task<IRenderedComponent<ImportProfileEditor>> RenderExistingProfileAsync(string sheetName = "PLATINES")
    {
        var locator = new RepeatingBlockLocator(
            firstBlockStartRow: 17, step: 8,
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("TypeElement", "B:E", 3, 5)]);
        var rule = new SheetExtractionRule(
            sheetName, locator,
            [new ConditionalPointRule("TypeElement", ConditionOperator.NotEquals, "TUBING", "POSE")], [], [], []);
        var profile = new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [rule]);
        await Store.SaveAsync(profile);
        var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
        cut.Find("#modify-sheet-rule-button-0").Click();
        return cut;
    }

    private async Task<SheetExtractionRule> SaveAndReloadRuleAsync(IRenderedComponent<ImportProfileEditor> cut)
    {
        cut.Find("#save-profile-button").Click();
        return (await Store.GetAllAsync()).Should().ContainSingle().Subject.SheetRules.Single();
    }

    [Fact]
    public async Task NewBlockField_RequiredCheckbox_IsUncheckedByDefault_SoTheFieldIsSavedOptional() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();

            cut.Find("#edit-0-block-field-is-required-checkbox").HasAttribute("checked").Should().BeFalse();
            cut.Find("#edit-0-block-field-name-input").Change("HasDebMad");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H19:N19");
            cut.Find("#edit-0-add-block-field-button").Click();

            cut.Find("#edit-0-block-field-optional-badge-2").Should().NotBeNull();
            var rule = await SaveAndReloadRuleAsync(cut);
            rule.Locator.Fields.Single(f => f.Name == "HasDebMad").IsRequired.Should().BeFalse();
            rule.Locator.Fields.Single(f => f.Name == "Identification").IsRequired.Should().BeTrue();
        });

    [Fact]
    public async Task NewBlockField_CheckingRequired_SavesItRequired() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();

            cut.Find("#edit-0-block-field-name-input").Change("Designation");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H16:V17");
            cut.Find("#edit-0-block-field-is-required-checkbox").Change(true);
            cut.Find("#edit-0-add-block-field-button").Click();

            cut.FindAll("#edit-0-block-field-optional-badge-2").Should().BeEmpty();
            (await SaveAndReloadRuleAsync(cut)).Locator.Fields.Single(f => f.Name == "Designation").IsRequired.Should().BeTrue();
        });

    [Fact]
    public async Task SourceField_IsAClosedListOfTheBlockFields_IncludingThePendingOne() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#edit-0-block-field-name-input").Change("HasDebMad");

            var options = cut.FindAll("#edit-0-point-rule-source-field-name-input option").Select(o => o.GetAttribute("value"));

            options.Should().Equal("", "Identification", "TypeElement", "HasDebMad");
        });

    [Fact]
    public async Task SourceField_NoLongerAmongTheBlockFields_IsShownAsUnknown_AndNotLostOnSave() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#edit-0-delete-block-field-button-1").Click();
            cut.Find("#edit-0-edit-conditional-point-rule-button-0").Click();

            var select = cut.Find("#edit-0-conditional-point-rule-edit-source-field-input-0");
            select.GetAttribute("value").Should().Be("TypeElement");
            select.QuerySelectorAll("option").Select(o => o.TextContent).Should().Contain("TypeElement (unknown)");

            cut.Find("#save-profile-button").Click();

            // Refused by the domain (lot 084.1), never silently dropped.
            (await Store.GetAllAsync()).Single().SheetRules.Single().PointRules.Single().SourceFieldName.Should().Be("TypeElement");
            cut.Markup.Should().Contain("TypeElement");
        });

    [Fact]
    public async Task IsNotBlank_HidesAndClearsTheComparisonValue_AndSavesARuleWithoutValue() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();
            cut.Find("#edit-0-block-field-name-input").Change("HasDebMad");
            cut.Find("#edit-0-block-field-absolute-range-input").Change("H19:N19");
            cut.Find("#edit-0-add-block-field-button").Click();

            cut.Find("#edit-0-point-rule-colonne-name-input").Change("RECEPTION DEBUT MAD");
            cut.Find("#edit-0-point-rule-source-field-name-input").Change("HasDebMad");
            cut.Find("#edit-0-point-rule-comparison-value-input").Change("DEBUT MAD");
            cut.Find("#edit-0-point-rule-operator-select").Change("IsNotBlank");

            cut.FindAll("#edit-0-point-rule-comparison-value-input").Should().BeEmpty();
            cut.Find("#edit-0-add-point-rule-button").Click();

            var saved = (await SaveAndReloadRuleAsync(cut)).PointRules.Single(r => r.ColonneName == "RECEPTION DEBUT MAD");
            saved.Operator.Should().Be(ConditionOperator.IsNotBlank);
            saved.ComparisonValue.Should().BeNull();
        });

    [Fact]
    public async Task WarningCheckbox_IsSavedOnTheSheetRule() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync();

            cut.Find("#edit-0-sheet-rule-warn-when-no-conditional-point-checkbox").Change(true);

            (await SaveAndReloadRuleAsync(cut)).WarnWhenNoConditionalPoint.Should().BeTrue();
        });

    [Fact]
    public async Task Procedure_ShowsNeitherTheRequiredNorTheWarningCheckbox() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync("PROCEDURE");

            cut.FindAll("#edit-0-block-field-is-required-checkbox").Should().BeEmpty();
            cut.FindAll("#edit-0-block-field-0-is-required-checkbox").Should().BeEmpty();
            cut.FindAll("#edit-0-sheet-rule-warn-when-no-conditional-point-checkbox").Should().BeEmpty();
        });

    [Fact]
    public async Task ElementSheet_ShowsBothCheckboxes() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderExistingProfileAsync("ISOLEMENT");

            cut.FindAll("#edit-0-block-field-is-required-checkbox").Should().ContainSingle();
            cut.FindAll("#edit-0-sheet-rule-warn-when-no-conditional-point-checkbox").Should().ContainSingle();
        });
}
