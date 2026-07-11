using ExcelETL.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.BlazorAdmin.Tests;

internal sealed class TestSystemLogsDbContextFactory(string databaseName) : IDbContextFactory<SystemLogsDbContext>
{
    public SystemLogsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SystemLogsDbContext>().UseInMemoryDatabase(databaseName).Options);
}
