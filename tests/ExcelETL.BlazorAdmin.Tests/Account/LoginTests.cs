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

    public LoginTests()
    {
        var userManagerMock = IdentityMocks.CreateUserManagerMock();
        _signInManagerMock = IdentityMocks.CreateSignInManagerMock(userManagerMock.Object);

        Services.AddSingleton(_signInManagerMock.Object);
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
        cut.Find("#Input\\.Email").Change("user@example.com");
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
        cut.Find("#Input\\.Email").Change("user@example.com");
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
        cut.Find("#Input\\.Email").Change("user@example.com");
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
}
