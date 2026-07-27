using System.Text.RegularExpressions;
using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

public partial class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void Generate_ReturnsTwelveCharacterPassword()
    {
        var password = TemporaryPasswordGenerator.Generate();

        password.Should().HaveLength(12);
    }

    [Fact]
    public void Generate_SatisfiesDefaultIdentityComplexityPolicy()
    {
        var password = TemporaryPasswordGenerator.Generate();

        UppercaseRegex().IsMatch(password).Should().BeTrue("it must contain at least one uppercase letter");
        LowercaseRegex().IsMatch(password).Should().BeTrue("it must contain at least one lowercase letter");
        DigitRegex().IsMatch(password).Should().BeTrue("it must contain at least one digit");
        NonAlphanumericRegex().IsMatch(password).Should().BeTrue("it must contain at least one non-alphanumeric character");
    }

    [Fact]
    public void Generate_ProducesDifferentPasswordsAcrossCalls()
    {
        var passwords = Enumerable.Range(0, 20).Select(_ => TemporaryPasswordGenerator.Generate()).ToList();

        passwords.Distinct().Should().HaveCount(passwords.Count);
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex("[a-z]")]
    private static partial Regex LowercaseRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumericRegex();
}
