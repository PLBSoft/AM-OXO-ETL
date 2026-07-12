using ExcelETL.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Identity;

public class UserRepositoryTests
{
    private readonly IDbContextFactory<ApplicationIdentityDbContext> _dbContextFactory =
        new TestApplicationIdentityDbContextFactory("UserRepositoryTests_" + Guid.NewGuid());

    private UserRepository CreateRepository() => new(_dbContextFactory);

    private async Task SeedAsync(params ApplicationUser[] users)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_WithNoUsers_ReturnsEmptyList()
    {
        var repository = CreateRepository();

        var result = await repository.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsUserSummariesForAllUsers()
    {
        await SeedAsync(
            new ApplicationUser { Id = "1", Email = "alice@example.com", UserName = "alice" },
            new ApplicationUser { Id = "2", Email = "bob@example.com", UserName = "bob" });

        var repository = CreateRepository();
        var result = await repository.GetAllAsync();

        result.Should().BeEquivalentTo(
        [
            new { Id = "1", Email = "alice@example.com", UserName = "alice" },
            new { Id = "2", Email = "bob@example.com", UserName = "bob" }
        ]);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByUserName()
    {
        await SeedAsync(
            new ApplicationUser { Id = "1", Email = "zack@example.com", UserName = "zack" },
            new ApplicationUser { Id = "2", Email = "amy@example.com", UserName = "amy" });

        var repository = CreateRepository();
        var result = await repository.GetAllAsync();

        result.Select(u => u.UserName).Should().Equal("amy", "zack");
    }
}
