using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

// Lot 044 (44.2): the migration itself only adds a `bit NOT NULL DEFAULT 0` column on a real SQL
// Server -- not something the InMemory provider can verify directly (it has no migrations at
// all). What *is* verifiable here is the property's default value and persistence round-trip,
// which is what the ticket's "colonne présente, valeur par défaut correcte" requirement actually
// depends on.
public class ApplicationUserRequirePasswordChangeOnFirstLoginTests
{
    private readonly IDbContextFactory<ApplicationIdentityDbContext> _dbContextFactory =
        new TestApplicationIdentityDbContextFactory("ApplicationUserRequirePasswordChangeOnFirstLoginTests_" + Guid.NewGuid());

    [Fact]
    public async Task ExistingUser_SeededWithoutTheFlag_DefaultsToFalse()
    {
        await using (var context = _dbContextFactory.CreateDbContext())
        {
            context.Users.Add(new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice@example.com" });
            await context.SaveChangesAsync();
        }

        await using var readContext = _dbContextFactory.CreateDbContext();
        var user = await readContext.Users.SingleAsync(u => u.Id == "1");

        user.RequirePasswordChangeOnFirstLogin.Should().BeFalse();
    }

    [Fact]
    public async Task User_WithFlagSetTrue_PersistsAndReloadsAsTrue()
    {
        await using (var context = _dbContextFactory.CreateDbContext())
        {
            context.Users.Add(new ApplicationUser
            {
                Id = "1",
                Email = "alice@example.com",
                UserName = "alice@example.com",
                RequirePasswordChangeOnFirstLogin = true,
            });
            await context.SaveChangesAsync();
        }

        await using var readContext = _dbContextFactory.CreateDbContext();
        var user = await readContext.Users.SingleAsync(u => u.Id == "1");

        user.RequirePasswordChangeOnFirstLogin.Should().BeTrue();
    }
}
