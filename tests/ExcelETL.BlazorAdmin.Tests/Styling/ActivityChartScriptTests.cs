using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 089 (089.2): wwwroot/js/activityChart.js draws the home page's activity chart with Chart.js.
// bUnit has no browser, so the script is checked as plain text (same convention as
// ThemeToggleScriptTests); what it really draws is a manual check (ticket 089, §5).
public class ActivityChartScriptTests
{
    private static string RepoRoot { get; } = FindRepoRoot();

    private static string Script { get; } =
        File.ReadAllText(Path.Combine(RepoRoot, "src", "ExcelETL.BlazorAdmin", "wwwroot", "js", "activityChart.js"));

    private static string AppRazor { get; } =
        File.ReadAllText(Path.Combine(RepoRoot, "src", "ExcelETL.BlazorAdmin", "Components", "App.razor"));

    [Fact]
    public void ExposesRenderAndDispose_OnAProjectGlobal()
    {
        Script.Should().Contain("window.amOxoActivityChart");
        Script.Should().Contain("render: render");
        Script.Should().Contain("dispose: dispose");
    }

    [Fact]
    public void ReadsEachStatusColourFromTheThemeTokens()
    {
        Script.Should().Contain("\"--m3-success\"");
        Script.Should().Contain("\"--m3-warning\"");
        Script.Should().Contain("\"--m3-danger\"");
    }

    [Fact]
    public void ContainsNoHardCodedColour()
    {
        Regex.IsMatch(Script, @"#[0-9a-fA-F]{3,8}\b").Should().BeFalse();
        Regex.IsMatch(Script, @"rgba?\(").Should().BeFalse();
    }

    [Fact]
    public void ContainsNoUserFacingText_EverythingComesTranslatedFromTheModel()
    {
        Script.Should().Contain("model.tooltipTitles");
        Script.Should().Contain("model.totalLabels");
        Script.Should().NotContain("fichier");
        Script.Should().NotContain("Succès");
    }

    [Fact]
    public void FollowsTheCardWidth_WithTheHeightSetByCss()
    {
        Script.Should().Contain("responsive: true");
        Script.Should().Contain("maintainAspectRatio: false");
    }

    [Fact]
    public void StacksTheBars_ShowsWholeNumbers_AndOneTooltipPerDay()
    {
        Script.Should().Contain("stacked: true");
        Script.Should().Contain("precision: 0");
        Script.Should().Contain("mode: \"index\"");
    }

    [Fact]
    public void TurnsAnimationOff_WhenTheSystemAsksForReducedMotion()
    {
        Script.Should().Contain("prefers-reduced-motion: reduce");
    }

    [Fact]
    public void RedrawsExistingCharts_WhenTheLightOrDarkThemeChanges()
    {
        Script.Should().Contain("MutationObserver");
        Script.Should().Contain("\"data-bs-theme\"");
    }

    [Fact]
    public void ReplacesAnExistingChartOnTheSameCanvas_InsteadOfStackingTwo()
    {
        Script.Should().Contain("Chart.getChart(canvas)");
        Script.Should().Contain(".destroy()");
    }

    [Fact]
    public void AppRazor_LoadsTheScriptThroughAssets_AfterChartJs_AndBeforeBlazor()
    {
        var script = AppRazor.IndexOf("<script src=\"@Assets[\"js/activityChart.js\"]\"></script>", StringComparison.Ordinal);

        script.Should().BeGreaterThanOrEqualTo(0);
        script.Should().BeGreaterThan(AppRazor.IndexOf("lib/chartjs/chart.umd.min.js", StringComparison.Ordinal));
        script.Should().BeLessThan(AppRazor.IndexOf("_framework/blazor.web.js", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (ExcelETL.slnx).");
    }
}
