using System.Globalization;
using Bunit;
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

public class ImportProfilesTests : BunitContext
{
    public ImportProfilesTests()
    {
        var dbContextFactory = new TestDbContextFactory("ImportProfilesTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
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

    private async Task SeedProfileAsync(ImportProfile profile)
    {
        var store = Services.GetRequiredService<IImportProfileStore>();
        await store.SaveAsync(profile);
    }

    private static ImportProfile BuildProfileWithOneSheetRule(
        string name = "MAD OXO", string equipementTypeElementNom = "MAD TRAVAUX")
    {
        var locator = new RepeatingBlockLocator(
            "ISOLEMENT",
            firstBlockStartRow: 9,
            step: 7,
            stopFieldName: "Identification",
            fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);

        var sheetRule = new SheetExtractionRule(
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"]);

        return new ImportProfile(name, equipementTypeElementNom, [], [], [sheetRule]);
    }

    [Fact]
    public async Task ImportProfiles_WithExistingProfile_DisplaysNameEquipementTypeElementNomAndSheetCount() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ImportProfiles>();

            cut.Markup.Should().Contain("MAD OXO");
            cut.Markup.Should().Contain("MAD TRAVAUX");
            cut.Markup.Should().Contain("1 sheet rule(s)");
        });

    [Fact]
    public async Task ImportProfiles_WithExistingProfile_AndFrenchCulture_DisplaysFrenchLabels() =>
        await WithCultureAsync("fr-FR", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ImportProfiles>();

            cut.Markup.Should().Contain("Profils d'import");
            cut.Markup.Should().Contain("1 règle(s) de feuille");
        });

    [Fact]
    public void ImportProfiles_WithNoProfiles_DisplaysNoEntriesMessage() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfiles>();

        cut.Markup.Should().Contain("No import profiles have been created yet.");
    });

    [Fact]
    public void CreateButton_NavigatesToNewProfileRoute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfiles>();
        var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        cut.Find("#create-profile-button").Click();

        navigationManager.Uri.Should().EndWith("/import-profiles/new");
    });

    [Fact]
    public void TestProfileButton_NavigatesToTestRoute() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfiles>();
        var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        cut.Find("#test-import-profile-button").Click();

        navigationManager.Uri.Should().EndWith("/import-profiles/test");
    });

    [Fact]
    public async Task EditButton_NavigatesToEditRouteWithProfileId() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await SeedProfileAsync(profile);

            var cut = Render<ImportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

            cut.Find($"#edit-profile-button-{profile.Id}").Click();

            navigationManager.Uri.Should().EndWith($"/import-profiles/{profile.Id}/edit");
        });

    [Fact]
    public async Task DuplicateButton_CreatesSuffixedCopyWithNewId_AndRefreshesListWithoutNavigating() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO");
            await SeedProfileAsync(original);

            var cut = Render<ImportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
            var uriBeforeDuplicate = navigationManager.Uri;

            cut.Find($"#duplicate-profile-button-{original.Id}").Click();

            cut.Markup.Should().Contain("MAD OXO (Copy)");
            navigationManager.Uri.Should().Be(uriBeforeDuplicate);

            var store = Services.GetRequiredService<IImportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(2);
            all.Should().Contain(p => p.Name == "MAD OXO (Copy)" && p.Id != original.Id);
        });
}
