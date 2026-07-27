using System.Security.Claims;
using Bunit;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Account;
using ExcelETL.BlazorAdmin.Components.Account.Pages;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Account;

// Lot 045 (45.2).
public class ForcePasswordChangeTests : BunitContext
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    public ForcePasswordChangeTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManagerMock();
        _signInManagerMock = IdentityMocks.CreateSignInManagerMock(_userManagerMock.Object);

        Services.AddSingleton(_userManagerMock.Object);
        Services.AddSingleton(_signInManagerMock.Object);
        Services.AddSingleton(_userRepositoryMock.Object);
        Services.AddScoped<IdentityRedirectManager>();
        Services.AddLocalization();

        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth")),
        }));
    }

    private void SetUpCurrentUser(ApplicationUser user) =>
        _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

    // Note: a real browser reaching this page over static SSR never renders the form at all when
    // redirected away -- NavigationManager.NavigateTo throws a NavigationException there, which
    // ASP.NET Core intercepts before any HTML is sent (same mechanism RedirectToLogin.razor relies
    // on). bUnit's fake NavigationManager doesn't reproduce that short-circuit, so what's actually
    // verifiable here is that the redirect itself happens with the right target.
    [Fact]
    public void OnInitialized_WithFlagFalse_RedirectsAway()
    {
        SetUpCurrentUser(new ApplicationUser { Id = "user-1", RequirePasswordChangeOnFirstLogin = false });
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        Render<ForcePasswordChange>();

        navigationManager.Uri.Should().NotContain(ForcePasswordChange.Route);
    }

    [Fact]
    public void OnInitialized_WithFlagTrue_RendersForm_AndDoesNotRedirect()
    {
        SetUpCurrentUser(new ApplicationUser { Id = "user-1", RequirePasswordChangeOnFirstLogin = true });
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var uriBeforeRender = navigationManager.Uri;

        var cut = Render<ForcePasswordChange>();

        navigationManager.Uri.Should().Be(uriBeforeRender);
        cut.Find("#force-password-change-form").Should().NotBeNull();
    }

    [Fact]
    public void SubmittingValidNewPassword_ClearsFlag_RefreshesSignIn_AndRedirectsHome()
    {
        var user = new ApplicationUser { Id = "user-1", RequirePasswordChangeOnFirstLogin = true };
        var refreshedUser = new ApplicationUser { Id = "user-1", RequirePasswordChangeOnFirstLogin = false };
        SetUpCurrentUser(user);
        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(refreshedUser);
        _userRepositoryMock
            .Setup(r => r.ChangePasswordAsync("user-1", "Temp0rary!", "NewP@ss1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success);

        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = Render<ForcePasswordChange>();
        cut.Find("#current-password-input").Change("Temp0rary!");
        cut.Find("#new-password-input").Change("NewP@ss1");
        cut.Find("#confirm-password-input").Change("NewP@ss1");
        cut.Find("#force-password-change-form").Submit();

        _signInManagerMock.Verify(s => s.RefreshSignInAsync(refreshedUser), Times.Once);
        navigationManager.Uri.Should().NotContain(ForcePasswordChange.Route);
    }

    [Fact]
    public void SubmittingInvalidCurrentPassword_StaysOnPage_ShowsError_AndNeverRefreshesSignIn()
    {
        var user = new ApplicationUser { Id = "user-1", RequirePasswordChangeOnFirstLogin = true };
        SetUpCurrentUser(user);
        _userRepositoryMock
            .Setup(r => r.ChangePasswordAsync("user-1", "WrongPassword", "NewP@ss1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failed(["Incorrect password."]));

        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var uriBeforeSubmit = navigationManager.Uri;

        var cut = Render<ForcePasswordChange>();
        cut.Find("#current-password-input").Change("WrongPassword");
        cut.Find("#new-password-input").Change("NewP@ss1");
        cut.Find("#confirm-password-input").Change("NewP@ss1");
        cut.Find("#force-password-change-form").Submit();

        navigationManager.Uri.Should().Be(uriBeforeSubmit);
        cut.Markup.Should().Contain("Incorrect password.");
        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
        _signInManagerMock.Verify(s => s.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}
