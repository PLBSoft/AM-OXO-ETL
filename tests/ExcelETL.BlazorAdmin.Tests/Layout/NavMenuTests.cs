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

        cut.Markup.Should().Contain("Register");
        cut.Markup.Should().Contain("Login");
    });

    [Fact]
    public void NavMenu_WhenNotAuthorized_AndFrenchCulture_ShowsFrenchLinks() => WithCulture("fr-FR", () =>
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = Render<NavMenu>();

        cut.Markup.Should().Contain("S'inscrire");
        cut.Markup.Should().Contain("Connexion");
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

        cut.Markup.Should().NotContain("My Profile");
    });

    private static readonly string[] AdminLinkIds =
    [
        "nav-users-link",
        "nav-import-profiles-link",
        "nav-import-profiles-test-link",
        "nav-export-profiles-link",
        "nav-export-profiles-test-link",
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
}
