using System.Text.RegularExpressions;
using ExcelETL.Application.Identity;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

public class UserManagementServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityManagerMocks.CreateUserManagerMock();

    private UserManagementService CreateService() => new(_userManagerMock.Object);

    [Fact]
    public async Task CreateUserAsync_Succeeds_ReturnsGeneratedPasswordSatisfyingComplexityPolicy_AndSetsRequirePasswordChangeFlag()
    {
        ApplicationUser? createdUser = null;
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        var result = await service.CreateUserAsync("alice@example.com", "Alice", "Smith");

        result.Succeeded.Should().BeTrue();
        result.TemporaryPassword.Should().NotBeNullOrEmpty();
        Regex.IsMatch(result.TemporaryPassword!, "[A-Z]").Should().BeTrue();
        Regex.IsMatch(result.TemporaryPassword!, "[a-z]").Should().BeTrue();
        Regex.IsMatch(result.TemporaryPassword!, "[0-9]").Should().BeTrue();
        Regex.IsMatch(result.TemporaryPassword!, @"[^A-Za-z0-9]").Should().BeTrue();
        createdUser.Should().NotBeNull();
        createdUser!.RequirePasswordChangeOnFirstLogin.Should().BeTrue();
        createdUser.UserName.Should().Be("alice@example.com");
        createdUser.Email.Should().Be("alice@example.com");
        createdUser.FirstName.Should().Be("Alice");
        createdUser.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task CreateUserAsync_NeverAssignsAnyRole()
    {
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        await service.CreateUserAsync("alice@example.com", "Alice", "Smith");

        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_WhenCreateFails_ReturnsFailureWithErrors()
    {
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email 'alice@example.com' is already taken." }));

        var service = CreateService();
        var result = await service.CreateUserAsync("alice@example.com", "Alice", "Smith");

        result.Succeeded.Should().BeFalse();
        result.UserId.Should().BeNull();
        result.TemporaryPassword.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Should().Be("Email 'alice@example.com' is already taken.");
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownUser_ThrowsInvalidOperationException()
    {
        _userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var service = CreateService();

        var act = () => service.ResetPasswordAsync("missing");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResetPasswordAsync_Succeeds_ReturnsNewPassword_AndSetsRequirePasswordChangeFlag()
    {
        var user = new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _userManagerMock
            .Setup(m => m.ResetPasswordAsync(user, "reset-token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        var result = await service.ResetPasswordAsync("1");

        result.Succeeded.Should().BeTrue();
        result.TemporaryPassword.Should().NotBeNullOrEmpty();
        user.RequirePasswordChangeOnFirstLogin.Should().BeTrue();
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenResetFails_ReturnsFailureWithErrors_AndDoesNotSetFlag()
    {
        var user = new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _userManagerMock
            .Setup(m => m.ResetPasswordAsync(user, "reset-token", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token." }));

        var service = CreateService();
        var result = await service.ResetPasswordAsync("1");

        result.Succeeded.Should().BeFalse();
        result.TemporaryPassword.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Should().Be("Invalid token.");
        user.RequirePasswordChangeOnFirstLogin.Should().BeFalse();
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_SelfDeletion_RefusesWithoutCallingDeleteAsync()
    {
        var service = CreateService();

        var result = await service.DeleteUserAsync("user-1", "user-1");

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(UserDeletionFailureReason.SelfDeletion);
        _userManagerMock.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_TargetIsSoleRemainingAdmin_RefusesWithoutCallingDeleteAsync()
    {
        var admin = new ApplicationUser { Id = "admin-1", Email = "admin@example.com", UserName = "admin@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(admin);
        _userManagerMock.Setup(m => m.IsInRoleAsync(admin, IdentitySeeder.AdminRoleName)).ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.GetUsersInRoleAsync(IdentitySeeder.AdminRoleName))
            .ReturnsAsync(new List<ApplicationUser> { admin });

        var service = CreateService();
        var result = await service.DeleteUserAsync("admin-1", "another-user");

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(UserDeletionFailureReason.LastAdminRemaining);
        _userManagerMock.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_TargetIsAdminButNotTheLastOne_DeletesSuccessfully()
    {
        var admin1 = new ApplicationUser { Id = "admin-1", Email = "admin1@example.com", UserName = "admin1@example.com" };
        var admin2 = new ApplicationUser { Id = "admin-2", Email = "admin2@example.com", UserName = "admin2@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("admin-1")).ReturnsAsync(admin1);
        _userManagerMock.Setup(m => m.IsInRoleAsync(admin1, IdentitySeeder.AdminRoleName)).ReturnsAsync(true);
        _userManagerMock
            .Setup(m => m.GetUsersInRoleAsync(IdentitySeeder.AdminRoleName))
            .ReturnsAsync(new List<ApplicationUser> { admin1, admin2 });
        _userManagerMock.Setup(m => m.DeleteAsync(admin1)).ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        var result = await service.DeleteUserAsync("admin-1", "another-user");

        result.Succeeded.Should().BeTrue();
        _userManagerMock.Verify(m => m.DeleteAsync(admin1), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_TargetIsNonAdminUser_DeletesSuccessfully()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "user@example.com", UserName = "user@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsInRoleAsync(user, IdentitySeeder.AdminRoleName)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var service = CreateService();
        var result = await service.DeleteUserAsync("user-1", "another-user");

        result.Succeeded.Should().BeTrue();
        _userManagerMock.Verify(
            m => m.GetUsersInRoleAsync(It.IsAny<string>()), Times.Never,
            "a non-admin target never needs the last-admin count at all");
        _userManagerMock.Verify(m => m.DeleteAsync(user), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_UnknownUser_ThrowsInvalidOperationException()
    {
        _userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var service = CreateService();

        var act = () => service.DeleteUserAsync("missing", "another-user");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeleteAsyncFails_ReturnsFailureWithErrors()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "user@example.com", UserName = "user@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsInRoleAsync(user, IdentitySeeder.AdminRoleName)).ReturnsAsync(false);
        _userManagerMock
            .Setup(m => m.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));

        var service = CreateService();
        var result = await service.DeleteUserAsync("user-1", "another-user");

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(UserDeletionFailureReason.None);
        result.Errors.Should().ContainSingle().Which.Should().Be("Concurrency failure.");
    }

    [Fact]
    public async Task GetAdminUserIdsAsync_ReturnsIdsOfEveryAdminUser()
    {
        var admin1 = new ApplicationUser { Id = "admin-1", Email = "admin1@example.com", UserName = "admin1@example.com" };
        var admin2 = new ApplicationUser { Id = "admin-2", Email = "admin2@example.com", UserName = "admin2@example.com" };
        _userManagerMock
            .Setup(m => m.GetUsersInRoleAsync(IdentitySeeder.AdminRoleName))
            .ReturnsAsync(new List<ApplicationUser> { admin1, admin2 });

        var service = CreateService();
        var result = await service.GetAdminUserIdsAsync();

        result.Should().BeEquivalentTo(["admin-1", "admin-2"]);
    }
}
