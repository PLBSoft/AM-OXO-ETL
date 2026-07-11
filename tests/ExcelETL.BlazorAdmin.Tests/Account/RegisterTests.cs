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

public class RegisterTests : BunitContext
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;

    public RegisterTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        userStoreMock
            .Setup(s => s.SetUserNameAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        userStoreMock
            .As<IUserEmailStore<ApplicationUser>>()
            .Setup(s => s.SetEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userManagerMock = IdentityMocks.CreateUserManagerMock(userStoreMock.Object);
        _signInManagerMock = IdentityMocks.CreateSignInManagerMock(_userManagerMock.Object);

        Services.AddSingleton(_userManagerMock.Object);
        Services.AddSingleton(userStoreMock.Object);
        Services.AddSingleton(_signInManagerMock.Object);
        Services.AddSingleton<ILogger<Register>>(NullLogger<Register>.Instance);
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
    public void RegisterUser_WithValidInput_CreatesUserAndSignsIn() => WithCulture("en-US", () =>
    {
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Str0ngPwd!"))
            .ReturnsAsync(IdentityResult.Success);

        var cut = Render<Register>();
        cut.Find("#Input\\.Email").Change("newuser@example.com");
        cut.Find("#Input\\.Password").Change("Str0ngPwd!");
        cut.Find("#Input\\.ConfirmPassword").Change("Str0ngPwd!");
        cut.Find("form").Submit();

        _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Str0ngPwd!"), Times.Once);
        _signInManagerMock.Verify(s => s.SignInAsync(It.IsAny<ApplicationUser>(), false, null), Times.Once);
    });

    [Fact]
    public void RegisterUser_WhenCreateFails_DisplaysIdentityErrorsAndDoesNotSignIn() => WithCulture("en-US", () =>
    {
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email 'newuser@example.com' is already taken." }));

        var cut = Render<Register>();
        cut.Find("#Input\\.Email").Change("newuser@example.com");
        cut.Find("#Input\\.Password").Change("Str0ngPwd!");
        cut.Find("#Input\\.ConfirmPassword").Change("Str0ngPwd!");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("is already taken");
        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
        _signInManagerMock.Verify(s => s.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    });

    [Fact]
    public void RegisterUser_WithMismatchedPasswords_DoesNotCallCreateAsync() => WithCulture("en-US", () =>
    {
        var cut = Render<Register>();
        cut.Find("#Input\\.Email").Change("newuser@example.com");
        cut.Find("#Input\\.Password").Change("Str0ngPwd!");
        cut.Find("#Input\\.ConfirmPassword").Change("SomethingElse!");
        cut.Find("form").Submit();

        _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    });
}
