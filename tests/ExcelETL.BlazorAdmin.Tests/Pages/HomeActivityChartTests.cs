using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using ExcelETL.Application.Home;
using ExcelETL.BlazorAdmin.Components.Pages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

// Lot 088 (088.6): the activity chart's marks, checked directly on the SVG (no chart library, no
// JavaScript -- D6). Layout itself (does it fit the card?) is out of bUnit's reach.
public class HomeActivityChartTests : BunitContext
{
    public HomeActivityChartTests()
    {
        Services.AddLocalization();
    }

    // 30 days ending 28/09/2026, all empty except the given ones (day of September -> counts).
    private static IReadOnlyList<DailyGenerationActivity> Days(
        params (int DayOfSeptember, int Success, int Warning, int Rejected)[] filled)
    {
        var firstDay = new DateOnly(2026, 8, 30);
        return Enumerable.Range(0, GenerationActivityBuilder.DayCount)
            .Select(offset =>
            {
                var day = firstDay.AddDays(offset);
                var match = filled.FirstOrDefault(f => day == new DateOnly(2026, 9, f.DayOfSeptember));
                return match == default
                    ? new DailyGenerationActivity(day, 0, 0, 0)
                    : new DailyGenerationActivity(day, match.Success, match.Warning, match.Rejected);
            })
            .ToList();
    }

    private IRenderedComponent<HomeActivityChart> RenderChart(IReadOnlyList<DailyGenerationActivity> days) =>
        Render<HomeActivityChart>(parameters => parameters.Add(c => c.Days, days));

    private static IElement Bar(IRenderedComponent<HomeActivityChart> cut, string isoDay) =>
        cut.Find($"g.home-activity-bar[data-day='{isoDay}']");

    private static double Number(IElement element, string attribute) =>
        double.Parse(element.GetAttribute(attribute)!, CultureInfo.InvariantCulture);

    private static T InCulture<T>(string culture, Func<T> action)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void RendersOneBarPerDay_FromTheOldestToToday()
    {
        var cut = RenderChart(Days((27, 1, 0, 0)));

        cut.FindAll("g.home-activity-bar").Select(g => g.GetAttribute("data-day")).Should().Equal(
            Enumerable.Range(0, 30).Select(i => new DateOnly(2026, 8, 30).AddDays(i).ToString("yyyy-MM-dd")));
    }

    [Fact]
    public void TheBusiestDay_FillsThePlotHeight_AndOtherBarsAreProportional()
    {
        var cut = RenderChart(Days((27, 2, 1, 1), (26, 2, 0, 0)));

        Bar(cut, "2026-09-27").QuerySelectorAll("rect.home-activity-segment")
            .Sum(r => Number(r, "height")).Should().BeApproximately(HomeActivityChart.PlotHeight, 0.01);
        Bar(cut, "2026-09-26").QuerySelectorAll("rect.home-activity-segment")
            .Sum(r => Number(r, "height")).Should().BeApproximately(HomeActivityChart.PlotHeight / 2, 0.01);
        cut.Find("#home-activity-chart-max").TextContent.Should().Be("4");
    }

    [Fact]
    public void Segments_AreStackedBottomUp_SuccessThenWarningThenRejected_AndAZeroCountDrawsNothing()
    {
        var cut = RenderChart(Days((27, 1, 1, 1), (26, 3, 0, 0)));

        var stacked = Bar(cut, "2026-09-27").QuerySelectorAll("rect.home-activity-segment").ToList();
        stacked.Select(r => r.ClassList.Last()).Should().Equal(
            "home-activity-segment-success", "home-activity-segment-warning", "home-activity-segment-rejected");
        // Each segment sits right on top of the one before it (smaller y = higher on screen).
        Number(stacked[1], "y").Should().BeApproximately(Number(stacked[0], "y") - Number(stacked[1], "height"), 0.01);
        Number(stacked[2], "y").Should().BeApproximately(Number(stacked[1], "y") - Number(stacked[2], "height"), 0.01);

        Bar(cut, "2026-09-26").QuerySelectorAll("rect.home-activity-segment").Should().ContainSingle();
        Bar(cut, "2026-09-25").QuerySelectorAll("rect.home-activity-segment").Should().BeEmpty();
    }

    [Fact]
    public void EveryBar_HasATooltipWithItsDateAndCounts()
    {
        var cut = InCulture("fr-FR", () => RenderChart(Days((27, 3, 1, 1))));

        Bar(cut, "2026-09-27").QuerySelector("title")!.TextContent
            .Should().Be("27/09/2026 : 5 fichier(s) (3 succès, 1 avec avertissements, 1 rejeté(s))");
        Bar(cut, "2026-09-25").QuerySelector("title")!.TextContent
            .Should().Be("25/09/2026 : 0 fichier(s) (0 succès, 0 avec avertissements, 0 rejeté(s))");
    }

    [Fact]
    public void EveryBar_HasAFullHeightHoverTarget()
    {
        var cut = RenderChart(Days((27, 1, 0, 0)));

        cut.FindAll("rect.home-activity-hit").Should().HaveCount(30)
            .And.OnlyContain(r => Math.Abs(Number(r, "height") - HomeActivityChart.PlotHeight) < 0.01);
    }

    [Fact]
    public void AxisLabels_ShowOneDateAWeek_EndingWithToday()
    {
        var cut = RenderChart(Days());

        cut.FindAll("text.home-activity-day-label").Select(t => t.TextContent)
            .Should().Equal("31/08", "07/09", "14/09", "21/09", "28/09");
    }

    [Fact]
    public void SvgAttributes_UseADotAsDecimalSeparator_EvenInFrench()
    {
        var cut = InCulture("fr-FR", () => RenderChart(Days((27, 1, 1, 1))));

        cut.FindAll("rect").SelectMany(r => new[] { r.GetAttribute("x"), r.GetAttribute("y"), r.GetAttribute("width"), r.GetAttribute("height") })
            .Should().OnlyContain(value => value != null && !value.Contains(','));
    }

    [Fact]
    public void Svg_IsDescribedByATextSummary_ForScreenReaders()
    {
        var cut = InCulture("fr-FR", () => RenderChart(Days((27, 3, 1, 1), (2, 1, 0, 0))));

        cut.Find("#home-activity-chart").GetAttribute("role").Should().Be("img");
        cut.Find("#home-activity-chart").GetAttribute("aria-labelledby").Should().Be("home-activity-chart-summary");
        cut.Find("#home-activity-chart-summary").TextContent.Should().Be(
            "6 fichier(s) traité(s) du 30/08/2026 au 28/09/2026 : 4 succès, 1 avec avertissements, 1 rejeté(s).");
    }

    [Fact]
    public void Legend_NamesTheThreeStatusesInText()
    {
        var cut = InCulture("fr-FR", () => RenderChart(Days((27, 1, 0, 0))));

        cut.FindAll("#home-activity-legend li").Select(li => li.TextContent.Trim())
            .Should().Equal("Succès", "Avec avertissements", "Rejetés");
    }

    [Fact]
    public void AccessibleTable_HasOneRowPerDay_WithItsCounts()
    {
        var cut = RenderChart(Days((27, 3, 1, 2)));

        var rows = cut.FindAll("#home-activity-table tbody tr");
        rows.Should().HaveCount(30);
        var row = rows.Single(r => r.QuerySelector("th")!.TextContent == "27/09/2026");
        row.QuerySelectorAll("td").Select(td => td.TextContent).Should().Equal("3", "1", "2");
    }
}
