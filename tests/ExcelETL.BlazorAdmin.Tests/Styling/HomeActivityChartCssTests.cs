using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Styling;

// Lot 088 (088.6): bUnit computes no CSS, so the chart's colours are checked on the stylesheet text,
// same convention as the rest of this folder. Every segment colour must come from a theme token
// (redefined for the dark theme by theme-m3.css), never a colour written by hand (D5).
public class HomeActivityChartCssTests
{
    private static string Css { get; } = File.ReadAllText(RepoPath(
        "src", "ExcelETL.BlazorAdmin", "Components", "Pages", "HomeActivityChart.razor.css"));

    [Theory]
    [InlineData("success", "--m3-success")]
    [InlineData("warning", "--m3-warning")]
    [InlineData("rejected", "--m3-danger")]
    public void EachStatusSegment_IsFilledWithItsThemeToken(string status, string token)
    {
        Css.Should().MatchRegex(
            $@"\.home-activity-segment-{status}\s*\{{[^}}]*fill:\s*var\({token}\);");
    }

    [Fact]
    public void Stylesheet_ContainsNoHardCodedColour()
    {
        Regex.IsMatch(Css, @"#[0-9a-fA-F]{3,8}\b").Should().BeFalse();
        Regex.IsMatch(Css, @"rgba?\(\s*\d").Should().BeFalse();
    }

    [Fact]
    public void StackedSegments_AreSeparatedByAGapInTheCardColour()
    {
        Css.Should().MatchRegex(@"\.home-activity-segment\s*\{[^}]*stroke:\s*var\(--bs-card-bg\);[^}]*stroke-width:\s*2;");
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
