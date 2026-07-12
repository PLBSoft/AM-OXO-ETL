namespace ExcelETL.Application.Identity;

// A plain, framework-free result shape for Identity operations. ASP.NET Core Identity's own
// IdentityResult/IdentityError are Infrastructure-layer (Microsoft.AspNetCore.Identity) types --
// keeping them out of this Application-layer interface avoids leaking that dependency upward.
// Errors carry pre-localized text: LocalizedIdentityErrorDescriber already resolves each
// IdentityError.Description via IStringLocalizer before UserRepository maps it to this type.
public sealed record IdentityOperationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static IdentityOperationResult Success { get; } = new(true, []);

    public static IdentityOperationResult Failed(IReadOnlyList<string> errors) => new(false, errors);
}
