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

public class HistoryTests : BunitContext
{
    private readonly IDbContextFactory<ExcelEtlDbContext> _dbContextFactory =
        new TestDbContextFactory("HistoryTests_" + Guid.NewGuid());

    public HistoryTests()
    {
        Services.AddSingleton(_dbContextFactory);
        Services.AddSingleton<IExtractionHistoryRepository, ExtractionHistoryRepository>();
    }

    private async Task SeedHistoryAsync(ExtractionHistory history)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.ExtractionHistories.Add(history);
        await context.SaveChangesAsync();
    }

    [Fact]
    public void History_WithNoEntries_DisplaysEmptyMessage()
    {
        var cut = Render<History>();

        cut.Markup.Should().Contain("No extraction jobs have run yet.");
    }

    [Fact]
    public async Task History_WithCompletedEntry_DisplaysDownloadLink()
    {
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");
        history.MarkCompleted(@"C:\archive\invoice-processed.xlsx");
        await SeedHistoryAsync(history);

        var cut = Render<History>();

        cut.Markup.Should().Contain("invoice.xlsx");
        cut.Markup.Should().Contain($"/history/{history.Id}/download");
    }

    [Fact]
    public async Task History_WithFailedEntry_DoesNotDisplayDownloadLink()
    {
        var history = new ExtractionHistory(DateTimeOffset.UtcNow, "invoice.xlsx");
        history.MarkFailed();
        await SeedHistoryAsync(history);

        var cut = Render<History>();

        cut.Markup.Should().Contain("invoice.xlsx");
        cut.Markup.Should().NotContain($"/history/{history.Id}/download");
    }

    [Fact]
    public async Task History_OrdersEntriesByJobTimestampDescending()
    {
        var older = new ExtractionHistory(DateTimeOffset.UtcNow.AddHours(-1), "older.xlsx");
        var newer = new ExtractionHistory(DateTimeOffset.UtcNow, "newer.xlsx");
        await SeedHistoryAsync(older);
        await SeedHistoryAsync(newer);

        var cut = Render<History>();

        var indexOfNewer = cut.Markup.IndexOf("newer.xlsx", StringComparison.Ordinal);
        var indexOfOlder = cut.Markup.IndexOf("older.xlsx", StringComparison.Ordinal);
        indexOfNewer.Should().BeLessThan(indexOfOlder);
    }
}
