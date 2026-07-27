namespace ExcelETL.Application.Identity;

// Distinct from IdentityOperationResult: on success this also carries the newly created user's Id
// and the generated temporary password, for one-time display in the UI (Lot 044, 44.1).
public sealed record UserCreationResult(bool Succeeded, string? UserId, string? TemporaryPassword, IReadOnlyList<string> Errors)
{
    public static UserCreationResult Success(string userId, string temporaryPassword) => new(true, userId, temporaryPassword, []);

    public static UserCreationResult Failed(IReadOnlyList<string> errors) => new(false, null, null, errors);
}
