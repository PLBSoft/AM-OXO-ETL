using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Identity;

public class ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Lot 050 (50.5, D5 schema half + D2 length filet de securite). Configured after
    // base.OnModelCreating so it wins over Identity's own default (non-unique, non-filtered)
    // EmailIndex mapping.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            // SQL Server treats multiple NULLs as equal under a plain unique index -- the filter
            // is not optional, or two accounts with no email would violate the index.
            user.HasIndex(u => u.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("EmailIndex")
                .HasFilter("[NormalizedEmail] IS NOT NULL");

            // Schema-level safety net only -- the visible, localized 2-50 rule lives in
            // ApplicationUserValidator (50.1), the only one the administrator ever sees.
            user.Property(u => u.FirstName).HasMaxLength(50);
            user.Property(u => u.LastName).HasMaxLength(50);
        });
    }
}
