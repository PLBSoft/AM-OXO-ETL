using ExcelETL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.BlazorAdmin.Tests;

internal sealed class TestDbContextFactory(string databaseName) : IDbContextFactory<ExcelEtlDbContext>
{
    public ExcelEtlDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ExcelEtlDbContext>().UseInMemoryDatabase(databaseName).Options);
}
