using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Layout;
using ExcelETL.BlazorAdmin.Resources;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

public class NavMenuTests : BunitContext
{
    public NavMenuTests() => Services.AddLocalization();

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

    [Fact]
    public void NavMenu_WhenNotAuthorized_AndEnglishCulture_ShowsEnglishLinks() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Find("#nav-register-link").TextContent.Should().Contain("Register");
        cut.Find("#nav-login-link").TextContent.Should().Contain("Login");
    });

    [Fact]
    public void NavMenu_WhenNotAuthorized_AndFrenchCulture_ShowsFrenchLinks() => WithCulture("fr-FR", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Find("#nav-register-link").TextContent.Should().Contain("S'inscrire");
        cut.Find("#nav-login-link").TextContent.Should().Contain("Connexion");
    });

    [Fact]
    public void NavMenu_WhenAuthorized_AndEnglishCulture_ShowsLogoutLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com");

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("Logout");
        cut.Markup.Should().Contain("admin@example.com");
    });

    [Fact]
    public void NavMenu_WhenAuthorized_AndEnglishCulture_ShowsProfileLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com");

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("My Profile");
        cut.Find("a[href='profile']").Should().NotBeNull();
    });

    [Fact]
    public void NavMenu_WhenAuthorized_ShowsSingleLinkWithUsernameAndProfileLabel() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("jdupont");

        var cut = Render<NavMenu>();

        var profileLabel = Services.GetRequiredService<IStringLocalizer<BlazorAdminMessages>>()["NavMenu_Profile"];

        var profileLink = cut.Find("#nav-profile-link");
        profileLink.GetAttribute("href").Should().Be("profile");
        profileLink.TextContent.Should().Contain("jdupont");
        profileLink.TextContent.Should().Contain(profileLabel.Value);
    });

    [Fact]
    public void NavMenu_WhenAuthorized_DoesNotRenderStandaloneUsernameElement() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("jdupont");

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-profile-link").Should().HaveCount(1);
        cut.FindAll("span.nav-link").Should().BeEmpty();
    });

    [Fact]
    public void NavMenu_WhenNotAuthorized_DoesNotShowProfileLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-profile-link").Should().BeEmpty();
    });

    private static readonly string[] AdminLinkIds =
    [
        "nav-users-link",
        "nav-import-profiles-link",
        "nav-export-profiles-link",
        "nav-generated-files-link",
    ];

    [Fact]
    public void NavMenu_WhenNotAuthorized_HidesAdminLinks_AndShowsLoginLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        foreach (var id in AdminLinkIds)
        {
            cut.FindAll($"#{id}").Should().BeEmpty();
        }

        var loginLink = cut.Find("#nav-login-link");
        loginLink.GetAttribute("href").Should().Be("Account/Login");
    });

    [Fact]
    public void NavMenu_WhenAuthorizedAsAdmin_ShowsAdminLinks_AndHidesLoginLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com").SetRoles(IdentitySeeder.AdminRoleName);

        var cut = Render<NavMenu>();

        foreach (var id in AdminLinkIds)
        {
            cut.FindAll($"#{id}").Should().HaveCount(1);
        }

        cut.FindAll("#nav-login-link").Should().BeEmpty();
    });

    [Fact]
    public void NavMenu_WhenNotAuthorized_ShowsLoginLink_ExactlyOnce() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-login-link").Should().HaveCount(1);
    });

    [Fact]
    public void NavMenu_WhenNotAuthorized_HidesLogsLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-logs-link").Should().BeEmpty();
    });

    [Fact]
    public void NavMenu_WhenAuthorized_WithoutAdminRole_ShowsLogsLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("someone@example.com");

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-logs-link").Should().HaveCount(1);
    });

    [Fact]
    public void NavMenu_WhenAuthorizedAsAdmin_DoesNotRenderProfileTestLinks() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com").SetRoles(IdentitySeeder.AdminRoleName);

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-import-profiles-test-link").Should().BeEmpty();
        cut.FindAll("#nav-export-profiles-test-link").Should().BeEmpty();
    });

    [Fact]
    public void NavMenu_RendersAdminAndLogsLinks_InExpectedOrder() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com").SetRoles(IdentitySeeder.AdminRoleName);

        var cut = Render<NavMenu>();

        var orderedIds = new[]
        {
            "nav-import-profiles-link",
            "nav-export-profiles-link",
            "nav-users-link",
            "nav-generated-files-link",
            "nav-logs-link",
            "nav-profile-link",
        };

        var indexes = orderedIds.Select(id => cut.Markup.IndexOf($"id=\"{id}\"", StringComparison.Ordinal)).ToArray();

        indexes.Should().OnlyContain(index => index >= 0);
        indexes.Should().BeInAscendingOrder();

        var logoutLabel = Services.GetRequiredService<IStringLocalizer<BlazorAdminMessages>>()["NavMenu_Logout"].Value;
        var logoutButtonIndex = cut.Markup.IndexOf(logoutLabel, StringComparison.Ordinal);
        var profileLinkIndex = indexes[^1];

        logoutButtonIndex.Should().BeGreaterThan(profileLinkIndex);
    });

    [Fact]
    public void NavMenu_RendersRenamedBrand() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("Alpha - MAD / REL OXO");
        cut.Markup.Should().NotContain("ExcelETL.BlazorAdmin");
    });

    // --- Lot Y (Y1: title/hamburger collision on mobile) --------------------------------------

    // Y0 confirmed X11 is merged and the hamburger toggler is a sibling of .top-row (not one of its
    // flex children -- it stays absolutely positioned to preserve the
    // `.navbar-toggler:checked ~ .nav-scrollable` CSS-only sibling selector), so the guard-rail below
    // asserts on the real structure rather than the ticket's initial (incorrect) assumption that all
    // three elements are literal children of .top-row.
    [Fact]
    public void NavMenu_BrandLink_HasTruncationAndFlexGrowClasses() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        var brand = cut.Find(".navbar-brand");
        brand.ClassList.Should().Contain("text-truncate");
        brand.ClassList.Should().Contain("flex-grow-1");
    });

    [Fact]
    public void NavMenu_HamburgerToggler_HasFlexShrinkZeroClass() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        var toggler = cut.Find(".navbar-toggler");
        toggler.ClassList.Should().Contain("flex-shrink-0");
    });

    // Real structure: brand link + SectionOutlet slot are children of .top-row .container-fluid;
    // the toggler is a sibling of .top-row itself (see the comment above) -- both remain true after
    // the Y1 fix, guarding against an accidental DOM restructuring that would break X11.
    [Fact]
    public void NavMenu_TopRowStructure_BrandStaysInsideContainerFluid_TogglerStaysOutsideTopRow() =>
        WithCulture("en-US", () =>
        {
            this.AddAuthorization().SetNotAuthorized();

            var cut = Render<NavMenu>();

            var containerFluid = cut.Find(".top-row .container-fluid");
            containerFluid.QuerySelector(".navbar-brand").Should().NotBeNull();

            var topRow = cut.Find(".top-row");
            topRow.QuerySelector(".navbar-toggler").Should().BeNull();
        });

    private sealed class NavMenuWithBackLink : Microsoft.AspNetCore.Components.ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<NavMenu>(0);
            builder.CloseComponent();

            builder.OpenComponent<ExcelETL.BlazorAdmin.Components.Layout.PageBackNavLink>(1);
            builder.AddComponentParameter(2, nameof(PageBackNavLink.Id), "y1-back-test-button");
            builder.AddComponentParameter(3, nameof(PageBackNavLink.Label), "Back");
            builder.AddComponentParameter(
                4,
                nameof(PageBackNavLink.OnClick),
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, () => { }));
            builder.CloseComponent();
        }
    }

    [Fact]
    public void NavMenu_WithActiveBackLink_BackLinkHasFlexShrinkZeroClass_AlongsideTruncatedBrand() =>
        WithCulture("en-US", () =>
        {
            this.AddAuthorization().SetNotAuthorized();

            var cut = Render<NavMenuWithBackLink>();

            var backLink = cut.Find("#y1-back-test-button");
            backLink.ClassList.Should().Contain("flex-shrink-0");

            var brand = cut.Find(".navbar-brand");
            brand.ClassList.Should().Contain("text-truncate");

            var containerFluid = cut.Find(".top-row .container-fluid");
            containerFluid.QuerySelector("#y1-back-test-button").Should().NotBeNull();
            containerFluid.QuerySelector(".navbar-brand").Should().NotBeNull();
        });
}
