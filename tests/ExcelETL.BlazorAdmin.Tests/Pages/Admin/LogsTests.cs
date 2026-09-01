using System.Globalization;
using Bunit;
using ExcelETL.Application.Diagnostics;
using ExcelETL.BlazorAdmin.Components.Pages.Admin;
using ExcelETL.BlazorAdmin.Services;
using ExcelETL.BlazorAdmin.Tests;
using ExcelETL.Infrastructure.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages.Admin;

public class LogsTests : BunitContext
{
    private readonly IDbContextFactory<SystemLogsDbContext> _systemLogsDbContextFactory =
        new TestSystemLogsDbContextFactory("LogsTests_SystemLogs_" + Guid.NewGuid());
    private readonly Mock<ILocalTimeFormatter> _localTimeFormatterMock = new();

    public LogsTests()
    {
        Services.AddSingleton(_systemLogsDbContextFactory);
        Services.AddSingleton<ISystemLogRepository, SystemLogRepository>();
        Services.AddSingleton(_localTimeFormatterMock.Object);
        Services.AddLocalization();

        // Logs.razor's OnAfterRenderAsync (Lot 064) reads RendererInfo.IsInteractive unconditionally
        // -- bUnit throws MissingRendererInfoException unless a render mode is set first. Defaults to
        // non-interactive (matching real static prerendering); tests that need the JS-interop path
        // override this explicitly before rendering.
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
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

    // Lot 041 (41.4): log-copy-btn was the audit's one strict icon-only-button non-conformance --
    // aria-label was missing while title was already present (the inverse of SheetRuleForm/
    // SheetGenerationRuleForm's own gap, where aria-label was present and title was missing).
    [Fact]
    public async Task LogCopyButton_HasAriaLabelMatchingTitle() => await WithCultureAsync("en-US", async () =>
    {
        await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", "Some entry", null));

        var cut = Render<Logs>();

        var copyButton = cut.Find(".log-copy-btn");
        copyButton.GetAttribute("aria-label").Should().Be(copyButton.GetAttribute("title"));
        copyButton.GetAttribute("aria-label").Should().NotBeNullOrEmpty();
    });

    // V2: explicit decision (see Lot V doc intro) -- Logs.razor keeps its native <table>, never
    // gets the d-none/d-md-table + card-fallback treatment applied to the other list pages. Guards
    // against an accidental generalization of that responsive pattern to this page.
    [Fact]
    public async Task Logs_TableNeverGetsResponsiveCardToggleClasses() => await WithCultureAsync("en-US", async () =>
    {
        await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", "Some entry", null));

        var cut = Render<Logs>();

        var table = cut.Find("table.table");
        table.ClassList.Should().NotContain("d-none");
        table.ClassList.Should().NotContain("d-md-table");
        cut.FindAll("div.d-md-none").Should().BeEmpty();
    });

    // V5: message truncation + <details>/<summary> accordion for the full text.
    [Fact]
    public async Task Logs_LongMessage_IsTruncatedTo50CharsWithEllipsis_UntilExpanded() =>
        await WithCultureAsync("en-US", async () =>
        {
            var longMessage = "SELECT * FROM VeryLongTableName WHERE Column1 = 'value' AND Column2 = 'other value'";
            await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", longMessage, null));

            var cut = Render<Logs>();

            var expectedTruncated = longMessage[..50] + "…";
            var summary = cut.Find("#view-log-details-button-1");
            summary.TextContent.Trim().Should().Be(expectedTruncated);
            cut.Markup.Should().NotContain(longMessage);
            cut.FindAll("pre code").Should().BeEmpty();
        });

    [Fact]
    public async Task Logs_ShortMessage_IsDisplayedInFull_WithNoTruncationOrDetailsLink() =>
        await WithCultureAsync("en-US", async () =>
        {
            var shortMessage = "Short message under 50 chars";
            await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", shortMessage, null));

            var cut = Render<Logs>();

            cut.Markup.Should().Contain(shortMessage);
            cut.Markup.Should().NotContain("…");
            cut.FindAll("#view-log-details-button-1").Should().BeEmpty();
        });

    [Fact]
    public async Task Logs_ClickingViewDetails_RevealsFullMessageInPreCode() =>
        await WithCultureAsync("en-US", async () =>
        {
            var longMessage = "SELECT * FROM VeryLongTableName WHERE Column1 = 'value' AND Column2 = 'other value'";
            await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", longMessage, null));

            var cut = Render<Logs>();
            cut.FindAll("pre code").Should().BeEmpty();

            cut.Find("#view-log-details-button-1").Click();

            var codeElements = cut.FindAll("pre code");
            codeElements.Should().NotBeEmpty();
            codeElements[0].TextContent.Should().Be(longMessage);
        });

    [Fact]
    public async Task Logs_MessageWithSpecialSqlCharacters_RendersWithoutBreakingPageHtml() =>
        await WithCultureAsync("en-US", async () =>
        {
            var sqlMessage = "SELECT * FROM Foo WHERE Bar = 'it''s <a value>' AND Baz > 1 AND Qux < 2";
            await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", sqlMessage, null));

            var cut = Render<Logs>();

            cut.Find("#view-log-details-button-1").Click();

            var codeElements = cut.FindAll("pre code");
            codeElements.Should().NotBeEmpty();
            codeElements[0].TextContent.Should().Be(sqlMessage);
        });

    [Fact]
    public async Task Logs_FilterControls_HaveLargeSizeClasses() => await WithCultureAsync("en-US", async () =>
    {
        await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", "Some entry", null));

        var cut = Render<Logs>();

        cut.Find("#log-level-filter").ClassList.Should().Contain("form-select-lg");
        cut.Find("#log-time-filter").ClassList.Should().Contain("form-select-lg");
        cut.Find("#log-search").ClassList.Should().Contain("form-control-lg");
    });

    // Lot 064 (64.2): stable HTML id on the time cell, selected by id per this project's own
    // convention, never by text/position. Non-interactive render (bUnit's default, no
    // SetRendererInfo) never calls into JS interop at all -- same as static prerendering -- so the
    // cell shows the raw UTC-formatted fallback until the browser converts it, per the ticket's own
    // documented limitation.
    [Fact]
    public async Task LogTimestampCell_HasStableId_AndShowsUtcFallback_WhenNotYetInteractive() =>
        await WithCultureAsync("en-US", async () =>
        {
            var entry = new SystemLogEntry(1, new DateTime(2026, 9, 1, 20, 43, 27, DateTimeKind.Utc), "Information", "Some entry", null);
            await SeedLogAsync(entry);

            var cut = Render<Logs>();

            var cell = cut.Find("#log-timestamp-1");
            cell.TextContent.Should().Be("2026-09-01 20:43:27");
        });

    // Lot 064 (64.2): once the circuit is interactive, OnAfterRenderAsync hands every visible
    // entry's UTC timestamp to ILocalTimeFormatter (real implementation: JS interop) in one batched
    // call, and the same stable-id cell is updated with the browser-local result.
    [Fact]
    public async Task LogTimestampCell_UpdatesToTheLocalTimeFormatterResult_OnceInteractive() =>
        await WithCultureAsync("en-US", async () =>
        {
            await SeedLogAsync(new SystemLogEntry(1, DateTime.UtcNow, "Information", "Some entry", null));
            _localTimeFormatterMock
                .Setup(f => f.FormatManyAsync(It.IsAny<IReadOnlyList<DateTime>>(), "yyyy-MM-dd HH:mm:ss"))
                .ReturnsAsync((IReadOnlyList<DateTime> values, string _) =>
                    values.Select(_ => "STUBBED-LOCAL-TIME").ToList());
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));

            var cut = Render<Logs>();
            cut.WaitForState(() => cut.Find("#log-timestamp-1").TextContent == "STUBBED-LOCAL-TIME");

            _localTimeFormatterMock.Verify(
                f => f.FormatManyAsync(It.IsAny<IReadOnlyList<DateTime>>(), "yyyy-MM-dd HH:mm:ss"), Times.Once);
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
