using System.Globalization;
using System.Security.Claims;
using Bunit;
using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Layout;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Services;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Extraction.Primitives;
using ExcelETL.Domain.Extraction.Profile;
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

// Lot 058 (58.3): garde-fou for the icon+label gabarit (d-flex align-items-center
// justify-content-center gap-1) across the whole periphery identified by the ticket's own
// `grep -rn 'MarkupString)AdminIconMarkup'` -- the profile editors and their 8 sub-forms, the two
// list pages' Create buttons, ExportProfileTest's generate-workbook-button, ApiTest's
// process-button, Users.razor's 4 buttons, and PageBackNavLink.razor. Home.razor's icon usages are
// deliberately excluded -- they sit inside <a class="card"> KPI tiles, not <button> elements with
// a visible-label pattern, and already use their own already-correct d-flex/gap spacing.
public class IconLabelButtonGabaritTests : BunitContext
{
    private const string Gabarit = "d-flex align-items-center justify-content-center gap-1";

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
        try { action(); }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    private static void AssertHasGabaritIconAndLabel(AngleSharp.Dom.IElement button)
    {
        button.ClassList.Should().Contain(Gabarit.Split(' '));
        button.QuerySelectorAll("svg").Should().HaveCount(1);
        button.QuerySelector("svg").Should().NotBeNull();
        button.QuerySelector("svg")!.GetAttribute("aria-hidden").Should().Be("true");
        button.TextContent.Trim().Should().NotBeNullOrEmpty();
    }

    // ------------------------------------------------------------------------------------------
    // Import/export profile editors + their 8 sub-forms (always-present "Add" instances).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ImportProfileEditor_IconLabelButtons_AllCarryTheGabarit() => WithCulture("en-US", () =>
    {
        var dbContextFactory = new TestDbContextFactory("IconGabarit_Import_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();

        var cut = Render<ImportProfileEditor>();

        foreach (var id in new[]
        {
            "add-default-tableau-button", "add-default-application-name-button",
            "save-profile-button",
            "add-block-field-button", "add-unconditional-colonne-button", "add-point-rule-button",
            "add-header-field-button", "add-header-composite-button", "add-sheet-rule-button",
        })
        {
            AssertHasGabaritIconAndLabel(cut.Find($"#{id}"));
        }

        // The add-form toggle is icon-less in its "open" state (default here, creation mode) --
        // still gets the gabarit class, but the shared icon-count helper doesn't apply to it.
        cut.Find("#toggle-add-sheet-rule-form-button").ClassList.Should().Contain(Gabarit.Split(' '));
    });

