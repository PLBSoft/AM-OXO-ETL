using System.Globalization;
using System.Linq;
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

    // V6: full-width, large buttons + extra spacing above the Security section on mobile.
    [Fact]
    public void Profile_UpdateInfoAndChangePasswordButtons_AreFullWidthAndLarge() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        var updateInfoButton = cut.Find("#profile-info-form button[type=submit]");
        updateInfoButton.ClassList.Should().Contain("w-100");
        updateInfoButton.ClassList.Should().Contain("btn-lg");

        var changePasswordButton = cut.Find("#profile-password-form button[type=submit]");
        changePasswordButton.ClassList.Should().Contain("w-100");
        changePasswordButton.ClassList.Should().Contain("btn-lg");
    });

    [Fact]
    public void Profile_SecurityHeadingContainer_HasAdditionalTopMargin() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        var securityHeading = cut.FindAll("h2").Single(h => h.TextContent.Contains("Security"));
        var rowContainer = securityHeading.ParentElement!.ParentElement!;
        rowContainer.ClassList.Should().Contain("row");
        rowContainer.ClassList.Should().Contain("mt-5");
    });

    // Lot 042 (42.1): each field's aria-describedby points to its own validation message's id,
    // present in the DOM from initial render, across both forms on this page.
    [Fact]
    public void InfoFormFields_HaveAriaDescribedByMatchingTheirValidationMessageId() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        var firstName = cut.Find("#Info\\.FirstName");
        firstName.GetAttribute("aria-describedby").Should().Be("Info.FirstName-validation");
        cut.Find("#Info\\.FirstName-validation").Should().NotBeNull();

        var lastName = cut.Find("#Info\\.LastName");
        lastName.GetAttribute("aria-describedby").Should().Be("Info.LastName-validation");
        cut.Find("#Info\\.LastName-validation").Should().NotBeNull();

        var email = cut.Find("#Info\\.Email");
        email.GetAttribute("aria-describedby").Should().Be("Info.Email-validation");
        cut.Find("#Info\\.Email-validation").Should().NotBeNull();
    });

    [Fact]
    public void PasswordFormFields_HaveAriaDescribedByMatchingTheirValidationMessageId() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        var current = cut.Find("#Password\\.Current");
        current.GetAttribute("aria-describedby").Should().Be("Password.Current-validation");
        cut.Find("#Password\\.Current-validation").Should().NotBeNull();

        var newPassword = cut.Find("#Password\\.New");
        newPassword.GetAttribute("aria-describedby").Should().Be("Password.New-validation");
        cut.Find("#Password\\.New-validation").Should().NotBeNull();

        var confirm = cut.Find("#Password\\.Confirm");
        confirm.GetAttribute("aria-describedby").Should().Be("Password.Confirm-validation");
        cut.Find("#Password\\.Confirm-validation").Should().NotBeNull();
    });

    [Fact]
    public void LanguageSection_WithEnglishCulture_HighlightsEnglishButton_AndLinksBothToCultureEndpoint() => WithCulture("en-US", () =>
    {
        var cut = Render<Profile>();

        var english = cut.Find("#language-english-button");
        var french = cut.Find("#language-french-button");

        english.ClassList.Should().Contain("btn-secondary");
        english.GetAttribute("aria-current").Should().Be("true");
        english.GetAttribute("href").Should().Be("culture/set?culture=en-US&redirectUri=%2Fprofile");

        french.ClassList.Should().Contain("btn-outline-secondary");
        french.GetAttribute("aria-current").Should().BeNull();
        french.GetAttribute("href").Should().Be("culture/set?culture=fr-FR&redirectUri=%2Fprofile");
    });

    [Fact]
    public void LanguageSection_WithFrenchCulture_HighlightsFrenchButton() => WithCulture("fr-FR", () =>
    {
        var cut = Render<Profile>();

        var english = cut.Find("#language-english-button");
        var french = cut.Find("#language-french-button");

        french.ClassList.Should().Contain("btn-secondary");
        french.GetAttribute("aria-current").Should().Be("true");

        english.ClassList.Should().Contain("btn-outline-secondary");
        english.GetAttribute("aria-current").Should().BeNull();
    });
}
