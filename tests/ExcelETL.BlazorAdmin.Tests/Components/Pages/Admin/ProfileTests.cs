using System.Globalization;
using System.Security.Claims;
using Bunit;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Components.Pages.Admin;

public class ProfileTests : BunitContext
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    public ProfileTests()
    {
        Services.AddSingleton(_userRepositoryMock.Object);
        Services.AddLocalization();

        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("alice@example.com");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        RenderTree.Add<CascadingValue<HttpContext>>(p => p.Add(c => c.Value, new DefaultHttpContext()));

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile("user-1", "alice@example.com", "alice@example.com", "Alice", "Smith"));
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
    public void OnInitialized_LoadsCurrentUserProfile_AndPrefillsInfoForm() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        cut.Find("#Info\\.FirstName").GetAttribute("value").Should().Be("Alice");
        cut.Find("#Info\\.LastName").GetAttribute("value").Should().Be("Smith");
        cut.Find("#Info\\.Email").GetAttribute("value").Should().Be("alice@example.com");
    });

    [Fact]
    public void UpdateProfile_WithValidInput_CallsRepositoryAndShowsSuccessMessage() => WithCulture("en-US", () =>
    {
        _userRepositoryMock
            .Setup(r => r.UpdateProfileAsync("user-1", "Alicia", "Smith", "alicia@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success);

        var cut = Render<Profile>();
        cut.Find("#Info\\.FirstName").Change("Alicia");
        cut.Find("#Info\\.Email").Change("alicia@example.com");
        cut.Find("#profile-info-form").Submit();

        _userRepositoryMock.Verify(
            r => r.UpdateProfileAsync("user-1", "Alicia", "Smith", "alicia@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
        cut.Markup.Should().Contain("Your information has been updated.");
        cut.Find(".alert").ClassList.Should().Contain("alert-success");
    });

    [Fact]
    public void UpdateProfile_WhenRepositoryFails_ShowsErrorMessage() => WithCulture("en-US", () =>
    {
        _userRepositoryMock
            .Setup(r => r.UpdateProfileAsync("user-1", "Alice", "Smith", "taken@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failed(["Email 'taken@example.com' is already taken."]));

        var cut = Render<Profile>();
        cut.Find("#Info\\.Email").Change("taken@example.com");
        cut.Find("#profile-info-form").Submit();

        cut.Markup.Should().Contain("is already taken");
        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
    });

    [Fact]
    public void ChangePassword_WithValidInput_ShowsLoggedOutMessage_AndHidesPasswordForm() => WithCulture("en-US", () =>
    {
        _userRepositoryMock
            .Setup(r => r.ChangePasswordAsync("user-1", "OldP@ss1", "NewP@ss1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success);

        var cut = Render<Profile>();
        cut.Find("#Password\\.Current").Change("OldP@ss1");
        cut.Find("#Password\\.New").Change("NewP@ss1");
        cut.Find("#Password\\.Confirm").Change("NewP@ss1");
        cut.Find("#profile-password-form").Submit();

        _userRepositoryMock.Verify(
            r => r.ChangePasswordAsync("user-1", "OldP@ss1", "NewP@ss1", It.IsAny<CancellationToken>()),
            Times.Once);
        cut.Markup.Should().Contain("you have been signed out");
        cut.FindAll("#profile-password-form").Should().BeEmpty();
        cut.Find("form[action=\"Account/Logout\"]").Should().NotBeNull();
    });

    [Fact]
    public void ChangePassword_WithWrongCurrentPassword_ShowsErrorMessage_AndKeepsFormVisible() => WithCulture("en-US", () =>
    {
        _userRepositoryMock
            .Setup(r => r.ChangePasswordAsync("user-1", "WrongPassword", "NewP@ss1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failed(["Incorrect password."]));

        var cut = Render<Profile>();
        cut.Find("#Password\\.Current").Change("WrongPassword");
        cut.Find("#Password\\.New").Change("NewP@ss1");
        cut.Find("#Password\\.Confirm").Change("NewP@ss1");
        cut.Find("#profile-password-form").Submit();

        cut.Markup.Should().Contain("Incorrect password.");
        cut.Find(".alert").ClassList.Should().Contain("alert-danger");
        cut.FindAll("#profile-password-form").Should().NotBeEmpty();
    });

    [Fact]
    public void ChangePassword_WithMismatchedConfirmation_DoesNotCallRepository() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();
        cut.Find("#Password\\.Current").Change("OldP@ss1");
        cut.Find("#Password\\.New").Change("NewP@ss1");
        cut.Find("#Password\\.Confirm").Change("SomethingElse!");
        cut.Find("#profile-password-form").Submit();

        _userRepositoryMock.Verify(
            r => r.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    });

    [Fact]
    public void UpdateProfile_WithValidInput_AndFrenchCulture_ShowsFrenchSuccessMessage() => WithCulture("fr-FR", () =>
    {
        _userRepositoryMock
            .Setup(r => r.UpdateProfileAsync("user-1", "Alice", "Smith", "alice@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success);

        var cut = Render<Profile>();
        cut.Find("#profile-info-form").Submit();

        cut.Markup.Should().Contain("Vos informations ont été mises à jour.");
    });
}
