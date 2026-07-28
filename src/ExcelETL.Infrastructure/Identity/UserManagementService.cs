using ExcelETL.Application.Identity;
using ExcelETL.Infrastructure.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace ExcelETL.Infrastructure.Identity;

// Lot 044 (44.1). Uses UserManager.GeneratePasswordResetTokenAsync + ResetPasswordAsync(token) for
// the reset path -- confirmed at 44.0 there is no existing RemovePassword/AddPassword usage
// anywhere in this codebase to mirror, and the token-based overload is the standard ASP.NET Core
// Identity mechanism for an admin resetting someone else's password without knowing the old one.
public class UserManagementService(
    UserManager<ApplicationUser> userManager, IStringLocalizer<InfrastructureMessages> localizer) : IUserManagementService
{
    public async Task<UserCreationResult> CreateUserAsync(
        string userName, string email, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        // Lot 050 (50.2): the empty/whitespace case is rejected before any call to CreateAsync --
        // Identity's own default UserValidator would also reject it, but this guard guarantees no
        // account is ever attempted with a blank connection identifier.
        if (string.IsNullOrWhiteSpace(userName))
        {
            return UserCreationResult.Failed([localizer["UserNameRequired"]]);
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var user = new ApplicationUser
        {
            UserName = userName,
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

    public async Task<IdentityOperationResult> UpdateUserAsync(
        string userId, string userName, string email, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User '{userId}' was not found.");

        var previousUserName = user.UserName;

        user.UserName = userName;
        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;

        // A single UpdateAsync call persists all four fields together -- Identity's own
        // ValidateUserAsync (default UserValidator + ApplicationUserValidator, 50.1, + the
        // RequireUniqueEmail/AllowedUserNameCharacters options) runs first and, on any failure,
        // Store.UpdateAsync is never reached: no field is written (D3/D4's atomicity requirement).
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return IdentityOperationResult.Failed(updateResult.Errors.Select(e => e.Description).ToArray());
        }

        // D4: compared case-insensitively, matching Identity's own normalization (a plain
        // upper-invariant transform) -- a rename that only changes casing is treated as unchanged
        // and never rotates the security stamp.
        if (!string.Equals(previousUserName, userName, StringComparison.OrdinalIgnoreCase))
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        return IdentityOperationResult.Success;
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
