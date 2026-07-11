using ExcelETL.Infrastructure.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace ExcelETL.Infrastructure.Identity;

// ASP.NET Core Identity's built-in IdentityErrorDescriber returns hardcoded English Description
// text. This override resolves each Description via InfrastructureMessages instead, reusing the
// Code the base class already assigns to each error (e.g. "PasswordTooShort") as the resource
// key -- no separate error-code enum needed here, unlike Domain/Application, since Identity
// already gives us one for free.
public class LocalizedIdentityErrorDescriber(IStringLocalizer<InfrastructureMessages> localizer)
    : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Localize(nameof(DefaultError));

    public override IdentityError ConcurrencyFailure() => Localize(nameof(ConcurrencyFailure));

    public override IdentityError PasswordMismatch() => Localize(nameof(PasswordMismatch));

    public override IdentityError InvalidToken() => Localize(nameof(InvalidToken));

    public override IdentityError RecoveryCodeRedemptionFailed() => Localize(nameof(RecoveryCodeRedemptionFailed));

    public override IdentityError LoginAlreadyAssociated() => Localize(nameof(LoginAlreadyAssociated));

    public override IdentityError InvalidUserName(string? userName) => Localize(nameof(InvalidUserName), userName);

    public override IdentityError InvalidEmail(string? email) => Localize(nameof(InvalidEmail), email);

    public override IdentityError DuplicateUserName(string userName) => Localize(nameof(DuplicateUserName), userName);

    public override IdentityError DuplicateEmail(string email) => Localize(nameof(DuplicateEmail), email);

    public override IdentityError InvalidRoleName(string? role) => Localize(nameof(InvalidRoleName), role);

    public override IdentityError DuplicateRoleName(string role) => Localize(nameof(DuplicateRoleName), role);

    public override IdentityError UserAlreadyHasPassword() => Localize(nameof(UserAlreadyHasPassword));

    public override IdentityError UserLockoutNotEnabled() => Localize(nameof(UserLockoutNotEnabled));

    public override IdentityError UserAlreadyInRole(string role) => Localize(nameof(UserAlreadyInRole), role);

    public override IdentityError UserNotInRole(string role) => Localize(nameof(UserNotInRole), role);

    public override IdentityError PasswordTooShort(int length) => Localize(nameof(PasswordTooShort), length);

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Localize(nameof(PasswordRequiresUniqueChars), uniqueChars);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Localize(nameof(PasswordRequiresNonAlphanumeric));

    public override IdentityError PasswordRequiresDigit() => Localize(nameof(PasswordRequiresDigit));

    public override IdentityError PasswordRequiresLower() => Localize(nameof(PasswordRequiresLower));

    public override IdentityError PasswordRequiresUpper() => Localize(nameof(PasswordRequiresUpper));

    private IdentityError Localize(string code, params object?[] args) =>
        new() { Code = code, Description = localizer[code, (object[])args] };
}
