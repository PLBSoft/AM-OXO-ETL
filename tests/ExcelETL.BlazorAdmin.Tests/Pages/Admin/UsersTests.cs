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

    private static UserSummary MakeUser(string id, string email, string userName, string firstName = "First", string lastName = "Last") =>
        new(id, email, userName, firstName, lastName);

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
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
    });

    // V2: mobile-first table -> card fallback at the md breakpoint, same idiom as
    // ImportProfiles/ExportProfiles -- see ImportProfilesTests for the fuller rationale comment.
    [Fact]
    public void Users_RendersBothTableAndCardTemplates_WithResponsiveClasses() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        var table = cut.Find("table.table");
        table.ClassList.Should().Contain("d-none");
        table.ClassList.Should().Contain("d-md-table");

        var cardContainer = cut.Find("div.d-md-none");
        cardContainer.QuerySelectorAll(".card").Should().HaveCount(1);
    });

    // Lot 050 (50.9, D7): the table now goes table-sm.
    [Fact]
    public void Users_Table_HasTableSmClass() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        cut.Find("table.table").ClassList.Should().Contain("table-sm");
    });

    // Lot 050 (50.7, D6): FirstName/LastName rendered in both templates; extends the existing V2
    // content-identity coverage rather than duplicating it in a new test.
    [Fact]
    public void Users_CardTemplate_DisplaysSameContentAsTable() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice", "Alice", "Smith"));

        var cut = Render<Users>();

        var tableRow = cut.Find("table tbody tr");
        tableRow.TextContent.Should().Contain("alice@example.com");
        tableRow.TextContent.Should().Contain("alice");
        tableRow.TextContent.Should().Contain("Alice");
        tableRow.TextContent.Should().Contain("Smith");

        var card = cut.Find("div.d-md-none .card");
        card.TextContent.Should().Contain("alice@example.com");
        card.TextContent.Should().Contain("alice");
        card.TextContent.Should().Contain("Alice");
        card.TextContent.Should().Contain("Smith");
    });

    // Lot 050 (50.9, D7): the Id column/GUID is no longer displayed anywhere -- identifiers stay
    // carried by the row action buttons' own HTML ids instead.
    [Fact]
    public void Users_DoesNotDisplayTheRawUserId() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1-a-very-long-guid-like-id", "alice@example.com", "alice"));

        var cut = Render<Users>();

        cut.Find("table thead").TextContent.Should().NotContain("Id");
        cut.Find("table tbody tr").TextContent.Should().NotContain("user-1-a-very-long-guid-like-id");
        cut.Find("div.d-md-none .card").TextContent.Should().NotContain("user-1-a-very-long-guid-like-id");
    });

    // Lot 050 (50.9): the three row-action buttons sit in a horizontally-aligned container --
    // asserted on the class, not a computed layout (bUnit doesn't render CSS).
    [Fact]
    public void Users_RowActionButtons_AreInAHorizontallyAlignedContainer() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();

        var actionsContainer = cut.Find("#edit-user-button-user-1").ParentElement!;
        actionsContainer.ClassList.Should().Contain("right-aligned-actions");
        actionsContainer.QuerySelectorAll("button").Should().HaveCount(3);
    });

    // Lot 050 (50.8, D6): role badge, no per-row role query (N+1 regression guard).
    [Fact]
    public void Users_AdminRow_ShowsAdminBadge() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("admin-1", "admin@example.com", "admin"));
        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["admin-1"]);

        var cut = Render<Users>();

        var badge = cut.Find("#user-role-badge-admin-1");
        badge.TextContent.Should().Be("Admin");
        badge.ClassList.Should().Contain("badge");
    });

    [Fact]
    public void Users_StandardRow_ShowsUserLabel_NotABadge() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));
        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["admin-1"]);

        var cut = Render<Users>();

        var status = cut.Find("#user-role-badge-user-1");
        status.TextContent.Should().Be("User");
        status.ClassList.Should().NotContain("badge");
    });

    [Fact]
    public void Users_CardTemplate_ShowsSameRoleStatusAsTable() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("admin-1", "admin@example.com", "admin"));
        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["admin-1"]);

        var cut = Render<Users>();

        cut.Find("#user-role-badge-card-admin-1").TextContent.Should().Be("Admin");
    });

    [Fact]
    public void Users_RenderingThreeRows_CallsGetAdminUserIdsExactlyOnce() => WithCulture("en-US", () =>
    {
        SetUsers(
            MakeUser("user-1", "a@example.com", "a"),
            MakeUser("user-2", "b@example.com", "b"),
            MakeUser("user-3", "c@example.com", "c"));

        Render<Users>();

        _userManagementServiceMock.Verify(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    });

    // Lot 044 (44.3): creation.
    [Fact]
    public void CreateUser_WithValidInput_ShowsGeneratedTemporaryPassword_AndReloadsList() => WithCulture("en-US", () =>
    {
        SetUsers();
        _userManagementServiceMock
            .Setup(s => s.CreateUserAsync("alice01", "alice@example.com", "Alice", "Smith", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCreationResult.Success("user-2", "Tmp!Passw0rd"))
            .Callback(() => SetUsers(MakeUser("user-2", "alice@example.com", "alice01", "Alice", "Smith")));

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-username-input").Input("alice01");
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-last-name-input").Input("Smith");
        cut.Find("#create-user-email-input").Input("alice@example.com");
        cut.Find("#create-user-submit-button").Click();

        cut.Find("#temporary-password-display").TextContent.Should().Contain("Tmp!Passw0rd");
        cut.Markup.Should().Contain("alice@example.com");
        cut.FindAll("#create-user-first-name-input").Should().BeEmpty();
    });

    // Uses a userName that genuinely differs from the email -- the exact-argument assertion below
    // would also pass on a pre-lot-050 implementation if the two values matched, proving nothing.
    [Fact]
    public void CreateUser_PassesTheTypedUserName_DistinctFromEmail_ToTheService() => WithCulture("en-US", () =>
    {
        SetUsers();
        _userManagementServiceMock
            .Setup(s => s.CreateUserAsync("alice01", "alice@example.com", "Alice", "Smith", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCreationResult.Success("user-2", "Tmp!Passw0rd"));

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-username-input").Input("alice01");
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-last-name-input").Input("Smith");
        cut.Find("#create-user-email-input").Input("alice@example.com");
        cut.Find("#create-user-submit-button").Click();

        _userManagementServiceMock.Verify(
            s => s.CreateUserAsync("alice01", "alice@example.com", "Alice", "Smith", It.IsAny<CancellationToken>()), Times.Once);
    });

    [Fact]
    public void CreateUser_WithMissingUserName_ShowsErrorWithoutCallingService() => WithCulture("en-US", () =>
    {
        SetUsers();

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-last-name-input").Input("Smith");
        cut.Find("#create-user-email-input").Input("alice@example.com");
        cut.Find("#create-user-submit-button").Click();

        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        _userManagementServiceMock.Verify(
            s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    });

    [Fact]
    public void CreateUser_WithMissingField_ShowsErrorWithoutCallingService() => WithCulture("en-US", () =>
    {
        SetUsers();

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-username-input").Input("alice01");
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-submit-button").Click();

        cut.Find(".alert-danger").GetAttribute("role").Should().Be("alert");
        _userManagementServiceMock.Verify(
            s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    });

    [Fact]
    public void DismissTemporaryPasswordButton_HidesTheDisplay() => WithCulture("en-US", () =>
    {
        SetUsers();
        _userManagementServiceMock
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCreationResult.Success("user-2", "Tmp!Passw0rd"));

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();
        cut.Find("#create-user-username-input").Input("alice01");
        cut.Find("#create-user-first-name-input").Input("Alice");
        cut.Find("#create-user-last-name-input").Input("Smith");
        cut.Find("#create-user-email-input").Input("alice@example.com");
        cut.Find("#create-user-submit-button").Click();

        cut.Find("#dismiss-temporary-password-button").Click();

        cut.FindAll("#temporary-password-display").Should().BeEmpty();
    });

    // Lot 050 (50.6): the username field's help text is visible below it, informational only.
    [Fact]
    public void CreateForm_UserNameField_HasHelpText() => WithCulture("en-US", () =>
    {
        SetUsers();

        var cut = Render<Users>();
        cut.Find("#create-user-button").Click();

        cut.Markup.Should().Contain("3 to 30 characters");
    });

    // Lot 044 (44.3), lot 050 (50.3/50.6): modification, now including the username field.
    [Fact]
    public void EditUser_WithValidInput_UpdatesFieldsVisibleInTheList() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "old@example.com", "old_name", "Old", "Name"));
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile("user-1", "old@example.com", "old_name", "Old", "Name"));
        _userManagementServiceMock
            .Setup(s => s.UpdateUserAsync("user-1", "new_name", "alicia@example.com", "Alicia", "Smith", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success)
            .Callback(() => SetUsers(MakeUser("user-1", "alicia@example.com", "new_name", "Alicia", "Smith")));

        var cut = Render<Users>();
        cut.Find("#edit-user-button-user-1").Click();

        cut.Find("#edit-user-username-input-user-1").GetAttribute("value").Should().Be("old_name");
        cut.Find("#edit-user-first-name-input-user-1").GetAttribute("value").Should().Be("Old");

        cut.Find("#edit-user-username-input-user-1").Input("new_name");
        cut.Find("#edit-user-first-name-input-user-1").Input("Alicia");
        cut.Find("#edit-user-last-name-input-user-1").Input("Smith");
        cut.Find("#edit-user-email-input-user-1").Input("alicia@example.com");
        cut.Find("#save-edit-user-button-user-1").Click();

        cut.Markup.Should().Contain("alicia@example.com");
        cut.Markup.Should().Contain("new_name");
        _userManagementServiceMock.Verify(
            s => s.UpdateUserAsync("user-1", "new_name", "alicia@example.com", "Alicia", "Smith", It.IsAny<CancellationToken>()), Times.Once);
    });

    [Fact]
    public void EditUser_ServiceReturnsFailure_ShowsErrorMessage_KeepsFormOpen_DoesNotReloadList() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "old@example.com", "old_name", "Old", "Name"));
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile("user-1", "old@example.com", "old_name", "Old", "Name"));
        _userManagementServiceMock
            .Setup(s => s.UpdateUserAsync("user-1", "taken", "old@example.com", "Old", "Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Failed(["User name 'taken' is already taken."]));

        var cut = Render<Users>();
        cut.Find("#edit-user-button-user-1").Click();
        cut.Find("#edit-user-username-input-user-1").Input("taken");
        cut.Find("#save-edit-user-button-user-1").Click();

        var alert = cut.Find(".alert-danger");
        alert.GetAttribute("role").Should().Be("alert");
        alert.TextContent.Should().Contain("already taken");
        cut.Find("#edit-user-username-input-user-1").GetAttribute("value").Should().Be("taken");
        _userRepositoryMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    });

    // Lot 044 (44.3): reset password.
    [Fact]
    public void ResetPassword_ShowsInlineConfirmation_AndNeverCallsServiceBeforeConfirm() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

        var cut = Render<Users>();
        cut.Find("#reset-password-button-user-1").Click();

        cut.Find("#reset-password-confirm-user-1").Should().NotBeNull();
        _userManagementServiceMock.Verify(
            s => s.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    });

    [Fact]
    public void ConfirmResetPassword_CallsServiceAndDisplaysNewTemporaryPassword() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));
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
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

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
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

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
            MakeUser("user-1", "alice-to-delete@example.com", "alice"),
            MakeUser("user-2", "bob-to-keep@example.com", "bob"));
        _userManagementServiceMock
            .Setup(s => s.DeleteUserAsync("user-1", CurrentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserDeletionResult.Success)
            .Callback(() => SetUsers(MakeUser("user-2", "bob-to-keep@example.com", "bob")));

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
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));

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
            MakeUser("user-1", "alice@example.com", "alice"),
            MakeUser("user-2", "bob@example.com", "bob"));

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
        SetUsers(MakeUser(CurrentUserId, "self@example.com", "self"));

        var cut = Render<Users>();

        cut.Find($"#delete-user-button-{CurrentUserId}").HasAttribute("disabled").Should().BeTrue();
        cut.Find($"#delete-user-button-{CurrentUserId}").GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
    });

    [Fact]
    public void SoleRemainingAdminRow_DeleteButtonIsDisabled() => WithCulture("en-US", () =>
    {
        SetUsers(MakeUser("admin-1", "admin@example.com", "admin"));
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
        SetUsers(MakeUser("user-1", "alice@example.com", "alice"));
        _userManagementServiceMock
            .Setup(s => s.GetAdminUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string>)["admin-1"]);

        var cut = Render<Users>();

        cut.Find("#delete-user-button-user-1").HasAttribute("disabled").Should().BeFalse();
    });
}
