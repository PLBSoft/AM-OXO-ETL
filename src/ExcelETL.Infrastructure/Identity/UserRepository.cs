using ExcelETL.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Identity;

public class UserRepository(
    IDbContextFactory<ApplicationIdentityDbContext> dbContextFactory,
    UserManager<ApplicationUser> userManager) : IUserRepository
{
    public async Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users
            .OrderBy(u => u.UserName)
            .Select(u => new UserSummary(u.Id, u.Email, u.UserName))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserProfile?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users
            .Where(u => u.Id == id)
            .Select(u => new UserProfile(u.Id, u.Email, u.UserName, u.FirstName, u.LastName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IdentityOperationResult> UpdateProfileAsync(
        string id, string firstName, string lastName, string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id)
            ?? throw new InvalidOperationException($"User '{id}' was not found.");

        user.FirstName = firstName;
        user.LastName = lastName;

        // SetEmailAsync/SetUserNameAsync reset EmailConfirmed and rotate the security stamp, so
        // they're only called when the email actually changed -- calling them unconditionally
        // would force that churn (and an extra round trip) on every plain name edit.
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                return ToResult(setEmailResult);
            }

            var setUserNameResult = await userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                return ToResult(setUserNameResult);
            }
        }

        var updateResult = await userManager.UpdateAsync(user);
        return ToResult(updateResult);
    }

    public async Task<IdentityOperationResult> ChangePasswordAsync(
        string id, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id)
            ?? throw new InvalidOperationException($"User '{id}' was not found.");

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return ToResult(result);
    }

    private static IdentityOperationResult ToResult(IdentityResult result) =>
        result.Succeeded
            ? IdentityOperationResult.Success
            : IdentityOperationResult.Failed(result.Errors.Select(e => e.Description).ToArray());
}
