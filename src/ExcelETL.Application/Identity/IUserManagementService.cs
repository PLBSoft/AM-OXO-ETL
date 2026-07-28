namespace ExcelETL.Application.Identity;

// Lot 044: the create/reset-password/delete surface of user administration. Deliberately does not
// duplicate IUserRepository.UpdateProfileAsync (firstName/lastName/email edit, no username/role/
// password change -- that method also backs Profile.razor's self-service editing, which stays out
// of lot 050's scope and never gains a username field) -- Users.razor's "modify" action goes
// through UpdateUserAsync below instead (lot 050, 50.3), the admin-facing counterpart that also
// covers the connection identifier (UserName).
public interface IUserManagementService
{
    // Lot 050 (50.2): UserName is now an explicit, independent parameter -- never derived from
    // email (Login.razor authenticates by user name, not by email; see D1/D5 in
    // tickets-tdd-lot-050-identite-connexion-username-unicite-email-role-visible.md).
    Task<UserCreationResult> CreateUserAsync(
        string userName, string email, string firstName, string lastName, CancellationToken cancellationToken = default);

    // Lot 050 (50.3, D3/D4): the four fields are updated in a single persistence operation -- if
    // any is rejected, none is written. UpdateSecurityStampAsync is called only when the user name
    // actually changed (compared case-insensitively, matching Identity's own normalization), so a
    // renamed account's other active sessions are invalidated at their next revalidation (D4) while
    // an unrelated field-only edit or a same-value "rename" never rotates the stamp.
    Task<IdentityOperationResult> UpdateUserAsync(
        string userId, string userName, string email, string firstName, string lastName, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserDeletionResult> DeleteUserAsync(string userId, string currentUserId, CancellationToken cancellationToken = default);

    // Lets the UI pre-compute the "last remaining Admin" guard client-side (to disable the delete
    // button proactively, per the ticket's own explicit efficiency requirement) without needing to
    // attempt -- and have refused -- a real deletion first.
    Task<IReadOnlyList<string>> GetAdminUserIdsAsync(CancellationToken cancellationToken = default);
}
