using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExcelETL.Infrastructure.Identity;

public class ApplicationIdentityDbContextFactory : IDesignTimeDbContextFactory<ApplicationIdentityDbContext>
{
    public ApplicationIdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationIdentityDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=ExcelEtl;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity"));

        return new ApplicationIdentityDbContext(optionsBuilder.Options);
    }
}
