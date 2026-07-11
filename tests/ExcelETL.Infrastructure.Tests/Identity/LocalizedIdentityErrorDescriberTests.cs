using ExcelETL.Infrastructure.Identity;
using ExcelETL.Infrastructure.Resources;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

public class LocalizedIdentityErrorDescriberTests
{
    public static IEnumerable<object[]> DescriberCases()
    {
        yield return Case(d => d.DefaultError(), "DefaultError");
        yield return Case(d => d.ConcurrencyFailure(), "ConcurrencyFailure");
        yield return Case(d => d.PasswordMismatch(), "PasswordMismatch");
        yield return Case(d => d.InvalidToken(), "InvalidToken");
        yield return Case(d => d.RecoveryCodeRedemptionFailed(), "RecoveryCodeRedemptionFailed");
        yield return Case(d => d.LoginAlreadyAssociated(), "LoginAlreadyAssociated");
        yield return Case(d => d.InvalidUserName("bad name"), "InvalidUserName", "bad name");
        yield return Case(d => d.InvalidUserName(null), "InvalidUserName", (object?)null);
        yield return Case(d => d.InvalidEmail("not-an-email"), "InvalidEmail", "not-an-email");
        yield return Case(d => d.DuplicateUserName("alice"), "DuplicateUserName", "alice");
        yield return Case(d => d.DuplicateEmail("alice@example.com"), "DuplicateEmail", "alice@example.com");
        yield return Case(d => d.InvalidRoleName("bad role"), "InvalidRoleName", "bad role");
        yield return Case(d => d.DuplicateRoleName("Admin"), "DuplicateRoleName", "Admin");
        yield return Case(d => d.UserAlreadyHasPassword(), "UserAlreadyHasPassword");
        yield return Case(d => d.UserLockoutNotEnabled(), "UserLockoutNotEnabled");
        yield return Case(d => d.UserAlreadyInRole("Admin"), "UserAlreadyInRole", "Admin");
        yield return Case(d => d.UserNotInRole("Admin"), "UserNotInRole", "Admin");
        yield return Case(d => d.PasswordTooShort(8), "PasswordTooShort", 8);
        yield return Case(d => d.PasswordRequiresUniqueChars(4), "PasswordRequiresUniqueChars", 4);
        yield return Case(d => d.PasswordRequiresNonAlphanumeric(), "PasswordRequiresNonAlphanumeric");
        yield return Case(d => d.PasswordRequiresDigit(), "PasswordRequiresDigit");
        yield return Case(d => d.PasswordRequiresLower(), "PasswordRequiresLower");
        yield return Case(d => d.PasswordRequiresUpper(), "PasswordRequiresUpper");
    }

    private static object[] Case(
        Func<IdentityErrorDescriber, IdentityError> invoke, string expectedCode, params object?[] expectedArgs) =>
        [invoke, expectedCode, expectedArgs];

    [Theory]
    [MemberData(nameof(DescriberCases))]
    public void DescriberMethod_ReturnsCodeAndLocalizedDescription(
        Func<IdentityErrorDescriber, IdentityError> invoke, string expectedCode, object?[] expectedArgs)
    {
        var describer = new LocalizedIdentityErrorDescriber(CreateLocalizer());

        var error = invoke(describer);

        error.Code.Should().Be(expectedCode);
        error.Description.Should().Be(FormatExpected(expectedCode, expectedArgs));
    }

    private static string FormatExpected(string code, object?[] args) =>
        $"{code}:{string.Join(",", args.Select(a => a?.ToString() ?? "null"))}";

    private static IStringLocalizer<InfrastructureMessages> CreateLocalizer()
    {
        var mock = new Mock<IStringLocalizer<InfrastructureMessages>>();
        mock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] args) => new LocalizedString(name, FormatExpected(name, args)));
        return mock.Object;
    }
}
