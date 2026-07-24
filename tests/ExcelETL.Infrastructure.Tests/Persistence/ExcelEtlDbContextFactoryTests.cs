using ExcelETL.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Tests.Persistence;

// Lot 029: the design-time factory hardcodes its own connection string, independent of the
// two hosts' appsettings.json -- guards it from drifting back to the retired ExcelEtl name.
public class ExcelEtlDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_UsesRenamedDatabase()
    {
        var factory = new ExcelEtlDbContextFactory();

        using var context = factory.CreateDbContext([]);
        var connectionString = context.Database.GetConnectionString();

        connectionString.Should().Contain("AM-OXO-ETL-MAD-REL");
        connectionString.Should().NotContain("Database=ExcelEtl;");
    }
}
