using ExcelETL.Application.Identity;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

public class UserRepositoryTests
{
    private readonly IDbContextFactory<ApplicationIdentityDbContext> _dbContextFactory =
        new TestApplicationIdentityDbContextFactory("UserRepositoryTests_" + Guid.NewGuid());

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityManagerMocks.CreateUserManagerMock();

    private UserRepository CreateRepository() => new(_dbContextFactory, _userManagerMock.Object);

    private async Task SeedAsync(params ApplicationUser[] users)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_WithNoUsers_ReturnsEmptyList()
    {
        var repository = CreateRepository();

        var result = await repository.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsUserSummariesForAllUsers()
    {
        await SeedAsync(
            new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice", FirstName = "Alice", LastName = "Smith" },
            new ApplicationUser { Id = "2", Email = "bob@example.com", UserName = "bob", FirstName = "Bob", LastName = "Jones" });

        var repository = CreateRepository();
        var result = await repository.GetAllAsync();

        result.Should().BeEquivalentTo(
        [
            new { Id = "1", Email = "alice@example.com", UserName = "alice", FirstName = "Alice", LastName = "Smith" },
            new { Id = "2", Email = "bob@example.com", UserName = "bob", FirstName = "Bob", LastName = "Jones" }
        ]);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByUserName()
    {
        await SeedAsync(
            new ApplicationUser { Id = "1", Email = "zack@example.com", UserName = "zack" },
            new ApplicationUser { Id = "2", Email = "amy@example.com", UserName = "amy" });

        var repository = CreateRepository();
        var result = await repository.GetAllAsync();

        result.Select(u => u.UserName).Should().Equal("amy", "zack");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsProfile()
    {
        await SeedAsync(new ApplicationUser
        {
            Id = "1",
            Email = "alice@example.com",
            UserName = "alice@example.com",
            FirstName = "Alice",
            LastName = "Smith",
        });

        var repository = CreateRepository();
        var result = await repository.GetByIdAsync("1");

        result.Should().BeEquivalentTo(new UserProfile("1", "alice@example.com", "alice@example.com", "Alice", "Smith"));
    }

    [Fact]
    public async Task GetByIdAsync_MissingUser_ReturnsNull()
    {
        var repository = CreateRepository();

        var result = await repository.GetByIdAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileAsync_UnknownUser_ThrowsInvalidOperationException()
    {
        _userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var repository = CreateRepository();

        var act = () => repository.UpdateProfileAsync("missing", "Alice", "Smith", "alice@example.com");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateProfileAsync_ChangedEmail_UpdatesNamesEmailAndUserName_ReturnsSuccess()
    {
        var user = new ApplicationUser { Id = "1", Email = "old@example.com", UserName = "old@example.com", FirstName = "Old", LastName = "Name" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.SetEmailAsync(user, "new@example.com")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.SetUserNameAsync(user, "new@example.com")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var repository = CreateRepository();
        var result = await repository.UpdateProfileAsync("1", "Alice", "Smith", "new@example.com");

        result.Succeeded.Should().BeTrue();
        user.FirstName.Should().Be("Alice");
        user.LastName.Should().Be("Smith");
        _userManagerMock.Verify(m => m.SetEmailAsync(user, "new@example.com"), Times.Once);
        _userManagerMock.Verify(m => m.SetUserNameAsync(user, "new@example.com"), Times.Once);
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_UnchangedEmail_DoesNotCallSetEmailOrSetUserName()
    {
        var user = new ApplicationUser { Id = "1", Email = "same@example.com", UserName = "same@example.com", FirstName = "Old", LastName = "Name" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var repository = CreateRepository();
        var result = await repository.UpdateProfileAsync("1", "Alice", "Smith", "same@example.com");

        result.Succeeded.Should().BeTrue();
        _userManagerMock.Verify(m => m.SetEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _userManagerMock.Verify(m => m.SetUserNameAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenSetEmailFails_ReturnsFailureWithErrorsAndDoesNotCallUpdate()
    {
        var user = new ApplicationUser { Id = "1", Email = "old@example.com", UserName = "old@example.com", FirstName = "Old", LastName = "Name" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.SetEmailAsync(user, "taken@example.com"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email 'taken@example.com' is already taken." }));

        var repository = CreateRepository();
        var result = await repository.UpdateProfileAsync("1", "Alice", "Smith", "taken@example.com");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Email 'taken@example.com' is already taken.");
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_UnknownUser_ThrowsInvalidOperationException()
    {
        _userManagerMock.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var repository = CreateRepository();

        var act = () => repository.ChangePasswordAsync("missing", "old", "new");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidCurrentPassword_ReturnsSuccess()
    {
        var user = new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, "OldP@ss1", "NewP@ss1")).ReturnsAsync(IdentityResult.Success);

        var repository = CreateRepository();
        var result = await repository.ChangePasswordAsync("1", "OldP@ss1", "NewP@ss1");

        result.Succeeded.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsFailureWithErrors()
    {
        var user = new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice@example.com" };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.ChangePasswordAsync(user, "WrongPassword", "NewP@ss1"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));

        var repository = CreateRepository();
        var result = await repository.ChangePasswordAsync("1", "WrongPassword", "NewP@ss1");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Incorrect password.");
    }

    // Lot 045 (45.1): a successful password change lifts RequirePasswordChangeOnFirstLogin so a
    // temporary password created/reset by an admin (Lot 044) doesn't stay forced open forever.
    [Fact]
    public async Task ChangePasswordAsync_SucceedsWithFlagTrue_ClearsFlag_AndUpdatesUserOnce()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "alice@example.com",
            UserName = "alice@example.com",
            RequirePasswordChangeOnFirstLogin = true,
        };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, "Temp0rary!", "NewP@ss1")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var repository = CreateRepository();
        var result = await repository.ChangePasswordAsync("1", "Temp0rary!", "NewP@ss1");

        result.Succeeded.Should().BeTrue();
        user.RequirePasswordChangeOnFirstLogin.Should().BeFalse();
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_FailsWithFlagTrue_LeavesFlagTrue_AndNeverCallsUpdate()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "alice@example.com",
            UserName = "alice@example.com",
            RequirePasswordChangeOnFirstLogin = true,
        };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock
            .Setup(m => m.ChangePasswordAsync(user, "WrongPassword", "NewP@ss1"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));

        var repository = CreateRepository();
        var result = await repository.ChangePasswordAsync("1", "WrongPassword", "NewP@ss1");

        result.Succeeded.Should().BeFalse();
        user.RequirePasswordChangeOnFirstLogin.Should().BeTrue();
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_SucceedsWithFlagAlreadyFalse_LeavesFlagFalse_AndNeverCallsUpdate()
    {
        var user = new ApplicationUser
        {
            Id = "1",
            Email = "alice@example.com",
            UserName = "alice@example.com",
            RequirePasswordChangeOnFirstLogin = false,
        };
        _userManagerMock.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, "OldP@ss1", "NewP@ss1")).ReturnsAsync(IdentityResult.Success);

        var repository = CreateRepository();
        var result = await repository.ChangePasswordAsync("1", "OldP@ss1", "NewP@ss1");

        result.Succeeded.Should().BeTrue();
        user.RequirePasswordChangeOnFirstLogin.Should().BeFalse();
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}
