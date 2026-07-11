using System.Globalization;
using Bunit;
using ExcelETL.Application.Extraction;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Domain.Entities;
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

    public DashboardTests()
    {
        Services.AddSingleton(_dbContextFactory);
        Services.AddSingleton<IExtractionHistoryRepository, ExtractionHistoryRepository>();
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

    [Fact]
    public void Dashboard_WithNoHistory_DisplaysZeroes() => WithCulture("en-US", () =>
    {
        var cut = Render<Dashboard>();

        cut.Find("#stat-total-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-completed-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-failed-jobs").TextContent.Should().Be("0");
        cut.Find("#stat-average-duration").TextContent.Should().Be("N/A");
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
