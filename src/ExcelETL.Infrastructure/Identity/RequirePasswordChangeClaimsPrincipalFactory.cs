using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ExcelETL.Infrastructure.Identity;

// Lot 045 (45.0/45.4): exposes RequirePasswordChangeOnFirstLogin (Lot 044) as a claim on sign-in so
// the guard placed high in the BlazorAdmin render tree (PasswordChangeGuard) can check it from the
// cascading AuthenticationState without a database read on every navigation. The claim is
// regenerated (and dropped, once the flag is lifted) every time SignInManager builds a new
// principal -- at login and at SignInManager.RefreshSignInAsync after a successful password change.
public sealed class RequirePasswordChangeClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    public const string ClaimType = "RequirePasswordChangeOnFirstLogin";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.RequirePasswordChangeOnFirstLogin)
        {
            identity.AddClaim(new Claim(ClaimType, bool.TrueString));
        }

        return identity;
    }
}
