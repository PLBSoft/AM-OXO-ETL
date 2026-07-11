using System.Globalization;
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
        Services.AddLocalization();
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
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
    public void Dashboard_WithNoHistoryOrLogs_DisplaysZeroesAndEmptyMessages() => WithCulture("en-US", () =>
    {
        var cut = Render<Dashboard>();

        cut.Find("#stat-total-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-completed-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-failed-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-average-duration").TextContent.Should().Be("N/A");
        cut.Markup.Should().Contain("No log entries recorded yet.");
    });

    [Fact]
    public void Dashboard_WithFrenchCulture_DisplaysFrenchLabels() => WithCulture("fr-FR", () =>
    {
        var cut = Render<Dashboard>();

        cut.Markup.Should().Contain("Tableau de bord");
        cut.Markup.Should().Contain("Total des travaux");
        cut.Markup.Should().Contain("Terminés");
        cut.Markup.Should().Contain("Échoués");
        cut.Markup.Should().Contain("Durée moyenne");
        cut.Find("#stat-average-duration").TextContent.Should().Be("N/D");
        cut.Markup.Should().Contain("Journaux récents");
        cut.Markup.Should().Contain("Aucune entrée de journal enregistrée pour l'instant.");
    });

    [Fact]
    public async Task Dashboard_DisplaysJobCountsByStatus() => await WithCultureAsync("en-US", async () =>
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
    });

    [Fact]
    public async Task Dashboard_DisplaysAverageDurationForCompletedJobs() => await WithCultureAsync("en-US", async () =>
    {
        var completed = new ExtractionHistory(DateTimeOffset.UtcNow.AddMinutes(-10), "completed.xlsx");
        completed.MarkCompleted(@"C:\archive\completed.xlsx");
        await SeedHistoryAsync(completed);

        var cut = Render<Dashboard>();

        cut.Find("#stat-average-duration").TextContent.Should().Be(completed.Duration!.Value.ToString(@"hh\:mm\:ss"));
    });

    [Fact]
    public async Task Dashboard_DisplaysRecentLogEntriesNewestFirst() => await WithCultureAsync("en-US", async () =>
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
    });

    private static async Task WithCultureAsync(string cultureName, Func<Task> action)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        try
        {
            await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
