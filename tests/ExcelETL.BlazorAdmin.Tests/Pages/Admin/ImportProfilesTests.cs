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

    // V1: client-reported bug -- the sheet-rules-count column literally showed "{0} sheet rule(s)".
    // Root cause: the <th> COLUMN HEADER reused Loc["ImportProfiles_SheetCount"] (the templated,
    // "{0} sheet rule(s)" resx value) with no count argument -- the data cell itself was already
    // correctly interpolated via Loc["ImportProfiles_SheetCount", count]. Fixed by giving the header
    // its own plain-text key, ImportProfiles_SheetCountHeader ("Sheet rules"). This test asserts on
    // the full markup (header + data) so a regression either way would be caught.
    [Fact]
    public async Task ImportProfiles_WithMultipleSheetRules_InterpolatesActualCount_NotLiteralPlaceholder() =>
        await WithCultureAsync("en-US", async () =>
        {
            static RepeatingBlockLocator BuildLocator(string sheet) => new(
                sheet, firstBlockStartRow: 9, step: 7, stopFieldName: "Identification",
                fields: [new BlockFieldDefinition("Identification", "B:E", 0, 0)]);
            var profile = new ImportProfile(
                "MAD OXO multi", "MAD TRAVAUX", [], [],
                [
                    new SheetExtractionRule("ISOLEMENT", BuildLocator("ISOLEMENT"), pointRules: [], unconditionalColonneNames: ["A"]),
                    new SheetExtractionRule("PLATINES", BuildLocator("PLATINES"), pointRules: [], unconditionalColonneNames: ["B"]),
                    new SheetExtractionRule("DIVERS", BuildLocator("DIVERS"), pointRules: [], unconditionalColonneNames: ["C"])
                ]);
            await SeedProfileAsync(profile);

            var cut = Render<ImportProfiles>();

            cut.Markup.Should().Contain("3 sheet rule(s)");
            cut.Markup.Should().NotContain("{0}");
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

    // V2: mobile-first table -> card fallback at the md (768px) breakpoint. bUnit doesn't compute
    // real layout, so these tests assert on the responsive classes and on both templates being
    // present in the DOM simultaneously (one hidden below md, the other above, via CSS only).
    [Fact]
    public async Task ImportProfiles_RendersBothTableAndCardTemplates_WithResponsiveClasses() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ImportProfiles>();

            var table = cut.Find("table.table");
            table.ClassList.Should().Contain("d-none");
            table.ClassList.Should().Contain("d-md-table");

            var cardContainer = cut.Find("div.d-md-none");
            cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
        });

    [Fact]
    public async Task ImportProfiles_CardTemplate_DisplaysSameContentAsTable() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await SeedProfileAsync(profile);

            var cut = Render<ImportProfiles>();

            var card = cut.Find("div.d-md-none .card");
            card.TextContent.Should().Contain("MAD OXO");
            card.TextContent.Should().Contain("MAD TRAVAUX");
            card.TextContent.Should().Contain("1 sheet rule(s)");

            card.QuerySelector($"#edit-profile-button-card-{profile.Id}").Should().NotBeNull();
            card.QuerySelector($"#duplicate-profile-button-card-{profile.Id}").Should().NotBeNull();
        });

    // V4: the two header action buttons stack full-width on mobile via the "d-grid gap-2 d-md-flex"
    // idiom on their shared wrapper (CSS Grid items stretch to fill by default below md; from md up,
    // d-md-flex switches back to inline flex where buttons keep their natural side-by-side width).
    [Fact]
    public void ImportProfiles_HeaderButtons_ShareResponsiveStackingWrapper() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfiles>();

        var testButton = cut.Find("#test-import-profile-button");
        var createButton = cut.Find("#create-profile-button");
        testButton.ParentElement.Should().BeSameAs(createButton.ParentElement);

        var wrapper = testButton.ParentElement!;
        wrapper.ClassList.Should().Contain("d-grid");
        wrapper.ClassList.Should().Contain("gap-2");
        wrapper.ClassList.Should().Contain("d-md-flex");
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
