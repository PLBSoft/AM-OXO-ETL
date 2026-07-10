using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExcelETL.Infrastructure.Persistence;

public class ExcelEtlDbContextFactory : IDesignTimeDbContextFactory<ExcelEtlDbContext>
{
    public ExcelEtlDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ExcelEtlDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=ExcelEtl;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_ExcelEtl"));

        return new ExcelEtlDbContext(optionsBuilder.Options);
    }
}
