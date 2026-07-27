namespace ExcelETL.Application.Identity;

// Same shape as UserCreationResult minus the UserId (the caller already knows which user it
// reset the password for) -- Lot 044, 44.1.
public sealed record PasswordResetResult(bool Succeeded, string? TemporaryPassword, IReadOnlyList<string> Errors)
{
    public static PasswordResetResult Success(string temporaryPassword) => new(true, temporaryPassword, []);

    public static PasswordResetResult Failed(IReadOnlyList<string> errors) => new(false, null, errors);
}
