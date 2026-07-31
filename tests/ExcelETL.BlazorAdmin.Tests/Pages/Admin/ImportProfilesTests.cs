using System.Globalization;
using Bunit;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.BlazorAdmin.Tests.Layout;
using ExcelETL.BlazorAdmin.Tests.Pages;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
            "ISOLEMENT", locator, pointRules: [], unconditionalColonneNames: ["PROLOCK VANNES"], [], []);

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

    // Lot 042 (42.2): the mobile card's per-row title previously skipped straight from h1 to h5 --
    // fixed to h2, keeping its pre-existing visual size via the Bootstrap `.h5` utility class.
    [Fact]
    public async Task ImportProfiles_WithExistingProfile_HasNoHeadingLevelSkip() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ImportProfiles>();

            HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
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
                    new SheetExtractionRule("ISOLEMENT", BuildLocator("ISOLEMENT"), pointRules: [], unconditionalColonneNames: ["A"], [], []),
                    new SheetExtractionRule("PLATINES", BuildLocator("PLATINES"), pointRules: [], unconditionalColonneNames: ["B"], [], []),
                    new SheetExtractionRule("DIVERS", BuildLocator("DIVERS"), pointRules: [], unconditionalColonneNames: ["C"], [], [])
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
    public void ImportProfiles_DisplaysNonTechnicalIntroText() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfiles>();

        var intro = cut.Find("#import-profiles-intro");
        intro.ClassList.Should().Contain("alert").And.Contain("alert-info");
        intro.TextContent.Should().NotBeNullOrWhiteSpace();
    });

    // Lot 041 (41.2): the "Créer un profil" CTA was one of the audit's flagged icon-less CTAs.
    [Fact]
    public void CreateProfileButton_HasIcon() => WithCulture("en-US", () =>
    {
        var cut = Render<ImportProfiles>();

        cut.Find("#create-profile-button").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
    });

    // Lot 030 (30.7): reopens V4/X2's mobile stacking decision on explicit client request -- the
    // two buttons now share the row's width equally (flex-fill) instead of each going full-width
    // on its own line.
    [Fact]
    public void HeaderActionButtons_ShareWidthEqually_AndAreNoLongerIndividuallyFullWidth() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfiles>();

            cut.Find("#test-import-profile-button").ClassList.Should().Contain("flex-fill");
            cut.Find("#test-import-profile-button").ClassList.Should().NotContain("w-100");
            cut.Find("#create-profile-button").ClassList.Should().Contain("flex-fill");
            cut.Find("#create-profile-button").ClassList.Should().NotContain("w-100");
        });

    // V3: row actions are icon-only (no visible text) -- must still carry an explicit aria-label
    // and title per convention-ui-blazor-icones-boutons.md's A11Y rule for icon-only buttons.
    [Fact]
    public async Task RowActionButtons_AreIconOnly_WithAriaLabelAndTitle_InBothTableAndCardTemplates() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await SeedProfileAsync(profile);

            var cut = Render<ImportProfiles>();

            foreach (var idPrefix in new[] { "edit-profile-button", "duplicate-profile-button", "delete-profile-button" })
            {
                foreach (var id in new[] { $"{idPrefix}-{profile.Id}", $"{idPrefix}-card-{profile.Id}" })
                {
                    var button = cut.Find($"#{id}");
                    button.TextContent.Trim().Should().BeEmpty();
                    button.QuerySelector("svg").Should().NotBeNull();
                    button.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
                    button.GetAttribute("title").Should().NotBeNullOrWhiteSpace();
                }
            }
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

    // Lot 030 (30.7): the two header action buttons now stay on one row at every breakpoint via a
    // plain "d-flex gap-2" wrapper -- reopens V4's "d-grid gap-2 d-md-flex" stacking idiom, which
    // used to go full-width-stacked below the md breakpoint.
    [Fact]
    public void ImportProfiles_HeaderButtons_ShareASingleRowFlexWrapper_AtEveryBreakpoint() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ImportProfiles>();

            var testButton = cut.Find("#test-import-profile-button");
            var createButton = cut.Find("#create-profile-button");
            testButton.ParentElement.Should().BeSameAs(createButton.ParentElement);

            var wrapper = testButton.ParentElement!;
            wrapper.ClassList.Should().Contain("d-flex");
            wrapper.ClassList.Should().Contain("gap-2");
            wrapper.ClassList.Should().NotContain("d-grid");
            wrapper.ClassList.Should().NotContain("d-md-flex");
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

    [Fact]
    public async Task DuplicateButton_WhenProfileAndItsCopyAlreadyExist_IncrementsSuffixToCopy2() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO");
            await SeedProfileAsync(original);
            await SeedProfileAsync(BuildProfileWithOneSheetRule("MAD OXO (Copy)"));

            var cut = Render<ImportProfiles>();
            cut.Find($"#duplicate-profile-button-{original.Id}").Click();

            var store = Services.GetRequiredService<IImportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(3);
            all.Should().Contain(p => p.Name == "MAD OXO (Copy 2)");
        });

    [Fact]
    public async Task DuplicateButton_ClickedOnTheCopyItself_AlsoIncrementsFromTheSharedBaseName() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO");
            await SeedProfileAsync(original);
            var copy = BuildProfileWithOneSheetRule("MAD OXO (Copy)");
            await SeedProfileAsync(copy);

            var cut = Render<ImportProfiles>();
            cut.Find($"#duplicate-profile-button-{copy.Id}").Click();

            var store = Services.GetRequiredService<IImportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(3);
            all.Should().Contain(p => p.Name == "MAD OXO (Copy 2)");
        });

    [Fact]
    public async Task DuplicateButton_WithThreeCollisionLevelsAlreadyPresent_ProducesCopy3() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO");
            await SeedProfileAsync(original);
            await SeedProfileAsync(BuildProfileWithOneSheetRule("MAD OXO (Copy)"));
            await SeedProfileAsync(BuildProfileWithOneSheetRule("MAD OXO (Copy 2)"));

            var cut = Render<ImportProfiles>();
            cut.Find($"#duplicate-profile-button-{original.Id}").Click();

            var store = Services.GetRequiredService<IImportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(4);
            all.Should().Contain(p => p.Name == "MAD OXO (Copy 3)");
        });

    // Lot 028 (28.2/28.3): clicking the delete button never calls DeleteAsync directly -- it only
    // opens the inline confirmation. A dedicated Mock<IImportProfileStore> registration (overriding
    // the real-EF one from the constructor) lets this be verified with Times.Never, per the
    // ticket's own explicit instruction.
    [Fact]
    public void DeleteButton_DoesNotCallDeleteAsyncImmediately_OnlyOpensConfirmation() => WithCulture("en-US", () =>
    {
        var profile = BuildProfileWithOneSheetRule();
        var storeMock = new Mock<IImportProfileStore>();
        storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
        Services.AddSingleton(storeMock.Object);

        var cut = Render<ImportProfiles>();
        cut.Find($"#delete-profile-button-{profile.Id}").Click();

        cut.Find($"#delete-profile-confirm-{profile.Id}").Should().NotBeNull();
        storeMock.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    [Fact]
    public void DeleteButton_OpensConfirmation_ShowingTheTargetedProfileName() => WithCulture("en-US", () =>
    {
        var profile = BuildProfileWithOneSheetRule("MAD OXO to delete");
        var storeMock = new Mock<IImportProfileStore>();
        storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
        Services.AddSingleton(storeMock.Object);

        var cut = Render<ImportProfiles>();
        cut.Find($"#delete-profile-button-{profile.Id}").Click();

        cut.Find($"#delete-profile-confirm-{profile.Id}").TextContent.Should().Contain("MAD OXO to delete");
        // Lot 040 (40.3): a destructive action pending confirmation is announced assertively,
        // consistent with the role="alert" treatment used for error messages (40.1).
        cut.Find($"#delete-profile-confirm-{profile.Id}").GetAttribute("role").Should().Be("alert");
    });

    [Fact]
    public void CancelDeleteButton_ClosesConfirmation_WithoutCallingDeleteAsync_AndOtherActionsStillWork() =>
        WithCulture("en-US", () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            var storeMock = new Mock<IImportProfileStore>();
            storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
            Services.AddSingleton(storeMock.Object);

            var cut = Render<ImportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
            cut.Find($"#delete-profile-button-{profile.Id}").Click();

            cut.Find($"#cancel-delete-profile-button-{profile.Id}").Click();

            cut.FindAll($"#delete-profile-confirm-{profile.Id}").Should().BeEmpty();
            storeMock.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

            // Edit/Duplicate are back and still functional after cancelling.
            cut.Find($"#edit-profile-button-{profile.Id}").Click();
            navigationManager.Uri.Should().EndWith($"/import-profiles/{profile.Id}/edit");
        });

    [Fact]
    public async Task ConfirmDeleteButton_CallsDeleteAsyncWithExactId_AndRemovesProfileFromReloadedList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var toDelete = BuildProfileWithOneSheetRule("MAD OXO to delete");
            var toKeep = BuildProfileWithOneSheetRule("MAD OXO to keep");
            await SeedProfileAsync(toDelete);
            await SeedProfileAsync(toKeep);

            var cut = Render<ImportProfiles>();
            cut.Find($"#delete-profile-button-{toDelete.Id}").Click();
            cut.Find($"#confirm-delete-profile-button-{toDelete.Id}").Click();

            cut.Markup.Should().NotContain("MAD OXO to delete");
            cut.Markup.Should().Contain("MAD OXO to keep");

            var store = Services.GetRequiredService<IImportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().NotContain(p => p.Id == toDelete.Id);
            all.Should().ContainSingle(p => p.Id == toKeep.Id);
        });

    [Fact]
    public async Task OpeningConfirmationOnAnotherRow_ClosesThePreviousOne_WithoutDeletingIt() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profileA = BuildProfileWithOneSheetRule("MAD OXO A");
            var profileB = BuildProfileWithOneSheetRule("MAD OXO B");
            await SeedProfileAsync(profileA);
            await SeedProfileAsync(profileB);

            var cut = Render<ImportProfiles>();
            cut.Find($"#delete-profile-button-{profileA.Id}").Click();
            cut.FindAll($"#delete-profile-confirm-{profileA.Id}").Should().HaveCount(1);

            cut.Find($"#delete-profile-button-{profileB.Id}").Click();

            cut.FindAll($"#delete-profile-confirm-{profileA.Id}").Should().BeEmpty();
            cut.FindAll($"#delete-profile-confirm-{profileB.Id}").Should().HaveCount(1);

            var store = Services.GetRequiredService<IImportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().Contain(p => p.Id == profileA.Id);
        });

    // X11 (Lot X): top-level list pages define no <PageBackNavLink>/<SectionContent>, so the
    // shared top-row's <SectionOutlet> stays genuinely empty here -- real DOM absence, not a
    // display:none check (same rule already applied to L2/NavMenu's own tests).
    [Fact]
    public void RenderedInsideTheSharedTopRowHost_ShowsNoBackNavLink() => WithCulture("en-US", () =>
    {
        var cut = Render<SectionOutletTestHost>(parameters => parameters.Add(
            p => p.ChildContent,
            (Microsoft.AspNetCore.Components.RenderFragment)(b =>
            {
                b.OpenComponent<ImportProfiles>(0);
                b.CloseComponent();
            })));

        var topRow = cut.Find(".top-row");
        topRow.QuerySelectorAll("button, a[id]").Should().BeEmpty();
        topRow.QuerySelector(".navbar-brand").Should().NotBeNull();
    });
}
