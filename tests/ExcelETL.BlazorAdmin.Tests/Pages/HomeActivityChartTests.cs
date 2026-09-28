using System.Globalization;
using Bunit;
using ExcelETL.Application.Home;
using ExcelETL.BlazorAdmin.Components.Pages;
using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

// Lot 089 (089.4): the chart is drawn by Chart.js in a <canvas> (wwwroot/js/activityChart.js), which
// bUnit can't see -- these tests check what the component hands to the script, when, and the hidden
// summary/table that keep the numbers readable without the drawing. The drawing itself is a manual
// check (ticket 089, §5).
public class HomeActivityChartTests : BunitContext
{
    private readonly Mock<IActivityChartInterop> _chart = new();

    public HomeActivityChartTests()
    {
        Services.AddLocalization();
        Services.AddSingleton(_chart.Object);
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
    }

    // 30 days ending 28/09/2026, all empty except the given ones (day of September -> counts).
    private static IReadOnlyList<DailyGenerationActivity> Days(
        params (int DayOfSeptember, int Success, int Warning, int Rejected)[] filled) =>
        Enumerable.Range(0, GenerationActivityBuilder.DayCount)
            .Select(offset => new DateOnly(2026, 8, 30).AddDays(offset))
            .Select(day =>
            {
                var match = filled.FirstOrDefault(f => day == new DateOnly(2026, 9, f.DayOfSeptember));
                return match == default
                    ? new DailyGenerationActivity(day, 0, 0, 0)
                    : new DailyGenerationActivity(day, match.Success, match.Warning, match.Rejected);
            })
            .ToList();

    private IRenderedComponent<HomeActivityChart> RenderChart(IReadOnlyList<DailyGenerationActivity> days) =>
        Render<HomeActivityChart>(parameters => parameters.Add(c => c.Days, days));

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
    public void RendersACanvas_DescribedByTheHiddenSummary_InsideAFixedHeightContainer()
    {
        var cut = RenderChart(Days((27, 1, 0, 0)));

        var canvas = cut.Find("canvas#home-activity-chart");
        canvas.GetAttribute("role").Should().Be("img");
        canvas.GetAttribute("aria-labelledby").Should().Be("home-activity-chart-summary");
        canvas.ParentElement!.ClassList.Should().Contain("home-activity-chart-canvas");
        cut.FindAll("svg").Should().BeEmpty();
    }

    [Fact]
    public void HiddenSummary_GivesTheTotalsInText()
    {
        var cut = InCulture("fr-FR", () => RenderChart(Days((27, 3, 1, 1), (2, 1, 0, 0))));

        cut.Find("#home-activity-chart-summary").TextContent.Should().Be(
            "6 fichier(s) traité(s) du 30/08/2026 au 28/09/2026 : 4 succès, 1 avec avertissements, 1 rejeté(s).");
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

    [Fact]
    public void BeforeTheCircuitIsInteractive_NothingIsDrawn()
    {
        RenderChart(Days((27, 1, 0, 0)));

        _chart.Verify(c => c.RenderAsync(It.IsAny<string>(), It.IsAny<ActivityChartModel>()), Times.Never);
    }

    [Fact]
    public void OnceInteractive_DrawsTheChartInTheCanvas_WithTheDaysCounts()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        ActivityChartModel? drawn = null;
        _chart.Setup(c => c.RenderAsync("home-activity-chart", It.IsAny<ActivityChartModel>()))
            .Callback<string, ActivityChartModel>((_, model) => drawn = model)
            .Returns(Task.CompletedTask);

        RenderChart(Days((27, 3, 1, 2)));

        drawn.Should().NotBeNull();
        drawn!.Labels.Should().HaveCount(30);
        drawn.Datasets.Select(d => d.Values[28]).Should().Equal(3, 1, 2);
        _chart.Verify(c => c.RenderAsync(It.IsAny<string>(), It.IsAny<ActivityChartModel>()), Times.Once);
    }

    [Fact]
    public void NewDaysWithTheSameCounts_DoNotRedrawTheChart()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = RenderChart(Days((27, 1, 0, 0)));

        cut.Render(parameters => parameters.Add(c => c.Days, Days((27, 1, 0, 0))));

        _chart.Verify(c => c.RenderAsync(It.IsAny<string>(), It.IsAny<ActivityChartModel>()), Times.Once);
    }

    [Fact]
    public void DifferentCounts_RedrawTheChart()
    {
        // e.g. the page regroups the days once the browser's time zone is known (lot 088).
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = RenderChart(Days((27, 1, 0, 0)));

        cut.Render(parameters => parameters.Add(c => c.Days, Days((28, 1, 0, 0))));

        _chart.Verify(c => c.RenderAsync(It.IsAny<string>(), It.IsAny<ActivityChartModel>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RemovingTheComponent_RemovesTheChartItDrew()
    {
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        RenderChart(Days((27, 1, 0, 0)));

        await DisposeComponentsAsync();

        _chart.Verify(c => c.DisposeChartAsync("home-activity-chart"), Times.Once);
    }

    [Fact]
    public async Task RemovingTheComponent_BeforeAnythingWasDrawn_CallsNoScript()
    {
        RenderChart(Days((27, 1, 0, 0)));

        await DisposeComponentsAsync();

        _chart.Verify(c => c.DisposeChartAsync(It.IsAny<string>()), Times.Never);
    }
}
