using Bunit;
using ExcelETL.Application.Diagnostics;
using ExcelETL.Application.Extraction;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Entities;
using ExcelETL.Infrastructure.Diagnostics;
using ExcelETL.Infrastructure.Persistence;
using ExcelETL.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class DashboardTests : BunitContext
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("DashboardTests_" + Guid.NewGuid());

    private readonly IDbContextFactory<SystemLogsDbContext> _systemLogsDbContextFactory =
        new TestSystemLogsDbContextFactory("DashboardTests_SystemLogs_" + Guid.NewGuid());

    public DashboardTests()
    {
        Services.AddSingleton(_dbContextFactory);
        Services.AddSingleton<IExtractionHistoryRepository, ExtractionHistoryRepository>();
        Services.AddSingleton(_systemLogsDbContextFactory);
        Services.AddSingleton<ISystemLogRepository, SystemLogRepository>();
    }

    private async Task SeedHistoryAsync(ExtractionHistory history)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.ExtractionHistories.Add(history);
        await context.SaveChangesAsync();
    }

    private async Task SeedLogAsync(SystemLogEntry entry)
    {
        await using var context = _systemLogsDbContextFactory.CreateDbContext();
        context.SystemLogs.Add(entry);
        await context.SaveChangesAsync();
    }

    [Fact]
    public void Dashboard_WithNoHistoryOrLogs_DisplaysZeroesAndEmptyMessages()
    {
        var cut = Render<Dashboard>();

        cut.Find("#stat-total-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-completed-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-failed-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-average-duration").TextContent.Should().Be("N/A");
        cut.Markup.Should().Contain("No log entries recorded yet.");
    }

    [Fact]
    public async Task Dashboard_DisplaysJobCountsByStatus()
    {
        var pending = new ExtractionHistory(DateTimeOffset.UtcNow, "pending.xlsx");
        await SeedHistoryAsync(pending);

        var completed = new ExtractionHistory(DateTimeOffset.UtcNow, "completed.xlsx");
        completed.MarkCompleted(@"C:\archive\completed.xlsx");
        await SeedHistoryAsync(completed);

        var failed = new ExtractionHistory(DateTimeOffset.UtcNow, "failed.xlsx");
        failed.MarkFailed();
        await SeedHistoryAsync(failed);

        var cut = Render<Dashboard>();

        cut.Find("#stat-total-jobs").TextContent.Should().Be("3");
        cut.Find("#stat-completed-jobs").TextContent.Should().Be("1");
        cut.Find("#stat-failed-jobs").TextContent.Should().Be("1");
    }

    [Fact]
    public async Task Dashboard_DisplaysAverageDurationForCompletedJobs()
    {
        var completed = new ExtractionHistory(DateTimeOffset.UtcNow.AddMinutes(-10), "completed.xlsx");
        completed.MarkCompleted(@"C:\archive\completed.xlsx");
        await SeedHistoryAsync(completed);

        var cut = Render<Dashboard>();

        cut.Find("#stat-average-duration").TextContent.Should().Be(completed.Duration!.Value.ToString(@"hh\:mm\:ss"));
    }

    [Fact]
    public async Task Dashboard_DisplaysRecentLogEntriesNewestFirst()
    {
        await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow.AddMinutes(-1), "Information", "Older entry", null));
        await SeedLogAsync(new SystemLogEntry(2, DateTime.UtcNow, "Error", "Newer entry", "System.Exception: boom"));

        var cut = Render<Dashboard>();

        cut.Markup.Should().Contain("Newer entry");
        cut.Markup.Should().Contain("Older entry");
        cut.Markup.Should().Contain("System.Exception: boom");

        var indexOfNewer = cut.Markup.IndexOf("Newer entry", StringComparison.Ordinal);
        var indexOfOlder = cut.Markup.IndexOf("Older entry", StringComparison.Ordinal);
        indexOfNewer.Should().BeLessThan(indexOfOlder);
    }
}
