using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 089 (089.4, D5): Chart.js draws at the width of its container and at the height CSS gives it
// (maintainAspectRatio: false in activityChart.js), so the size lives here. Colours now come from
// the theme inside the script (ActivityChartScriptTests), not from this stylesheet.
public class HomeActivityChartCssTests
{
    private static string Css { get; } = File.ReadAllText(RepoPath(
        "src", "ExcelETL.BlazorAdmin", "Components", "Pages", "HomeActivityChart.razor.css"));

    [Fact]
    public void CanvasContainer_Is280PixelsHigh_AndPositionedForChartJs()
    {
        Css.Should().MatchRegex(@"\.home-activity-chart-canvas\s*\{[^}]*position:\s*relative;[^}]*height:\s*280px;");
    }

    [Fact]
    public void CanvasContainer_Is220PixelsHigh_OnAPhone()
    {
        Css.Should().MatchRegex(
            @"@media \(max-width: 575\.98px\)\s*\{\s*\.home-activity-chart-canvas\s*\{\s*height:\s*220px;");
    }

    [Fact]
    public void Chart_IsNoLongerCappedInWidth()
    {
        // A max-width declaration inside a rule (the media query's own "(max-width: ...)" is fine).
        Css.Should().NotMatchRegex(@"\{[^}]*max-width\s*:");
    }

    [Fact]
    public void Stylesheet_ContainsNoHardCodedColour()
    {
        Regex.IsMatch(Css, @"#[0-9a-fA-F]{3,8}\b").Should().BeFalse();
        Regex.IsMatch(Css, @"rgba?\(").Should().BeFalse();
    }

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExcelETL.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory!.FullName, .. parts]);
    }
}
