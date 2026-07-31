using System.Globalization;
using Bunit;
using ExcelETL.Application.Generation;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.BlazorAdmin.Tests.Pages;
using ExcelETL.Domain.Generation.Fields;
using ExcelETL.Domain.Generation.Profile;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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

    // Lot 042 (42.2): the mobile card's per-row title previously skipped straight from h1 to h5 --
    // fixed to h2, keeping its pre-existing visual size via the Bootstrap `.h5` utility class.
    [Fact]
    public async Task ExportProfiles_WithExistingProfile_HasNoHeadingLevelSkip() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ExportProfiles>();

            HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
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

    // Lot 041 (41.2): the "Créer un profil d'export" CTA was one of the audit's flagged icon-less
    // CTAs.
    [Fact]
    public void CreateProfileButton_HasIcon() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfiles>();

        cut.Find("#create-export-profile-button").QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
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
    public void ExportProfiles_DisplaysNonTechnicalIntroText() => WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfiles>();

        var intro = cut.Find("#export-profiles-intro");
        intro.ClassList.Should().Contain("alert").And.Contain("alert-info");
        intro.TextContent.Should().NotBeNullOrWhiteSpace();
    });

    // Lot 030 (30.7): reopens V4/X2's mobile stacking decision on explicit client request -- the
    // two buttons now share the row's width equally (flex-fill) instead of each going full-width
    // on its own line.
    [Fact]
    public void HeaderActionButtons_ShareWidthEqually_AndAreNoLongerIndividuallyFullWidth() =>
        WithCulture("en-US", () =>
        {
            var cut = Render<ExportProfiles>();

            cut.Find("#test-export-profile-button").ClassList.Should().Contain("flex-fill");
            cut.Find("#test-export-profile-button").ClassList.Should().NotContain("w-100");
            cut.Find("#create-export-profile-button").ClassList.Should().Contain("flex-fill");
            cut.Find("#create-export-profile-button").ClassList.Should().NotContain("w-100");
        });

    // V3: same icon-only row actions as ImportProfilesTests -- parity check.
    [Fact]
    public async Task RowActionButtons_AreIconOnly_WithAriaLabelAndTitle_InBothTableAndCardTemplates() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await SeedProfileAsync(profile);

            var cut = Render<ExportProfiles>();

            foreach (var idPrefix in new[] { "edit-export-profile-button", "duplicate-export-profile-button", "delete-export-profile-button" })
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

            var cut = Render<ExportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

            cut.Find($"#edit-export-profile-button-{profile.Id}").Click();

            navigationManager.Uri.Should().EndWith($"/export-profiles/{profile.Id}/edit");
        });

    // V2: same table -> card fallback as ImportProfilesTests, see comments there.
    [Fact]
    public async Task ExportProfiles_RendersBothTableAndCardTemplates_WithResponsiveClasses() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedProfileAsync(BuildProfileWithOneSheetRule());

            var cut = Render<ExportProfiles>();

            var table = cut.Find("table.table");
            table.ClassList.Should().Contain("d-none");
            table.ClassList.Should().Contain("d-md-table");

            var cardContainer = cut.Find("div.d-md-none");
            cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
        });

    [Fact]
    public async Task ExportProfiles_CardTemplate_DisplaysSameContentAsTable() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            await SeedProfileAsync(profile);

            var cut = Render<ExportProfiles>();

            var card = cut.Find("div.d-md-none .card");
            card.TextContent.Should().Contain("MAD OXO export");
            card.TextContent.Should().Contain("1 sheet rule(s)");

            card.QuerySelector($"#edit-export-profile-button-card-{profile.Id}").Should().NotBeNull();
            card.QuerySelector($"#duplicate-export-profile-button-card-{profile.Id}").Should().NotBeNull();
        });

    // Lot 030 (30.7): same single-row flex wrapper reversal as ImportProfilesTests.
    [Fact]
    public void ExportProfiles_HeaderButtons_ShareASingleRowFlexWrapper_AtEveryBreakpoint() =>
        WithCulture("en-US", () =>
    {
        var cut = Render<ExportProfiles>();

        var testButton = cut.Find("#test-export-profile-button");
        var createButton = cut.Find("#create-export-profile-button");
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

    [Fact]
    public async Task DuplicateButton_WhenProfileAndItsCopyAlreadyExist_IncrementsSuffixToCopy2() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO export");
            await SeedProfileAsync(original);
            await SeedProfileAsync(BuildProfileWithOneSheetRule("MAD OXO export (Copy)"));

            var cut = Render<ExportProfiles>();
            cut.Find($"#duplicate-export-profile-button-{original.Id}").Click();

            var store = Services.GetRequiredService<IExportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(3);
            all.Should().Contain(p => p.Name == "MAD OXO export (Copy 2)");
        });

    [Fact]
    public async Task DuplicateButton_ClickedOnTheCopyItself_AlsoIncrementsFromTheSharedBaseName() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO export");
            await SeedProfileAsync(original);
            var copy = BuildProfileWithOneSheetRule("MAD OXO export (Copy)");
            await SeedProfileAsync(copy);

            var cut = Render<ExportProfiles>();
            cut.Find($"#duplicate-export-profile-button-{copy.Id}").Click();

            var store = Services.GetRequiredService<IExportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(3);
            all.Should().Contain(p => p.Name == "MAD OXO export (Copy 2)");
        });

    [Fact]
    public async Task DuplicateButton_WithThreeCollisionLevelsAlreadyPresent_ProducesCopy3() =>
        await WithCultureAsync("en-US", async () =>
        {
            var original = BuildProfileWithOneSheetRule("MAD OXO export");
            await SeedProfileAsync(original);
            await SeedProfileAsync(BuildProfileWithOneSheetRule("MAD OXO export (Copy)"));
            await SeedProfileAsync(BuildProfileWithOneSheetRule("MAD OXO export (Copy 2)"));

            var cut = Render<ExportProfiles>();
            cut.Find($"#duplicate-export-profile-button-{original.Id}").Click();

            var store = Services.GetRequiredService<IExportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().HaveCount(4);
            all.Should().Contain(p => p.Name == "MAD OXO export (Copy 3)");
        });

    // Lot 028 (28.2/28.3): mirrors ImportProfilesTests' symmetric coverage -- see there for the
    // fuller rationale comments (Mock<IExportProfileStore> override for the Times.Never/Once
    // verifications, per the ticket's own explicit instruction).
    [Fact]
    public void DeleteButton_DoesNotCallDeleteAsyncImmediately_OnlyOpensConfirmation() => WithCulture("en-US", () =>
    {
        var profile = BuildProfileWithOneSheetRule();
        var storeMock = new Mock<IExportProfileStore>();
        storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
        Services.AddSingleton(storeMock.Object);

        var cut = Render<ExportProfiles>();
        cut.Find($"#delete-export-profile-button-{profile.Id}").Click();

        cut.Find($"#delete-export-profile-confirm-{profile.Id}").Should().NotBeNull();
        storeMock.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    [Fact]
    public void DeleteButton_OpensConfirmation_ShowingTheTargetedProfileName() => WithCulture("en-US", () =>
    {
        var profile = BuildProfileWithOneSheetRule("MAD OXO export to delete");
        var storeMock = new Mock<IExportProfileStore>();
        storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
        Services.AddSingleton(storeMock.Object);

        var cut = Render<ExportProfiles>();
        cut.Find($"#delete-export-profile-button-{profile.Id}").Click();

        cut.Find($"#delete-export-profile-confirm-{profile.Id}").TextContent.Should().Contain("MAD OXO export to delete");
        // Lot 040 (40.3): a destructive action pending confirmation is announced assertively,
        // consistent with the role="alert" treatment used for error messages (40.1).
        cut.Find($"#delete-export-profile-confirm-{profile.Id}").GetAttribute("role").Should().Be("alert");
    });

    [Fact]
    public void CancelDeleteButton_ClosesConfirmation_WithoutCallingDeleteAsync_AndOtherActionsStillWork() =>
        WithCulture("en-US", () =>
        {
            var profile = BuildProfileWithOneSheetRule();
            var storeMock = new Mock<IExportProfileStore>();
            storeMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([profile]);
            Services.AddSingleton(storeMock.Object);

            var cut = Render<ExportProfiles>();
            var navigationManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
            cut.Find($"#delete-export-profile-button-{profile.Id}").Click();

            cut.Find($"#cancel-delete-export-profile-button-{profile.Id}").Click();

            cut.FindAll($"#delete-export-profile-confirm-{profile.Id}").Should().BeEmpty();
            storeMock.Verify(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

            cut.Find($"#edit-export-profile-button-{profile.Id}").Click();
            navigationManager.Uri.Should().EndWith($"/export-profiles/{profile.Id}/edit");
        });

    [Fact]
    public async Task ConfirmDeleteButton_CallsDeleteAsyncWithExactId_AndRemovesProfileFromReloadedList() =>
        await WithCultureAsync("en-US", async () =>
        {
            var toDelete = BuildProfileWithOneSheetRule("MAD OXO export to delete");
            var toKeep = BuildProfileWithOneSheetRule("MAD OXO export to keep");
            await SeedProfileAsync(toDelete);
            await SeedProfileAsync(toKeep);

            var cut = Render<ExportProfiles>();
            cut.Find($"#delete-export-profile-button-{toDelete.Id}").Click();
            cut.Find($"#confirm-delete-export-profile-button-{toDelete.Id}").Click();

            cut.Markup.Should().NotContain("MAD OXO export to delete");
            cut.Markup.Should().Contain("MAD OXO export to keep");

            var store = Services.GetRequiredService<IExportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().NotContain(p => p.Id == toDelete.Id);
            all.Should().ContainSingle(p => p.Id == toKeep.Id);
        });

    [Fact]
    public async Task OpeningConfirmationOnAnotherRow_ClosesThePreviousOne_WithoutDeletingIt() =>
        await WithCultureAsync("en-US", async () =>
        {
            var profileA = BuildProfileWithOneSheetRule("MAD OXO export A");
            var profileB = BuildProfileWithOneSheetRule("MAD OXO export B");
            await SeedProfileAsync(profileA);
            await SeedProfileAsync(profileB);

            var cut = Render<ExportProfiles>();
            cut.Find($"#delete-export-profile-button-{profileA.Id}").Click();
            cut.FindAll($"#delete-export-profile-confirm-{profileA.Id}").Should().HaveCount(1);

            cut.Find($"#delete-export-profile-button-{profileB.Id}").Click();

            cut.FindAll($"#delete-export-profile-confirm-{profileA.Id}").Should().BeEmpty();
            cut.FindAll($"#delete-export-profile-confirm-{profileB.Id}").Should().HaveCount(1);

            var store = Services.GetRequiredService<IExportProfileStore>();
            var all = await store.GetAllAsync();
            all.Should().Contain(p => p.Id == profileA.Id);
        });
}
