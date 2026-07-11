using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

public class IdentitySeederTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> BaseSeedUserConfig() => new()
    {
        ["AdminSeedUsers:0:UserName"] = "SLB",
        ["AdminSeedUsers:0:Email"] = "simon.lebecq@gmail.com",
        ["AdminSeedUsers:0:FirstName"] = "Simon",
        ["AdminSeedUsers:0:LastName"] = "Le Becq",
    };

    private static IdentitySeeder CreateSeeder(
        Mock<UserManager<ApplicationUser>> userManagerMock,
        Mock<RoleManager<IdentityRole>> roleManagerMock,
        IConfiguration configuration) =>
        new(userManagerMock.Object, roleManagerMock.Object, configuration, NullLogger<IdentitySeeder>.Instance);

    [Fact]
    public async Task SeedAsync_WhenAdminRoleMissing_CreatesAdminRole()
    {
        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(false);
        roleManagerMock.Setup(r => r.CreateAsync(It.IsAny<IdentityRole>())).ReturnsAsync(IdentityResult.Success);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration([]));

        await seeder.SeedAsync();

        roleManagerMock.Verify(
            r => r.CreateAsync(It.Is<IdentityRole>(role => role.Name == IdentitySeeder.AdminRoleName)), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenAdminRoleAlreadyExists_DoesNotRecreateRole()
    {
        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration([]));

        await seeder.SeedAsync();

        roleManagerMock.Verify(r => r.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WithConfiguredPassword_CreatesUserAndAssignsAdminRole()
    {
        var values = BaseSeedUserConfig();
        values["AdminSeedPasswords:SLB"] = "Test-Password-1!";

        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        userManagerMock.Setup(u => u.FindByNameAsync("SLB")).ReturnsAsync((ApplicationUser?)null);
        userManagerMock
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), "Test-Password-1!"))
            .ReturnsAsync(IdentityResult.Success);
        userManagerMock
            .Setup(u => u.IsInRoleAsync(It.IsAny<ApplicationUser>(), IdentitySeeder.AdminRoleName))
            .ReturnsAsync(false);
        userManagerMock
            .Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), IdentitySeeder.AdminRoleName))
            .ReturnsAsync(IdentityResult.Success);

        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration(values));

        await seeder.SeedAsync();

        userManagerMock.Verify(
            u => u.CreateAsync(
                It.Is<ApplicationUser>(usr =>
                    usr.UserName == "SLB" &&
                    usr.Email == "simon.lebecq@gmail.com" &&
                    usr.FirstName == "Simon" &&
                    usr.LastName == "Le Becq" &&
                    usr.EmailConfirmed),
                "Test-Password-1!"),
            Times.Once);
        userManagerMock.Verify(
            u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), IdentitySeeder.AdminRoleName), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WithoutConfiguredPassword_SkipsUserCreation()
    {
        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        userManagerMock.Setup(u => u.FindByNameAsync("SLB")).ReturnsAsync((ApplicationUser?)null);

        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration(BaseSeedUserConfig()));

        await seeder.SeedAsync();

        userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WhenUserAlreadyExistsAndInRole_DoesNotCreateOrReassign()
    {
        var values = BaseSeedUserConfig();
        values["AdminSeedPasswords:SLB"] = "Test-Password-1!";

        var existingUser = new ApplicationUser { UserName = "SLB", Email = "simon.lebecq@gmail.com" };
        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        userManagerMock.Setup(u => u.FindByNameAsync("SLB")).ReturnsAsync(existingUser);
        userManagerMock.Setup(u => u.IsInRoleAsync(existingUser, IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration(values));

        await seeder.SeedAsync();

        userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        userManagerMock.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_WhenUserExistsButNotInRole_AssignsRoleWithoutRecreatingUser()
    {
        var values = BaseSeedUserConfig();
        values["AdminSeedPasswords:SLB"] = "Test-Password-1!";

        var existingUser = new ApplicationUser { UserName = "SLB", Email = "simon.lebecq@gmail.com" };
        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        userManagerMock.Setup(u => u.FindByNameAsync("SLB")).ReturnsAsync(existingUser);
        userManagerMock.Setup(u => u.IsInRoleAsync(existingUser, IdentitySeeder.AdminRoleName)).ReturnsAsync(false);
        userManagerMock
            .Setup(u => u.AddToRoleAsync(existingUser, IdentitySeeder.AdminRoleName))
            .ReturnsAsync(IdentityResult.Success);

        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration(values));

        await seeder.SeedAsync();

        userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        userManagerMock.Verify(u => u.AddToRoleAsync(existingUser, IdentitySeeder.AdminRoleName), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WithMultipleConfiguredUsers_SeedsAll()
    {
        var values = BaseSeedUserConfig();
        values["AdminSeedUsers:1:UserName"] = "J2M";
        values["AdminSeedUsers:1:Email"] = "jean-marie.marcinkiewicz@alpha.fr";
        values["AdminSeedUsers:1:FirstName"] = "Jean-Marie";
        values["AdminSeedUsers:1:LastName"] = "Marcinkiewicz";
        values["AdminSeedPasswords:SLB"] = "Test-Password-1!";
        values["AdminSeedPasswords:J2M"] = "Test-Password-2!";

        var userManagerMock = IdentityManagerMocks.CreateUserManagerMock();
        userManagerMock.Setup(u => u.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        userManagerMock
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManagerMock
            .Setup(u => u.IsInRoleAsync(It.IsAny<ApplicationUser>(), IdentitySeeder.AdminRoleName))
            .ReturnsAsync(false);
        userManagerMock
            .Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), IdentitySeeder.AdminRoleName))
            .ReturnsAsync(IdentityResult.Success);

        var roleManagerMock = IdentityManagerMocks.CreateRoleManagerMock();
        roleManagerMock.Setup(r => r.RoleExistsAsync(IdentitySeeder.AdminRoleName)).ReturnsAsync(true);

        var seeder = CreateSeeder(userManagerMock, roleManagerMock, BuildConfiguration(values));

        await seeder.SeedAsync();

        userManagerMock.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(usr => usr.UserName == "SLB"), "Test-Password-1!"), Times.Once);
        userManagerMock.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(usr => usr.UserName == "J2M"), "Test-Password-2!"), Times.Once);
    }
}
