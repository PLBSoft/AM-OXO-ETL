using ExcelETL.Infrastructure.Persistence;
using ExcelETL.WebAPI.HealthChecks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ExcelETL.WebAPI.Tests.HealthChecks;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithReachableDatabase_ReturnsHealthy()
    {
        var dbContextFactory = new TestDbContextFactory("DatabaseHealthCheckTests_" + Guid.NewGuid());
        var sut = new DatabaseHealthCheck(dbContextFactory);

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDbContextFactoryThrows_ReturnsUnhealthy_NotAnException()
    {
        var sut = new DatabaseHealthCheck(new ThrowingDbContextFactory());

        var act = async () => await sut.CheckHealthAsync(new HealthCheckContext());

        var result = await act.Should().NotThrowAsync();
        result.Subject.Status.Should().Be(HealthStatus.Unhealthy);
    }

    // Mirrors the real EF Core InMemory provider factory used elsewhere in this project's own
    // tests -- kept minimal/local, no shared-test-helper convention in this repo.
    private sealed class TestDbContextFactory(string databaseName) : IDbContextFactory<ExcelEtlDbContext>
    {
        public ExcelEtlDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<ExcelEtlDbContext>().UseInMemoryDatabase(databaseName).Options);

        public Task<ExcelEtlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<ExcelEtlDbContext>
    {
        public ExcelEtlDbContext CreateDbContext() => throw new InvalidOperationException("Database unreachable.");

        public Task<ExcelEtlDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Database unreachable.");
    }
}
