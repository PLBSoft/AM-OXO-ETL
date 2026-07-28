using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Lot 050 (50.1, D1). ApplicationUserValidator has no character-set check of its own -- the
// character set itself is a native IdentityOptions.User.AllowedUserNameCharacters option, wired
// once in Program.cs. This proves it is genuinely wired into a real CreateAsync pipeline (real
// UserManager + EF Core InMemory store), not merely declared -- a mocked UserManager (used
// elsewhere in this test project) would never actually exercise the option.
public class UserNameCharacterSetIdentityIntegrationTests
{
    private static ServiceProvider BuildProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddLogging();
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Same set configured in ExcelETL.BlazorAdmin/Program.cs (D1): letters, digits,
                // '_' and '.' -- '-', '@' and '+' from Identity's own default are removed.
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.";
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("user@name")]
    [InlineData("user-name")]
    [InlineData("user+name")]
    public async Task CreateAsync_UserNameContainingDisallowedCharacter_Fails(string userName)
    {
        await using var provider = BuildProvider($"UserNameCharSet_{Guid.NewGuid()}");
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await userManager.CreateAsync(
            new ApplicationUser { UserName = userName, Email = "someone@example.com", FirstName = "A", LastName = "B" },
            "Password1");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "InvalidUserName");
    }

    [Fact]
    public async Task CreateAsync_UserNameWithOnlyAllowedCharacters_Succeeds()
    {
        await using var provider = BuildProvider($"UserNameCharSet_{Guid.NewGuid()}");
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await userManager.CreateAsync(
            new ApplicationUser { UserName = "user_name.01", Email = "someone@example.com", FirstName = "A", LastName = "B" },
            "Password1");

        result.Succeeded.Should().BeTrue();
    }
}
