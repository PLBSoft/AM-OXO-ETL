using ExcelETL.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace ExcelETL.Infrastructure.Identity;

// Lot 044 (44.1). Uses UserManager.GeneratePasswordResetTokenAsync + ResetPasswordAsync(token) for
// the reset path -- confirmed at 44.0 there is no existing RemovePassword/AddPassword usage
// anywhere in this codebase to mirror, and the token-based overload is the standard ASP.NET Core
// Identity mechanism for an admin resetting someone else's password without knowing the old one.
public class UserManagementService(UserManager<ApplicationUser> userManager) : IUserManagementService
{
    public async Task<UserCreationResult> CreateUserAsync(
        string email, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            RequirePasswordChangeOnFirstLogin = true,
        };

        var result = await userManager.CreateAsync(user, temporaryPassword);

        return result.Succeeded
            ? UserCreationResult.Success(user.Id, temporaryPassword)
            : UserCreationResult.Failed(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!resetResult.Succeeded)
        {
            return PasswordResetResult.Failed(resetResult.Errors.Select(e => e.Description).ToArray());
        }

        user.RequirePasswordChangeOnFirstLogin = true;
        await userManager.UpdateAsync(user);

        return PasswordResetResult.Success(temporaryPassword);
    }

    public async Task<UserDeletionResult> DeleteUserAsync(
        string userId, string currentUserId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(userId, currentUserId, StringComparison.Ordinal))
        {
            return UserDeletionResult.Refused(UserDeletionFailureReason.SelfDeletion);
        }

        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        if (await userManager.IsInRoleAsync(user, IdentitySeeder.AdminRoleName))
        {
            var admins = await userManager.GetUsersInRoleAsync(IdentitySeeder.AdminRoleName);
            if (admins.Count == 1)
            {
                return UserDeletionResult.Refused(UserDeletionFailureReason.LastAdminRemaining);
            }
        }

        var deleteResult = await userManager.DeleteAsync(user);

        return deleteResult.Succeeded
            ? UserDeletionResult.Success
            : UserDeletionResult.Failed(deleteResult.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<IReadOnlyList<string>> GetAdminUserIdsAsync(CancellationToken cancellationToken = default)
    {
        var admins = await userManager.GetUsersInRoleAsync(IdentitySeeder.AdminRoleName);
        return admins.Select(a => a.Id).ToArray();
    }
}
