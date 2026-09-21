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

// Client ticket J2M76 (2026-09-21): conditional point rules configured on PLATINES, a sheet that applies
// none, with nothing in the editor to say so. The editor now warns, section by section, about what the
// current sheet doesn't use (SheetRuleIgnoredSettings), and the read-only card flags a rule holding such a
// setting.
public class ImportProfileEditorIgnoredSettingsTests : BunitContext
{
    public ImportProfileEditorIgnoredSettingsTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorIgnoredSettingsTests_" + Guid.NewGuid());
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

    private static void WithCulture(string cultureName, Action action) =>
        WithCultureAsync(cultureName, () => { action(); return Task.CompletedTask; }).GetAwaiter().GetResult();

    private static RepeatingBlockLocator Locator(string sheet, string stopField = "Identification") => new(
        sheet, 17, 8, stopField,
        [
            new BlockFieldDefinition("Identification", "B:E", 0, 1), new BlockFieldDefinition("Designation", "H:V", -1, 0),
            new BlockFieldDefinition("TypeElement", "B:E", 3, 5)
        ]);

    // What the client configured (screenshots of ticket J2M76).
    // Lot 084.1: the rule's field must now be declared in the block, as the client did (optional here).
    private static SheetExtractionRule PlatinesWithConditionalRules() => new(
        "PLATINES",
        new RepeatingBlockLocator("PLATINES", 17, 8, "Identification",
        [
            .. Locator("PLATINES").Fields,
            new BlockFieldDefinition("HasDebMad", "H:N", 2, 2, isRequired: false)
        ]),
        [new ConditionalPointRule("HasDebMad", ConditionOperator.Equals, "DEBUT MAD", "DEB MAD RÉCEPTION PLATINES/TAMPONS PLEINS")],
        ["POSE ÉTIQUETTES"], [], []);

    private async Task<IRenderedComponent<ImportProfileEditor>> RenderWithRuleAsync(SheetExtractionRule rule)
    {
        var profile = new ImportProfile("Profil", "MAD TRAVAUX", [], [], [rule]);
        await Store.SaveAsync(profile);
        return Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));
    }

    // Client ticket J2M76, since lot 084.6: PLATINES applies conditional rules -- no warning any more.
    [Fact]
    public async Task ConditionalRulesOnPlatines_NoLongerWarn() =>
        await WithCultureAsync("fr-FR", async () =>
        {
            var cut = await RenderWithRuleAsync(PlatinesWithConditionalRules());

            cut.FindAll("#sheet-rule-ignored-settings-warning-0").Should().BeEmpty();
            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.FindAll("#edit-0-point-rules-ignored-warning").Should().BeEmpty();
            cut.FindAll("#edit-0-header-fields-ignored-warning").Should().BeEmpty();
        });

    [Fact]
    public async Task SheetRuleCard_FlagsARuleHoldingIgnoredSettings() =>
        await WithCultureAsync("fr-FR", async () =>
        {
            var cut = await RenderWithRuleAsync(new SheetExtractionRule(
                "PLATINES", Locator("PLATINES"), [], ["POSE ÉTIQUETTES"], [], [],
                fieldPresencePointRules: [new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "DEB MAD")]));

            var warning = cut.Find("#sheet-rule-ignored-settings-warning-0");
            warning.ClassList.Should().Contain(["alert", "alert-warning"]);
            warning.TextContent.Should().Contain("« PLATINES »");
        });

    [Fact]
    public async Task SheetRuleCard_ShowsNoWarning_WhenEverySettingIsUsed() =>
        await WithCultureAsync("fr-FR", async () =>
        {
            var cut = await RenderWithRuleAsync(new SheetExtractionRule("PLATINES", Locator("PLATINES"), [], ["POSE ÉTIQUETTES"], [], []));

            cut.FindAll("#sheet-rule-ignored-settings-warning-0").Should().BeEmpty();
        });

    // Lot 084.6: an element sheet reads headers and conditional rules; only the "filled cell" section,
    // read by no sheet any more (removed in 84.8), still warns.
    [Fact]
    public async Task Isolement_WarnsOnlyOnTheFilledCellSection() =>
        await WithCultureAsync("en-US", async () =>
        {
            var cut = await RenderWithRuleAsync(new SheetExtractionRule("ISOLEMENT", Locator("ISOLEMENT"), [], [], [], []));

            cut.Find("#modify-sheet-rule-button-0").Click();

            cut.FindAll("#edit-0-header-fields-ignored-warning").Should().BeEmpty();
            cut.FindAll("#edit-0-header-composites-ignored-warning").Should().BeEmpty();
            cut.FindAll("#edit-0-field-presence-rules-ignored-warning").Should().ContainSingle();
            cut.FindAll("#edit-0-point-rules-ignored-warning").Should().BeEmpty();
        });

    [Fact]
    public void AddForm_WarningsFollowTheSheetNameAsItIsTyped() => WithCulture("fr-FR", () =>
    {
        var cut = Render<ImportProfileEditor>();

        cut.FindAll("#point-rules-ignored-warning").Should().BeEmpty();

        cut.Find("#sheet-rule-name-input").Change("PLATINES");
        cut.FindAll("#point-rules-ignored-warning").Should().BeEmpty();
        cut.FindAll("#field-presence-rules-ignored-warning").Should().ContainSingle();
        cut.FindAll("#sheet-not-processed-warning").Should().BeEmpty();

        cut.Find("#sheet-rule-name-input").Change("PLATINE");
        cut.Find("#sheet-not-processed-warning").TextContent.Should().Contain("« PLATINE »").And.Contain("PLATINES");
        cut.FindAll("#field-presence-rules-ignored-warning").Should().BeEmpty();
    });

    [Fact]
    public void GeneralSettings_WarnOnlyWhenFilledInAndIgnored() => WithCulture("fr-FR", () =>
    {
        var cut = Render<ImportProfileEditor>();
        cut.Find("#sheet-rule-name-input").Change("PROCEDURE");

        cut.FindAll("#stop-field-ignored-warning").Should().BeEmpty();
        cut.FindAll("#couleur-etiquette-ignored-warning").Should().BeEmpty();

        cut.Find("#sheet-rule-stop-field-name-input").Change("Ordre");
        cut.Find("#stop-field-ignored-warning").TextContent.Should().Contain("« Action »");

        cut.Find("#sheet-rule-default-couleur-etiquette-input").Change("BLEUE");
        cut.FindAll("#couleur-etiquette-ignored-warning").Should().ContainSingle();

        cut.Find("#sheet-rule-name-input").Change("PLATINES");
        cut.Find("#sheet-rule-zero-energie-expected-value-input").Change("ZERO ENERGIE");
        cut.FindAll("#zero-energie-ignored-warning").Should().ContainSingle();
        cut.FindAll("#couleur-etiquette-ignored-warning").Should().BeEmpty();

        // Lot 084.6 (G10): the dedicated cell is read by no sheet any more; the colour comes from a block field.
        cut.Find("#sheet-rule-couleur-etiquette-cell-input").Change("H18:N18");
        cut.FindAll("#couleur-etiquette-cell-ignored-warning").Should().ContainSingle();

        cut.Find("#block-field-name-input").Change("CouleurEtiquette");
        cut.FindAll("#default-couleur-ignored-warning").Should().ContainSingle();

        cut.Find("#block-field-name-input").Change("");
        cut.Find("#sheet-rule-allowed-couleurs-etiquette-input").Change("ROUGE, BLANC");
        cut.FindAll("#allowed-couleurs-ignored-warning").Should().ContainSingle();
    });
}
