using ExcelETL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Tests.Seeding;

internal sealed class TestExcelEtlDbContextFactory(string databaseName) : IDbContextFactory<ExcelEtlDbContext>
{
    public ExcelEtlDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ExcelEtlDbContext>().UseInMemoryDatabase(databaseName).Options);
}
