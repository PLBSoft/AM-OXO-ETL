using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Resources;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Lot 050 (50.1). D1 (UserName 3-30) and D2 (FirstName/LastName 2-50, no character-set
// restriction) are exercised directly against ApplicationUserValidator -- no need to go through
// the UI. The manager parameter of ValidateAsync is never used by this validator, so it's safe to
// pass null! throughout.
public class ApplicationUserValidatorTests
{
    private static ApplicationUserValidator CreateValidator() => new(new RealResxStringLocalizer<InfrastructureMessages>());

    private static ApplicationUser CreateUser(string userName = "SLB", string firstName = "Simon", string lastName = "Lebecq") =>
        new() { UserName = userName, FirstName = firstName, LastName = lastName };

    [Fact]
    public async Task ValidateAsync_NominalValues_Succeeds()
    {
        var validator = CreateValidator();

        var result = await validator.ValidateAsync(null!, CreateUser());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task ValidateAsync_UserNameLength_ExactBoundaries(int length, bool expectedSucceeded)
    {
        var validator = CreateValidator();
        var user = CreateUser(userName: new string('a', length));

        var result = await validator.ValidateAsync(null!, user);

        result.Succeeded.Should().Be(expectedSucceeded);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task ValidateAsync_FirstNameLength_ExactBoundaries(int length, bool expectedSucceeded)
    {
        var validator = CreateValidator();
        var user = CreateUser(firstName: new string('a', length));

        var result = await validator.ValidateAsync(null!, user);

        result.Succeeded.Should().Be(expectedSucceeded);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public async Task ValidateAsync_LastNameLength_ExactBoundaries(int length, bool expectedSucceeded)
    {
        var validator = CreateValidator();
        var user = CreateUser(lastName: new string('a', length));

        var result = await validator.ValidateAsync(null!, user);

        result.Succeeded.Should().Be(expectedSucceeded);
    }

    // D2: no character-set restriction on FirstName/LastName -- must not be extended by symmetry
    // from D1's username character set, which would reject 2 of the 3 real seeded accounts.
    [Theory]
    [InlineData("Le Becq")]
    [InlineData("Jean-Marie")]
    [InlineData("O'Brien")]
    [InlineData("N'Diaye")]
    public async Task ValidateAsync_NamesWithSpaceHyphenOrApostrophe_AreAccepted(string name)
    {
        var validator = CreateValidator();
        var user = CreateUser(firstName: name, lastName: name);

        var result = await validator.ValidateAsync(null!, user);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_MultipleInvalidFields_ReturnsAllErrors_NotJustTheFirst()
    {
        var validator = CreateValidator();
        var user = CreateUser(userName: "ab", firstName: "A", lastName: "B");

        var result = await validator.ValidateAsync(null!, user);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidateAsync_SeedUserValues_SatisfyTheValidator()
    {
        var validator = CreateValidator();

        foreach (var seedUser in RealSeedUsersLoader.LoadRealSeedUsers())
        {
            var user = new ApplicationUser
            {
                UserName = seedUser.UserName,
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
            };

            var result = await validator.ValidateAsync(null!, user);

            result.Succeeded.Should().BeTrue(
                $"seed user '{seedUser.UserName}' must satisfy the validator or the application " +
                "becomes inaccessible to its own administrator on a fresh database (see risque " +
                "principal, lot 050)");
        }
    }

}
