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

// Client-reported (2026-09-16): PLATINES' "RECEPTION DEBUT MAD"/"RECEPTION DEBUT REL" field-presence
// rules (seeded since 2026-09-04) "disappeared" from the standard profile. SheetRuleForm rebuilt the
// SheetExtractionRule without them, so editing and saving a PLATINES rule silently deleted both --
// same class of data-loss bug as Lot 048.1 for header rules.
public class ImportProfileEditorFieldPresencePointRuleTests : BunitContext
{
    public ImportProfileEditorFieldPresencePointRuleTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfileEditorFieldPresencePointRuleTests_" + Guid.NewGuid());
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

    private static ImportProfile BuildProfileWithPlatinesFieldPresenceRules()
    {
        var locator = new RepeatingBlockLocator(
            "PLATINES", 17, 8, "Identification",
            [new BlockFieldDefinition("Identification", "B:E", 0, 1)]);
        var rule = new SheetExtractionRule(
            "PLATINES", locator, [], ["POSE ÉTIQUETTES"], [], [],
            fieldPresencePointRules:
            [
                new FieldPresencePointRule(new BlockFieldDefinition("PoseeLe", "H:N", 2, 2), "RECEPTION DEBUT MAD"),
                new FieldPresencePointRule(new BlockFieldDefinition("DeposeeLe", "H:N", 3, 3), "RECEPTION DEBUT REL")
            ],
            couleurEtiquetteCell: new BlockFieldDefinition("CouleurEtiquette", "H:N", 1, 1));

        return new ImportProfile("MAD OXO", "MAD TRAVAUX", [], [], [rule]);
    }

    [Fact]
    public async Task ModifyAndSaveSheetRule_PreservesFieldPresencePointRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#modify-sheet-rule-button-0").Click();
            cut.Find("#edit-0-sheet-rule-allowed-couleurs-etiquette-input").Change("ROUGE, BLANC");
            cut.Find("#save-sheet-rule-button-0").Click();
            cut.Find("#save-profile-button").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            var platines = reloaded!.SheetRules.Single();
            platines.AllowedCouleursEtiquette.Should().Equal("ROUGE", "BLANC");
            platines.FieldPresencePointRules
                .Select(r => (r.ColonneName, r.Cell.ColumnRange, r.Cell.RowOffsetStart, r.Cell.RowOffsetEnd))
                .Should().BeEquivalentTo(new[]
                {
                    ("RECEPTION DEBUT MAD", "H:N", 2, 2),
                    ("RECEPTION DEBUT REL", "H:N", 3, 3)
                });
        });

    [Fact]
    public async Task SaveProfile_WithoutTouchingSheetRules_PreservesFieldPresencePointRules() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithPlatinesFieldPresenceRules();
            await Store.SaveAsync(profile);

            var cut = Render<ImportProfileEditor>(parameters => parameters.Add(p => p.Id, profile.Id));

            cut.Find("#profile-name-input").Change("MAD OXO renamed");
            cut.Find("#save-profile-button").Click();

            var reloaded = await Store.GetByIdAsync(profile.Id);
            reloaded!.SheetRules.Single().FieldPresencePointRules.Should().HaveCount(2);
        });
}
