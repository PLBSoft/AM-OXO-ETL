using Microsoft.AspNetCore.Identity;

namespace ExcelETL.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    // Lot 044: forces the user to change a server-generated temporary password (creation or
    // admin-driven reset) at next login. Defaults to false so the 3 pre-existing seeded accounts
    // are unaffected.
    public bool RequirePasswordChangeOnFirstLogin { get; set; }
}
