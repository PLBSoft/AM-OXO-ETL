using ExcelETL.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Tests.Identity;

internal sealed class TestApplicationIdentityDbContextFactory(string databaseName)
    : IDbContextFactory<ApplicationIdentityDbContext>
{
    public ApplicationIdentityDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationIdentityDbContext>().UseInMemoryDatabase(databaseName).Options);
}
