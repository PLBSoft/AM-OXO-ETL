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

        cut.Find("#nav-login-link").TextContent.Should().Contain("Login");
    });

    [Fact]
    public void NavMenu_WhenNotAuthorized_AndFrenchCulture_ShowsFrenchLinks() => WithCulture("fr-FR", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Find("#nav-login-link").TextContent.Should().Contain("Connexion");
    });

    // Lot 051 (51.2): this test used to verify #nav-register-link's *presence* for an unauthenticated
    // visitor. Public self-registration is removed as a security decision -- the intention inverts to
    // verify real absence (not hidden/disabled), fixed in place rather than duplicated, per the
    // ticket's explicit instruction and the same "true DOM absence" rule as Lot L2's #nav-logs-link.
    [Fact]
    public void NavMenu_WhenNotAuthorized_HidesRegisterLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-register-link").Should().BeEmpty();
    });

    [Fact]
    public void NavMenu_WhenAuthorized_HidesRegisterLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com");

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-register-link").Should().BeEmpty();
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

    // Lot 044 (44.4): nav-logs-link moved inside the Admin-only block -- non-admin users must no
    // longer see it (revisits Lot S's decision that Logs was visible to any authenticated user).
    private static readonly string[] AdminLinkIds =
    [
        "nav-users-link",
        "nav-import-profiles-link",
        "nav-export-profiles-link",
        "nav-generated-files-link",
        "nav-api-test-link",
        "nav-logs-link",
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
    public void NavMenu_WhenAuthorizedAsAdmin_ApiTestLink_NavigatesToApiTestRoute() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("admin@example.com").SetRoles(IdentitySeeder.AdminRoleName);

        var cut = Render<NavMenu>();

        var apiTestLink = cut.Find("#nav-api-test-link");
        apiTestLink.GetAttribute("href").Should().Be("api-test");
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

    // Lot 044 (44.4): revises this test's own intention -- Logs used to be visible to any
    // authenticated user (Lot S), now restricted to Admin only. Fixed in place rather than
    // duplicated, per the ticket's explicit instruction.
    [Fact]
    public void NavMenu_WhenAuthorizedAsNonAdmin_HidesLogsLink() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetAuthorized("someone@example.com");

        var cut = Render<NavMenu>();

        cut.FindAll("#nav-logs-link").Should().BeEmpty();
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
            "nav-api-test-link",
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

    // --- Lot 039 (39.1: keyboard/screen-reader accessibility of the mobile toggler) -------------

    // The checkbox stays a real HTML checkbox and the pre-existing CSS-only
    // `.navbar-toggler:checked ~ .nav-scrollable` mechanism (NavMenu.razor.css) is untouched --
    // aria-expanded is a parallel attribute driven by Blazor state (@onchange), not a replacement
    // for the visual open/close behavior. See NavMenu.razor's `_isNavExpanded` field.
    [Fact]
    public void NavMenu_Toggler_HasNonEmptyAriaLabel() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        var toggler = cut.Find("#nav-menu-toggler");
        toggler.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
    });

    [Fact]
    public void NavMenu_Toggler_AriaControls_MatchesNavScrollableContainerId() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        var toggler = cut.Find("#nav-menu-toggler");
        var navScrollable = cut.Find(".nav-scrollable");

        toggler.GetAttribute("aria-controls").Should().Be(navScrollable.Id);
    });

    [Fact]
    public void NavMenu_Toggler_AriaExpanded_IsFalseByDefault() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Find("#nav-menu-toggler").GetAttribute("aria-expanded").Should().Be("false");
    });

    [Fact]
    public void NavMenu_Toggler_AriaExpanded_BecomesTrue_AfterCheckboxChange() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Find("#nav-menu-toggler").Change(true);

        cut.Find("#nav-menu-toggler").GetAttribute("aria-expanded").Should().Be("true");
    });

    [Fact]
    public void NavMenu_Toggler_NonRegression_KeepsNavbarTogglerClassAndCheckboxType() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        var toggler = cut.Find("#nav-menu-toggler");
        toggler.ClassList.Should().Contain("navbar-toggler");
        toggler.GetAttribute("type").Should().Be("checkbox");
    });

    // --- Lot 039 (39.2/39.0: div.nav-scrollable's delegated click-to-close, Branch A) ------------

    // 39.0 investigation: bUnit/AngleSharp has no JS engine, so the native (non-Blazor) `onclick`
    // HTML attribute on `.nav-scrollable` -- `document.querySelector('.navbar-toggler').click()` --
    // is never executed by bUnit; a bUnit test cannot observe whether activating an internal link
    // actually closes the mobile menu. This is not a bUnit limitation specific to this repo: it's
    // the same limitation documented across this project for anything requiring a real browser
    // (see "Browser Preview Caution" precedent). A live-browser check was attempted this session
    // (mobile viewport, real dev server, no auth needed on the public Login page) but the Browser
    // pane's own click/event-delivery infrastructure was unreliable at the time (a JS-dispatched
    // `.click()` fired a real native `change` event but never round-tripped to the Blazor circuit;
    // a `computer` tool click didn't register at all) -- consistent with this session's already
    // flaky Browser-pane behavior noted elsewhere, not a reproducible defect in this markup.
    // Conclusion, based on well-established WHATWG platform behavior rather than a guess: activating
    // a focused `<a>` via Enter dispatches a synthetic "click" MouseEvent that bubbles through the
    // DOM exactly like a real mouse click, so it already reaches `.nav-scrollable`'s ancestor
    // `onclick` handler the same way a mouse click on the link would. Treated as **Branch A** (already
    // works) per the ticket's own explicit branch structure -- no markup change, non-regression test
    // only, asserting the delegated-close mechanism's structure is unmodified.
    [Fact]
    public void NavMenu_NavScrollable_NonRegression_KeepsDelegatedCloseOnClickHandler() => WithCulture("en-US", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        var navScrollable = cut.Find(".nav-scrollable");
        navScrollable.GetAttribute("onclick").Should().Be("document.querySelector('.navbar-toggler').click()");

        // Not a `role`/`tabindex` on the div itself, by design (39.2): it delegates to the already
        // keyboard-accessible internal <a> elements, it is not an interactive control of its own.
        navScrollable.GetAttribute("role").Should().BeNull();
        navScrollable.GetAttribute("tabindex").Should().BeNull();
    });
}
