using ExcelETL.Application.Diagnostics;
using ExcelETL.Infrastructure.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcelETL.Infrastructure.Tests.Diagnostics;

public class SystemLogRepositoryTests
{
    private readonly IDbContextFactory<SystemLogsDbContext> _dbContextFactory =
        new TestSystemLogsDbContextFactory("SystemLogRepositoryTests_" + Guid.NewGuid());

    private SystemLogRepository CreateRepository() => new(_dbContextFactory);

    private async Task SeedAsync(params SystemLogEntry[] entries)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.SystemLogs.AddRange(entries);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRecentAsync_WithNoEntries_ReturnsEmptyList()
    {
        var repository = CreateRepository();

        var result = await repository.GetRecentAsync(10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsEntriesOrderedByTimestampDescending()
    {
        var older = new SystemLogEntry(1, DateTime.UtcNow.AddMinutes(-5), "Information", "Older", null);
        var newer = new SystemLogEntry(2, DateTime.UtcNow, "Information", "Newer", null);
        await SeedAsync(older, newer);

        var repository = CreateRepository();
        var result = await repository.GetRecentAsync(10);

        result.Select(e => e.Message).Should().Equal("Newer", "Older");
    }

    [Fact]
    public async Task GetRecentAsync_LimitsToRequestedCount()
    {
        var entries = Enumerable.Range(1, 5)
            .Select(i => new SystemLogEntry(i, DateTime.UtcNow.AddMinutes(-i), "Information", $"Entry {i}", null))
            .ToArray();
        await SeedAsync(entries);

        var repository = CreateRepository();
        var result = await repository.GetRecentAsync(2);

        result.Should().HaveCount(2);
        result.Select(e => e.Message).Should().Equal("Entry 1", "Entry 2");
    }

    [Fact]
    public async Task GetRecentAsync_IncludesExceptionWhenPresent()
    {
        await SeedAsync(new SystemLogEntry(
            1, DateTime.UtcNow, "Error", "Extraction failed", "System.InvalidOperationException: corrupt file"));

        var repository = CreateRepository();
        var result = await repository.GetRecentAsync(10);

        result.Single().Exception.Should().Be("System.InvalidOperationException: corrupt file");
    }
}
