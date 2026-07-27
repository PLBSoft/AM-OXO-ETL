using System.Globalization;
using System.Security.Claims;
using Bunit;
using ExcelETL.Application.Identity;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests.Pages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class UsersTests : BunitContext
{
    private const string CurrentUserId = "current-user-id";

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUserManagementService> _userManagementServiceMock = new();

    public UsersTests()
    {
        Services.AddSingleton(_userRepositoryMock.Object);
        Services.AddSingleton(_userManagementServiceMock.Object);
        Services.AddLocalization();

        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("current-user@example.com");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUserId));

        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)[]);
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

    private void SetUsers(params UserSummary[] users) =>
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<UserSummary>)users);

    [Fact]
    public void Users_WithNoUsers_DisplaysNoEntriesMessage() => WithCulture("en-US", () =>
    {
        SetUsers();

        var cut = Render<Users>();

        cut.Markup.Should().Contain("No users found.");
    });

    // Lot 042 (42.2): the mobile card's per-row title previously skipped straight from h1 to h5 --
    // fixed to h2, keeping its pre-existing visual size via the Bootstrap `.h5` utility class.
    [Fact]
    public void Users_WithExistingUser_HasNoHeadingLevelSkip() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
    });

    // V2: mobile-first table -> card fallback at the md breakpoint, same idiom as
    // ImportProfiles/ExportProfiles -- see ImportProfilesTests for the fuller rationale comment.
    [Fact]
    public void Users_RendersBothTableAndCardTemplates_WithResponsiveClasses() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        var table = cut.Find("table.table");
        table.ClassList.Should().Contain("d-none");
        table.ClassList.Should().Contain("d-md-table");

        var cardContainer = cut.Find("div.d-md-none");
        cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
    });

    [Fact]
    public void Users_CardTemplate_DisplaysSameContentAsTable() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        var card = cut.Find("div.d-md-none .card");
        card.TextContent.Should().Contain("alice@example.com");
        card.TextContent.Should().Contain("alice");
        card.TextContent.Should().Contain("user-1");
    });

    // Lot 044 (44.3): creation.
    [Fact]
    public void CreateUser_WithValidInput_ShowsGeneratedTemporaryPassword_AndReloadsList() => WithCulture("en-US", () =>
    {
        SetUsers();
        _userManagementServiceMock
            .Setup(s => s.CreateUserAsync("alice@example.com", "Alice", "Smith", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCreationResult.Success("user-2", "Tmp!Passw0rd"))
            .Callback(() => SetUsers(new UserSummary("user-2", "alice@example.com", "alice@example.com")));

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-last-name-input").Input("Smith");
        cut.Find("#create-user-email-input").Input("alice@example.com");
        cut.Find("#create-user-submit-button").Click();

        cut.Find("#temporary-password-display").TextContent.Should().Contain("Tmp!Passw0rd");
        cut.Markup.Should().Contain("alice@example.com");
        cut.FindAll("#create-user-first-name-input").Should().BeEmpty();
    });

    [Fact]
    public void CreateUser_WithMissingField_ShowsErrorWithoutCallingService() => WithCulture("en-US", () =>
    {
        SetUsers();

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-submit-button").Click();

        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        _userManagementServiceMock.Verify(
            s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    });

    [Fact]
    public void DismissTemporaryPasswordButton_HidesTheDisplay() => WithCulture("en-US", () =>
    {
        SetUsers();
        _userManagementServiceMock
            .Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCreationResult.Success("user-2", "Tmp!Passw0rd"));

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-last-name-input").Input("Smith");
        cut.Find("#create-user-email-input").Input("alice@example.com");
        cut.Find("#create-user-submit-button").Click();

        cut.Find("#dismiss-temporary-password-button").Click();

        cut.FindAll("#temporary-password-display").Should().BeEmpty();
    });

    // Lot 044 (44.3): modification.
    [Fact]
    public void EditUser_WithValidInput_UpdatesFieldsVisibleInTheList() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "old@example.com", "old@example.com"));
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile("user-1", "old@example.com", "old@example.com", "Old", "Name"));
        _userRepositoryMock
            .Setup(r => r.UpdateProfileAsync("user-1", "Alicia", "Smith", "alicia@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success)
            .Callback(() => SetUsers(new UserSummary("user-1", "alicia@example.com", "alicia@example.com")));

        var cut = Render<Users>();
        cut.Find("#edit-user-button-user-1").Click();

        cut.Find("#edit-user-first-name-input-user-1").GetAttribute("value").Should().Be("Old");

        cut.Find("#edit-user-first-name-input-user-1").Input("Alicia");
        cut.Find("#edit-user-last-name-input-user-1").Input("Smith");
        cut.Find("#edit-user-email-input-user-1").Input("alicia@example.com");
        cut.Find("#save-edit-user-button-user-1").Click();

        cut.Markup.Should().Contain("alicia@example.com");
        _userRepositoryMock.Verify(
            r => r.UpdateProfileAsync("user-1", "Alicia", "Smith", "alicia@example.com", It.IsAny<CancellationToken>()), Times.Once);
    });

    // Lot 044 (44.3): reset password.
    [Fact]
    public void ResetPassword_ShowsInlineConfirmation_AndNeverCallsServiceBeforeConfirm() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();
        cut.Find("#reset-password-button-user-1").Click();

        cut.Find("#reset-password-confirm-user-1").Should().NotBeNull();
        _userManagementServiceMock.Verify(
            s => s.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    [Fact]
    public void ConfirmResetPassword_CallsServiceAndDisplaysNewTemporaryPassword() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));
        _userManagementServiceMock
            .Setup(s => s.ResetPasswordAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PasswordResetResult.Success("NewTmp!23"));

        var cut = Render<Users>();
        cut.Find("#reset-password-button-user-1").Click();
        cut.Find("#confirm-reset-password-button-user-1").Click();

        cut.Find("#temporary-password-display").TextContent.Should().Contain("NewTmp!23");
        _userManagementServiceMock.Verify(s => s.ResetPasswordAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    });

    [Fact]
    public void CancelResetPassword_ClosesConfirmation_WithoutCallingService() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();
        cut.Find("#reset-password-button-user-1").Click();
        cut.Find("#cancel-reset-password-button-user-1").Click();

        cut.FindAll("#reset-password-confirm-user-1").Should().BeEmpty();
        _userManagementServiceMock.Verify(
            s => s.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    // Lot 044 (44.3): deletion, same assertions style as Lot 028's ImportProfiles delete pattern.
    [Fact]
    public void DeleteUser_DoesNotCallServiceImmediately_OnlyOpensConfirmation() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();
        cut.Find("#delete-user-button-user-1").Click();

        cut.Find("#delete-user-confirm-user-1").Should().NotBeNull();
        _userManagementServiceMock.Verify(
            s => s.DeleteUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    [Fact]
    public void ConfirmDeleteUser_CallsServiceWithExactId_AndRemovesUserFromReloadedList() => WithCulture("en-US", () =>
    {
        SetUsers(
            new UserSummary("user-1", "alice-to-delete@example.com", "alice"),
            new UserSummary("user-2", "bob-to-keep@example.com", "bob"));
        _userManagementServiceMock
            .Setup(s => s.DeleteUserAsync("user-1", CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserDeletionResult.Success)
            .Callback(() => SetUsers(new UserSummary("user-2", "bob-to-keep@example.com", "bob")));

        var cut = Render<Users>();
        cut.Find("#delete-user-button-user-1").Click();
        cut.Find("#confirm-delete-user-button-user-1").Click();

        cut.Markup.Should().NotContain("alice-to-delete@example.com");
        cut.Markup.Should().Contain("bob-to-keep@example.com");
        _userManagementServiceMock.Verify(s => s.DeleteUserAsync("user-1", CurrentUserId, It.IsAny<CancellationToken>()), Times.Once);
    });

    [Fact]
    public void CancelDeleteUser_ClosesConfirmation_WithoutCallingService() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();
        cut.Find("#delete-user-button-user-1").Click();
        cut.Find("#cancel-delete-user-button-user-1").Click();

        cut.FindAll("#delete-user-confirm-user-1").Should().BeEmpty();
        _userManagementServiceMock.Verify(
            s => s.DeleteUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    [Fact]
    public void OpeningConfirmationOnAnotherRow_ClosesThePreviousOne_WithoutDeletingIt() => WithCulture("en-US", () =>
    {
        SetUsers(
            new UserSummary("user-1", "alice@example.com", "alice"),
            new UserSummary("user-2", "bob@example.com", "bob"));

        var cut = Render<Users>();
        cut.Find("#delete-user-button-user-1").Click();
        cut.FindAll("#delete-user-confirm-user-1").Should().HaveCount(1);

        cut.Find("#delete-user-button-user-2").Click();

        cut.FindAll("#delete-user-confirm-user-1").Should().BeEmpty();
        cut.FindAll("#delete-user-confirm-user-2").Should().HaveCount(1);
        _userManagementServiceMock.Verify(
            s => s.DeleteUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    // Lot 044 (44.3): proactive client-side guard-rails, avoiding a round trip the service would
    // refuse anyway (self-deletion / last remaining Admin) -- the button is disabled outright.
    [Fact]
    public void CurrentUserRow_DeleteButtonIsDisabled() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary(CurrentUserId, "self@example.com", "self"));

        var cut = Render<Users>();

        cut.Find($"#delete-user-button-{CurrentUserId}").HasAttribute("disabled").Should().BeTrue();
        cut.Find($"#delete-user-button-{CurrentUserId}").GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
    });

    [Fact]
    public void SoleRemainingAdminRow_DeleteButtonIsDisabled() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("admin-1", "admin@example.com", "admin"));
        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["admin-1"]);

        var cut = Render<Users>();

        cut.Find("#delete-user-button-admin-1").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#delete-user-button-admin-1").GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
    });

    [Fact]
    public void NonAdminNonCurrentUserRow_DeleteButtonIsEnabled() => WithCulture("en-US", () =>
    {
        SetUsers(new UserSummary("user-1", "alice@example.com", "alice"));
        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["admin-1"]);

        var cut = Render<Users>();

        cut.Find("#delete-user-button-user-1").HasAttribute("disabled").Should().BeFalse();
    });
}
