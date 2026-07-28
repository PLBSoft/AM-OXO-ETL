using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Lot 050 (50.4, D5, applicative half). RequireUniqueEmail is a native IdentityOptions option --
// wanted to see it act for real (real UserManager + EF Core InMemory store), not merely declared,
// same rationale as UserNameCharacterSetIdentityIntegrationTests.
public class RequireUniqueEmailIdentityIntegrationTests
{
    private static ServiceProvider BuildProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationIdentityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddLogging();
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();

        return services.BuildServiceProvider();
    }

    private static ApplicationUser NewUser(string userName, string email) =>
        new() { UserName = userName, Email = email, FirstName = "A", LastName = "B" };

    [Fact]
    public async Task CreateAsync_SecondAccountWithAlreadyUsedEmail_Fails_OnlyOneAccountPersisted()
    {
        await using var provider = BuildProvider($"RequireUniqueEmail_{Guid.NewGuid()}");
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var first = await userManager.CreateAsync(NewUser("alice", "shared@example.com"), "Password1");
        first.Succeeded.Should().BeTrue();

        var second = await userManager.CreateAsync(NewUser("bob", "shared@example.com"), "Password1");

        second.Succeeded.Should().BeFalse();
        second.Errors.Should().Contain(e => e.Code == "DuplicateEmail");
        (await userManager.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ChangingEmailToAnotherAccountsEmail_Fails()
    {
        await using var provider = BuildProvider($"RequireUniqueEmail_{Guid.NewGuid()}");
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        await userManager.CreateAsync(NewUser("alice", "alice@example.com"), "Password1");
        var bob = NewUser("bob", "bob@example.com");
        await userManager.CreateAsync(bob, "Password1");

        bob.Email = "alice@example.com";
        var result = await userManager.UpdateAsync(bob);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DuplicateEmail");
    }

    // Non-facultatif per the ticket: the classic RequireUniqueEmail false positive (a user detected
    // as a duplicate of itself) -- its absence would make every user modification impossible.
    [Fact]
    public async Task UpdateAsync_KeepingOwnEmailUnchanged_Succeeds()
    {
        await using var provider = BuildProvider($"RequireUniqueEmail_{Guid.NewGuid()}");
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var alice = NewUser("alice", "alice@example.com");
        await userManager.CreateAsync(alice, "Password1");

        alice.FirstName = "Alicia";
        var result = await userManager.UpdateAsync(alice);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_EmailsDifferingOnlyByCase_AreTreatedAsDuplicates()
    {
        await using var provider = BuildProvider($"RequireUniqueEmail_{Guid.NewGuid()}");
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        await userManager.CreateAsync(NewUser("alice", "shared@example.com"), "Password1");
        var result = await userManager.CreateAsync(NewUser("bob", "SHARED@EXAMPLE.COM"), "Password1");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "DuplicateEmail");
    }

    // Same class of risk as 50.1's seed-conformance test: if two seeded emails collided, the seed
    // itself would partially fail at startup on a fresh database (D8).
    [Fact]
    public void SeedUsers_HaveDistinctEmails()
    {
        var emails = RealSeedUsersLoader.LoadRealSeedUsers()
            .Select(u => u.Email)
            .ToList();

        emails.Should().OnlyHaveUniqueItems();
    }
}
