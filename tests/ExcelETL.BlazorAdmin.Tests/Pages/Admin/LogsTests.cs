using System.Globalization;
using Bunit;
using ExcelETL.Application.Diagnostics;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Infrastructure.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class LogsTests : BunitContext
{
    private readonly IDbContextFactory<SystemLogsDbContext> _systemLogsDbContextFactory =
        new TestSystemLogsDbContextFactory("LogsTests_SystemLogs_" + Guid.NewGuid());

    public LogsTests()
    {
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

    private async Task SeedLogAsync(SystemLogEntry entry)
    {
        await using var context = _systemLogsDbContextFactory.CreateDbContext();
        context.SystemLogs.Add(entry);
        await context.SaveChangesAsync();
    }

    [Fact]
    public void Logs_WithNoLogs_DisplaysEmptyMessage() => WithCulture("en-US", () =>
    {
        var cut = Render<Logs>();

        cut.Markup.Should().Contain("No log entries recorded yet.");
    });

    [Fact]
    public void Logs_WithFrenchCulture_DisplaysFrenchLabels() => WithCulture("fr-FR", () =>
    {
        var cut = Render<Logs>();

        cut.Markup.Should().Contain("Journaux récents");
        cut.Markup.Should().Contain("Aucune entrée de journal enregistrée pour l'instant.");
    });

    [Fact]
    public async Task Logs_DisplaysRecentLogEntriesNewestFirst() => await WithCultureAsync("en-US", async () =>
    {
        await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow.AddMinutes(-1), "Information", "Older entry", null));
        await SeedLogAsync(new SystemLogEntry(2, DateTime.UtcNow, "Error", "Newer entry", "System.Exception: boom"));

        var cut = Render<Logs>();

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
