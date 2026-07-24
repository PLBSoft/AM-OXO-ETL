using System.Globalization;
using Bunit;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class ExportProfilesTests : BunitContext
{
    public ExportProfilesTests()
    {
        var dbContextFactory = new TestDbContextFactory("ExportProfilesTests_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
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

    private async Task SeedProfileAsync(ExportProfile profile)
    {
        var store = Services.GetRequiredService<IExportProfileStore>();
        await store.SaveAsync(profile);
    }

    private static ExportProfile BuildProfileWithOneSheetRule(string name = "MAD OXO export") =>
        new(name,
            [
                new SheetGenerationRule(
                    "Parents",
                    PivotSource.Equipement,
                    [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)],
                    [],
                    [])
            ]);

    [Fact]
    public async Task ExportProfiles_WithExistingProfile_DisplaysNameAndSheetCount() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ExportProfiles>();

            cut.Markup.Should().Contain("MAD OXO export");
            cut.Markup.Should().Contain("1 sheet rule(s)");
        });

    // V1: same client-reported bug as ImportProfilesTests -- the <th> column header reused the
    // templated ExportProfiles_SheetCount resx value with no count argument, literally rendering
    // "{0} sheet rule(s)" as the header text. Fixed via a dedicated plain-text
    // ExportProfiles_SheetCountHeader key ("Sheet rules"); the data cell's own interpolation was
    // already correct.
    [Fact]
    public async Task ExportProfiles_WithMultipleSheetRules_InterpolatesActualCount_NotLiteralPlaceholder() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = new ExportProfile(
                "MAD OXO export multi",
                [
                    new SheetGenerationRule("Parents", PivotSource.Equipement, [new ColumnDefinition("Repère", PivotFieldRef.EquipementRepere)], [], []),
                    new SheetGenerationRule("Enfants", PivotSource.Isolement, [new ColumnDefinition("Repère", PivotFieldRef.IsolementRepere)], [], []),
                    new SheetGenerationRule("Taches", PivotSource.TacheMultiple, [new ColumnDefinition("Action", PivotFieldRef.TacheMultipleAction)], [], [])
                ]);
            await SeedProfileAsync(profile);

            var cut = Render<ExportProfiles>();

            cut.Markup.Should().Contain("3 sheet rule(s)");
            cut.Markup.Should().NotContain("{0}");
        });

    [Fact]
    public async Task ExportProfiles_WithExistingProfile_AndFrenchCulture_DisplaysFrenchLabels() =>
        await WithCultureAsync("fr-FR", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ExportProfiles>();

            cut.Markup.Should().Contain("Profils d'export");
            cut.Markup.Should().Contain("1 règle(s) de feuille");
        });

    [Fact]
    public void ExportProfiles_WithNoProfiles_DisplaysNoEntriesMessage() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfiles>();

        cut.Markup.Should().Contain("No export profiles have been created yet.");
    });

    [Fact]
    public void CreateButton_NavigatesToNewProfileRoute() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfiles>();
        var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        cut.Find("#create-export-profile-button").Click();

        navigationManager.Uri.Should().EndWith("/export-profiles/new");
    });

    [Fact]
    public void TestProfileButton_NavigatesToTestRoute() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfiles>();
        var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        cut.Find("#test-export-profile-button").Click();

        navigationManager.Uri.Should().EndWith("/export-profiles/test");
    });

    [Fact]
    public async Task EditButton_NavigatesToEditRouteWithProfileId() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await SeedProfileAsync(profile);

            var cut = Render<ExportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

            cut.Find($"#edit-export-profile-button-{profile.Id}").Click();

            navigationManager.Uri.Should().EndWith($"/export-profiles/{profile.Id}/edit");
        });

    [Fact]
    public async Task DuplicateButton_CreatesSuffixedCopyWithNewId_AndRefreshesListWithoutNavigating() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO export");
            await SeedProfileAsync(original);

            var cut = Render<ExportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
            var uriBeforeDuplicate = navigationManager.Uri;

            cut.Find($"#duplicate-export-profile-button-{original.Id}").Click();

            cut.Markup.Should().Contain("MAD OXO export (Copy)");
            navigationManager.Uri.Should().Be(uriBeforeDuplicate);

            var store = Services.GetRequiredService<IExportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(2);
            all.Should().Contain(p => p.Name == "MAD OXO export (Copy)" && p.Id != original.Id);
            all.Single(p => p.Id != original.Id).SheetRules.Should().BeEquivalentTo(original.SheetRules);
        });
}
