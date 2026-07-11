using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Infrastructure.Identity;

// Bootstraps the fixed set of administrator accounts this on-premise deployment relies on for
// its first login, on every startup. User identity (name/email) comes from ordinary
// configuration (AdminSeedUsers); passwords are deliberately read from a separate
// AdminSeedPasswords key so they can be supplied via User Secrets locally or environment
// variables in production and never committed to source control. A user missing its password
// entry is skipped (with a warning) rather than failing startup, so a misconfigured secret on
// one environment doesn't take the whole app down.
public class IdentitySeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    public const string AdminRoleName = "Admin";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAdminRoleExistsAsync();

        var seedUsers = configuration.GetSection("AdminSeedUsers").Get<List<AdminSeedUser>>() ?? [];

        foreach (var seedUser in seedUsers)
        {
            await SeedUserAsync(seedUser);
        }
    }

    private async Task EnsureAdminRoleExistsAsync()
    {
        if (await roleManager.RoleExistsAsync(AdminRoleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(AdminRoleName));
        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to create the {RoleName} role: {Errors}",
                AdminRoleName, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedUserAsync(AdminSeedUser seedUser)
    {
        var user = await userManager.FindByNameAsync(seedUser.UserName);

        if (user is null)
        {
            var password = configuration[$"AdminSeedPasswords:{seedUser.UserName}"];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "Skipping admin seed user {UserName}: no password configured at " +
                    "AdminSeedPasswords:{UserName} (User Secrets locally, an environment " +
                    "variable in production).",
                    seedUser.UserName, seedUser.UserName);
                return;
            }

            var newUser = new ApplicationUser
            {
                UserName = seedUser.UserName,
                Email = seedUser.Email,
                EmailConfirmed = true,
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
            };

            var createResult = await userManager.CreateAsync(newUser, password);
            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Failed to create admin seed user {UserName}: {Errors}",
                    seedUser.UserName, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Created admin seed user {UserName}", seedUser.UserName);
            user = newUser;
        }

        if (await userManager.IsInRoleAsync(user, AdminRoleName))
        {
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, AdminRoleName);
        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "Failed to add admin seed user {UserName} to the {RoleName} role: {Errors}",
                seedUser.UserName, AdminRoleName, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
