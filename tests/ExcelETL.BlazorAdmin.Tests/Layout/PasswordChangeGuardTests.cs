using System.Security.Claims;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Account.Pages;
using ExcelETL.BlazorAdmin.Components.Layout;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Layout;

// Lot 045 (45.4): navigation guard enforcing RequirePasswordChangeOnFirstLogin globally, not just as
// a post-login message. Rendered directly (not through MainLayout/Router) so the interception can be
// exercised precisely via TestNavigationManager -- same feasibility mechanism confirmed by Lot 043
// (NavigateTo triggers registered location-changing handlers end-to-end under bUnit).
public class PasswordChangeGuardTests : BunitContext
{
    private readonly Bunit.TestDoubles.BunitAuthorizationContext _authContext;

    public PasswordChangeGuardTests()
    {
        // Both calls must happen before any service is resolved from Services (e.g. via
        // GetRequiredService<NavigationManager>()) -- bUnit locks its service container the moment
        // the first service is retrieved, and registering AddAuthorization()'s services afterward
        // throws.
        _authContext = this.AddAuthorization();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private void RenderGuardAsAuthorized(bool requiresPasswordChange)
    {
        _authContext.SetAuthorized("user@example.com");
        if (requiresPasswordChange)
        {
            _authContext.SetClaims(new Claim(RequirePasswordChangeClaimsPrincipalFactory.ClaimType, bool.TrueString));
        }

        Render<PasswordChangeGuard>();
    }

    [Fact]
    public void NavigatingToAdminPage_WithFlagClaim_RedirectsToForcePasswordChange()
    {
        RenderGuardAsAuthorized(requiresPasswordChange: true);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo("users");

        navigationManager.Uri.Should().EndWith(ForcePasswordChange.Route);
    }

    [Fact]
    public void NavigatingToForcePasswordChangePage_WithFlagClaim_DoesNotRedirect()
    {
        RenderGuardAsAuthorized(requiresPasswordChange: true);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo(ForcePasswordChange.Route);

        navigationManager.Uri.Should().EndWith(ForcePasswordChange.Route);
    }

    [Fact]
    public void NavigatingToLogout_WithFlagClaim_DoesNotRedirect()
    {
        RenderGuardAsAuthorized(requiresPasswordChange: true);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo("Account/Logout");

        navigationManager.Uri.Should().EndWith("Account/Logout");
    }

    [Fact]
    public void NavigatingToAdminPage_WithoutFlagClaim_DoesNotRedirect()
    {
        RenderGuardAsAuthorized(requiresPasswordChange: false);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo("users");

        navigationManager.Uri.Should().EndWith("users");
    }

    [Fact]
    public void OnFreshRender_WithFlagClaim_AndCurrentUriNotAllowed_RedirectsImmediately()
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("import-profiles");

        RenderGuardAsAuthorized(requiresPasswordChange: true);

        navigationManager.Uri.Should().EndWith(ForcePasswordChange.Route);
    }

    // Lot 049 (49.4): an error page must never be a redirection target. Redirecting away from
    // /not-found or /Error while the flag is set is a latent redirect loop -- today it is only masked
    // by BlazorDisableThrowNavigationException, and it is exactly how a rendering failure on the
    // forced-change page would turn into the inescapable loop reported for this lot.
    [Theory]
    [InlineData("not-found")]
    [InlineData("Error")]
    public void NavigatingToErrorPage_WithFlagClaim_DoesNotRedirect(string errorPagePath)
    {
        RenderGuardAsAuthorized(requiresPasswordChange: true);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        navigationManager.NavigateTo(errorPagePath);

        navigationManager.Uri.Should().EndWith(errorPagePath);
    }

    [Theory]
    [InlineData("not-found")]
    [InlineData("Error")]
    public void OnFreshRender_WithFlagClaim_AndCurrentUriIsErrorPage_DoesNotRedirect(string errorPagePath)
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo(errorPagePath);

        RenderGuardAsAuthorized(requiresPasswordChange: true);

        navigationManager.Uri.Should().EndWith(errorPagePath);
    }

    [Fact]
    public void OnFreshRender_WithFlagClaim_AndCurrentUriIsForcePasswordChange_DoesNotRedirect()
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo(ForcePasswordChange.Route);

        RenderGuardAsAuthorized(requiresPasswordChange: true);

        navigationManager.Uri.Should().EndWith(ForcePasswordChange.Route);
    }
}
