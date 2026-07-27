using System.Globalization;
using Bunit;
using ExcelETL.BlazorAdmin.Components.Account;
using ExcelETL.BlazorAdmin.Components.Account.Pages;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Account;

public class LoginTests : BunitContext
{
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;

    public LoginTests()
    {
        _userManagerMock = IdentityMocks.CreateUserManagerMock();
        _signInManagerMock = IdentityMocks.CreateSignInManagerMock(_userManagerMock.Object);

        // Lot 045 (45.3): Login.razor now looks up the freshly-signed-in user to check
        // RequirePasswordChangeOnFirstLogin -- default to "no flag" so the 4 pre-existing tests
        // (predating this lot) keep their exact prior behavior (redirect to ReturnUrl) unmodified.
        _userManagerMock
            .Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string userName) => new ApplicationUser { UserName = userName, RequirePasswordChangeOnFirstLogin = false });

        Services.AddSingleton(_signInManagerMock.Object);
        Services.AddSingleton(_userManagerMock.Object);
        Services.AddSingleton<ILogger<Login>>(NullLogger<Login>.Instance);
        Services.AddScoped<IdentityRedirectManager>();
        Services.AddLocalization();

        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext()));
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

    [Fact]
    public void LoginUser_WithValidCredentials_SignsInWithSuppliedCredentials() => WithCulture("en-US", () =>
    {
        _signInManagerMock
            .Setup(s => s.PasswordSignInAsync("user@example.com", "P@ssw0rd!", false, false))
            .ReturnsAsync(SignInResult.Success);

        var cut = Render<Login>();
        cut.Find("#Input\\.UserName").Change("user@example.com");
        cut.Find("#Input\\.Password").Change("P@ssw0rd!");
        cut.Find("form").Submit();

        _signInManagerMock.Verify(
            s => s.PasswordSignInAsync("user@example.com", "P@ssw0rd!", false, false),
            Times.Once);
    });

    [Fact]
    public void LoginUser_WithInvalidCredentials_DisplaysErrorMessage() => WithCulture("en-US", () =>
    {
        _signInManagerMock
            .Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.Failed);

        var cut = Render<Login>();
        cut.Find("#Input\\.UserName").Change("user@example.com");
        cut.Find("#Input\\.Password").Change("wrong-password");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Invalid login attempt");
        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
    });

    [Fact]
    public void LoginUser_WithInvalidCredentials_AndFrenchCulture_DisplaysFrenchErrorMessage() => WithCulture("fr-FR", () =>
    {
        _signInManagerMock
            .Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.Failed);

        var cut = Render<Login>();
        cut.Find("#Input\\.UserName").Change("user@example.com");
        cut.Find("#Input\\.Password").Change("wrong-password");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Tentative de connexion invalide");
        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
    });

    [Fact]
    public void LoginUser_WithEmptyEmail_DoesNotCallSignInManager() => WithCulture("en-US", () =>
    {
        var cut = Render<Login>();
        cut.Find("#Input\\.Password").Change("P@ssw0rd!");
        cut.Find("form").Submit();

        _signInManagerMock.Verify(
            s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    });

    // Lot 042 (42.1): each field's aria-describedby points to its own validation message's id,
    // present in the DOM from initial render (an empty container is fine, per the ticket's own
    // "no dynamic add/remove" instruction).
    [Fact]
    public void UsernameAndPasswordFields_HaveAriaDescribedByMatchingTheirValidationMessageId() => WithCulture("en-US", () =>
    {
        var cut = Render<Login>();

        var userName = cut.Find("#Input\\.UserName");
        userName.GetAttribute("aria-describedby").Should().Be("Input.UserName-validation");
        cut.Find("#Input\\.UserName-validation").Should().NotBeNull();

        var password = cut.Find("#Input\\.Password");
        password.GetAttribute("aria-describedby").Should().Be("Input.Password-validation");
        cut.Find("#Input\\.Password-validation").Should().NotBeNull();
    });

    // Lot 045 (45.3): a temporary password (Lot 044) must not let the user reach ReturnUrl (or
    // anywhere else) until it's changed.
    [Fact]
    public void LoginUser_WithFlagTrue_RedirectsToForcePasswordChange_NotReturnUrl() => WithCulture("en-US", () =>
    {
        _signInManagerMock
            .Setup(s => s.PasswordSignInAsync("temp@example.com", "Temp0rary!", false, false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock
            .Setup(m => m.FindByNameAsync("temp@example.com"))
            .ReturnsAsync(new ApplicationUser { UserName = "temp@example.com", RequirePasswordChangeOnFirstLogin = true });

        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("Account/Login?ReturnUrl=import-profiles");

        var cut = Render<Login>();
        cut.Find("#Input\\.UserName").Change("temp@example.com");
        cut.Find("#Input\\.Password").Change("Temp0rary!");
        cut.Find("form").Submit();

        navigationManager.Uri.Should().EndWith(ForcePasswordChange.Route);
        navigationManager.Uri.Should().NotContain("import-profiles");
    });

    // Non-regression: the existing seeded accounts (flag false) must keep reaching ReturnUrl exactly
    // as before this lot.
    [Fact]
    public void LoginUser_WithFlagFalse_RedirectsToReturnUrl_Unchanged() => WithCulture("en-US", () =>
    {
        _signInManagerMock
            .Setup(s => s.PasswordSignInAsync("user@example.com", "P@ssw0rd!", false, false))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock
            .Setup(m => m.FindByNameAsync("user@example.com"))
            .ReturnsAsync(new ApplicationUser { UserName = "user@example.com", RequirePasswordChangeOnFirstLogin = false });

        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("Account/Login?ReturnUrl=import-profiles");

        var cut = Render<Login>();
        cut.Find("#Input\\.UserName").Change("user@example.com");
        cut.Find("#Input\\.Password").Change("P@ssw0rd!");
        cut.Find("form").Submit();

        navigationManager.Uri.Should().EndWith("import-profiles");
    });

    [Fact]
    public void LoginUser_WithWrongPassword_NeverRedirectsToForcePasswordChange() => WithCulture("en-US", () =>
    {
        _signInManagerMock
            .Setup(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.Failed);

        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var uriBeforeSubmit = navigationManager.Uri;

        var cut = Render<Login>();
        cut.Find("#Input\\.UserName").Change("user@example.com");
        cut.Find("#Input\\.Password").Change("wrong-password");
        cut.Find("form").Submit();

        navigationManager.Uri.Should().Be(uriBeforeSubmit);
        _userManagerMock.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
    });
}
