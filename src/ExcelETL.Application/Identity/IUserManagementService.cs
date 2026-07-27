namespace ExcelETL.Application.Identity;

// Lot 044: the create/reset-password/delete surface of user administration. Deliberately does not
// duplicate IUserRepository.UpdateProfileAsync (firstName/lastName/email edit, no role/password
// change) -- Users.razor's "modify" action reuses that existing method as-is (confirmed at 44.0
// there is no equivalent already covering create/reset/delete).
public interface IUserManagementService
{
    Task<UserCreationResult> CreateUserAsync(
        string email, string firstName, string lastName, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserDeletionResult> DeleteUserAsync(string userId, string currentUserId, CancellationToken cancellationToken = default);

    // Lets the UI pre-compute the "last remaining Admin" guard client-side (to disable the delete
    // button proactively, per the ticket's own explicit efficiency requirement) without needing to
    // attempt -- and have refused -- a real deletion first.
    Task<IReadOnlyList<string>> GetAdminUserIdsAsync(CancellationToken cancellationToken = default);
}