    [Fact]
    public void ExportProfileEditor_IconLabelButtons_AllCarryTheGabarit() => WithCulture("en-US", () =>
    {
        var dbContextFactory = new TestDbContextFactory("IconGabarit_Export_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();

        var cut = Render<ExportProfileEditor>();

        foreach (var id in new[]
        {
            "save-export-profile-button",
            "add-column-definition-button", "add-point-column-definition-button",
            "add-application-column-definition-button", "add-sheet-generation-rule-button",
        })
        {
            AssertHasGabaritIconAndLabel(cut.Find($"#{id}"));
        }

        cut.Find("#toggle-add-sheet-generation-rule-form-button").ClassList.Should().Contain(Gabarit.Split(' '));
    });

    // Non-regression: 53.3/53.4/56.7 classes are still there, the gabarit only adds to them.
    [Fact]
    public void GabaritButtons_StillCarryTheirPreExistingClasses_NonRegression() => WithCulture("en-US", () =>
    {
        var dbContextFactory = new TestDbContextFactory("IconGabarit_NonReg_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();
        Services.AddSingleton<BusinessExceptionLocalizer>();

        var cut = Render<ImportProfileEditor>();

        var addBlockField = cut.Find("#add-block-field-button");
        addBlockField.ClassList.Should().Contain("btn").And.Contain("btn-secondary").And.Contain("w-100").And.Contain("mt-3");

        var saveProfile = cut.Find("#save-profile-button");
        saveProfile.ClassList.Should().Contain("btn").And.Contain("btn-primary").And.Contain("btn-lg").And.Contain("w-100").And.Contain("w-md-auto");
    });

    // ------------------------------------------------------------------------------------------
    // List pages' Create buttons.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ImportProfiles_CreateButton_CarriesTheGabarit() => WithCulture("en-US", () =>
    {
        var dbContextFactory = new TestDbContextFactory("IconGabarit_ImportProfiles_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddLocalization();

        var cut = Render<ImportProfiles>();

        AssertHasGabaritIconAndLabel(cut.Find("#create-profile-button"));
    });

    [Fact]
    public void ExportProfiles_CreateButton_CarriesTheGabarit() => WithCulture("en-US", () =>
    {
        var dbContextFactory = new TestDbContextFactory("IconGabarit_ExportProfiles_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();

        var cut = Render<ExportProfiles>();

        AssertHasGabaritIconAndLabel(cut.Find("#create-export-profile-button"));
    });

    // Parity: the two Create buttons, coming from different files, share the exact same class list.
    [Fact]
    public void CreateProfileButtons_HaveIdenticalGabaritClassAcrossImportAndExportListPages() => WithCulture("en-US", () =>
    {
        var importDbContextFactory = new TestDbContextFactory("IconGabarit_Parity_Import_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(importDbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddLocalization();

        var importCut = Render<ImportProfiles>();
        var exportCut = Render<ExportProfiles>();

        var importClass = importCut.Find("#create-profile-button").GetAttribute("class");
        var exportClass = exportCut.Find("#create-export-profile-button").GetAttribute("class");

        importClass.Should().Be(exportClass);
        importClass.Should().Contain(Gabarit);
    });

    // ------------------------------------------------------------------------------------------
    // ApiTest.process-button.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ApiTest_ProcessButton_CarriesTheGabarit() => WithCulture("en-US", () =>
    {
        var dbContextFactory = new TestDbContextFactory("IconGabarit_ApiTest_" + Guid.NewGuid());
        Services.AddSingleton<IDbContextFactory<ExcelEtlDbContext>>(dbContextFactory);
        Services.AddSingleton<IImportProfileStore, EfImportProfileStore>();
        Services.AddSingleton<IExportProfileStore, EfExportProfileStore>();
        Services.AddSingleton(new Mock<IOxoApiTestClient>().Object);
        Services.AddLocalization();

        var cut = Render<ApiTest>();

        AssertHasGabaritIconAndLabel(cut.Find("#process-button"));
    });

    // ExportProfileTest.generate-workbook-button's gabarit is covered by extending the existing
    // GenerateWorkbookButton_HasIcon test in ExportProfileTestTests.cs in place (58.3) -- that test
    // already has the real file-upload setup needed to make the button exist at all; duplicating
    // that setup here just to re-assert the same class list would be pure duplication.

    // ------------------------------------------------------------------------------------------
    // Users.razor's 4 icon+label buttons.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Users_CreateButton_CarriesTheGabarit() => WithCulture("en-US", () =>
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        var userManagementServiceMock = new Mock<IUserManagementService>();
        Services.AddSingleton(userRepositoryMock.Object);
        Services.AddSingleton(userManagementServiceMock.Object);
        Services.AddLocalization();
        userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<UserSummary>)[]);
        userManagementServiceMock.Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<string>)[]);

        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("admin@example.com");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, "admin-id"));

        var cut = Render<Users>();

        AssertHasGabaritIconAndLabel(cut.Find("#create-user-button"));
    });

    // ------------------------------------------------------------------------------------------
    // PageBackNavLink.razor -- explicit non-regression: still icon-only under 768px (aria-label +
    // title preserved), now also carrying the gabarit for its icon+label desktop rendering.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void PageBackNavLink_CarriesTheGabarit_AndKeepsAriaLabelAndTitle() => WithCulture("en-US", () =>
    {
        // PageBackNavLink projects into NavMenu's shared top-row SectionOutlet via SectionContent --
        // rendering it standalone produces no visible content. SectionOutletTestHost is this
        // project's own established test double for that structure (see NavMenuTests.cs).
        var cut = Render<ExcelETL.BlazorAdmin.Tests.Layout.SectionOutletTestHost>(parameters => parameters
            .Add(p => p.ChildContent, BackLinkFragment));

        var button = cut.Find("#back-link-test");
        button.ClassList.Should().Contain(Gabarit.Split(' '));
        button.GetAttribute("aria-label").Should().Be("Back to list");
        button.GetAttribute("title").Should().Be("Back to list");
        button.QuerySelector("svg[aria-hidden='true']").Should().NotBeNull();
    });

    private static readonly Microsoft.AspNetCore.Components.RenderFragment BackLinkFragment = builder =>
    {
        builder.OpenComponent<PageBackNavLink>(0);
        builder.AddComponentParameter(1, nameof(PageBackNavLink.Id), "back-link-test");
        builder.AddComponentParameter(2, nameof(PageBackNavLink.Label), "Back to list");
        builder.AddComponentParameter(
            3,
            nameof(PageBackNavLink.OnClick),
            Microsoft.AspNetCore.Components.EventCallback.Factory.Create(EmptyCallbackTarget, () => { }));
        builder.CloseComponent();
    };

    private static readonly object EmptyCallbackTarget = new();
}
