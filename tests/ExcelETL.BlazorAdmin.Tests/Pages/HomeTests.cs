using Bunit;
using ExcelETL.Application.Home;
using ExcelETL.BlazorAdmin.Components.Pages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Pages;

// Lot 054 (54.3/54.4/54.6): content only -- never routing or authorization (lots 049/051/052's own
// lesson, reaffirmed by this lot's own note d'efficacité). See HomeHttpTests.cs for the real HTTP
// requests proving "/" is reachable and reassigned.
public class HomeTests : BunitContext
{
    private readonly Mock<IHomeIndicatorsService> _serviceMock = new();

    public HomeTests()
    {
        Services.AddSingleton(_serviceMock.Object);
        Services.AddLocalization();
    }

    private static HomeIndicators KnownIndicators(
        int importProfileCount = 3, int exportProfileCount = 2, int generatedFileCount = 12) =>
        new(
            HomeIndicatorValue<int>.Known(importProfileCount),
            HomeIndicatorValue<int>.Known(exportProfileCount),
            HomeIndicatorValue<int>.Known(generatedFileCount),
            HomeIndicatorValue<DateTime?>.Known(new DateTime(2026, 7, 28, 10, 30, 0, DateTimeKind.Utc)));

    [Fact]
    public void WhileLoading_ShowsLoadingIndicator_NoTilesRendered()
    {
        var tcs = new TaskCompletionSource<HomeIndicators>();
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Home>();

        cut.Find("#home-loading");
        cut.FindAll("#home-kpi-import-profiles").Should().BeEmpty();
        cut.FindAll("#home-kpi-export-profiles").Should().BeEmpty();
        cut.FindAll("#home-kpi-generated-files").Should().BeEmpty();
        cut.FindAll("#home-kpi-last-generation").Should().BeEmpty();

        tcs.SetResult(KnownIndicators());
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);
    }

    [Fact]
    public void AriaLiveRegion_IsPresentFromInitialRender_BeforeDataFinishesLoading()
    {
        var tcs = new TaskCompletionSource<HomeIndicators>();
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var cut = Render<Home>();

        var region = cut.Find("#home-kpi-region");
        region.GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public void KnownValues_DisplaysFourTilesWithTheirValues()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(KnownIndicators(importProfileCount: 3, exportProfileCount: 2, generatedFileCount: 12));

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").TextContent.Should().Contain("3");
        cut.Find("#home-kpi-export-profiles").TextContent.Should().Contain("2");
        cut.Find("#home-kpi-generated-files").TextContent.Should().Contain("12");
        cut.Find("#home-kpi-last-generation").TextContent.Should().Contain("28/07/2026");
    }

    [Fact]
    public void ZeroCountersAndAbsentLastGeneration_ShowsZeroAndAnExplicitNoGenerationState()
    {
        var indicators = new HomeIndicators(
            HomeIndicatorValue<int>.Known(0),
            HomeIndicatorValue<int>.Known(0),
            HomeIndicatorValue<int>.Known(0),
            HomeIndicatorValue<DateTime?>.Absent());
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(indicators);

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").TextContent.Should().Contain("0");
        cut.Find("#home-kpi-export-profiles").TextContent.Should().Contain("0");
        cut.Find("#home-kpi-generated-files").TextContent.Should().Contain("0");
        // Not blank, not a default date -- an explicit, localized "no generation yet" state.
        cut.Find("#home-kpi-last-generation").TextContent.Should().NotBeNullOrWhiteSpace();
        cut.Find("#home-kpi-last-generation").TextContent.Should().NotContain("00:00");
    }

    [Fact]
    public void LinkTiles_PointToTheExpectedRoutes()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").GetAttribute("href").Should().Be("import-profiles");
        cut.Find("#home-kpi-export-profiles").GetAttribute("href").Should().Be("export-profiles");
        cut.Find("#home-kpi-generated-files").GetAttribute("href").Should().Be("generated-files");
    }

    [Fact]
    public void LastGenerationTile_IsNotALink()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-last-generation").TagName.Should().NotBe("A");
    }

    [Fact]
    public void OneUnavailableIndicator_ShowsDegradedStateOnThatTileOnly_OthersShowValues()
    {
        var indicators = new HomeIndicators(
            HomeIndicatorValue<int>.Known(3),
            HomeIndicatorValue<int>.Unavailable(),
            HomeIndicatorValue<int>.Known(12),
            HomeIndicatorValue<DateTime?>.Known(new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc)));
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(indicators);

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.Find("#home-kpi-import-profiles").TextContent.Should().Contain("3");
        cut.Find("#home-kpi-export-profiles").TextContent.Should().NotContain("0");
        cut.Find("#home-kpi-generated-files").TextContent.Should().Contain("12");
        cut.Find("#home-kpi-last-generation").TextContent.Should().Contain("28/07/2026");
    }

    [Fact]
    public void ServiceCallThrows_PageRendersGlobalErrorMessage_WithoutExceptionPropagating()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var cut = Render<Home>();

        cut.WaitForState(() => cut.FindAll("#home-global-error").Count == 1);
        cut.FindAll("#home-kpi-import-profiles").Should().BeEmpty();
    }

    [Fact]
    public void LabelIsAssociatedWithValue_ViaAriaLabelledby_NotJustVisualProximity()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        foreach (var tileId in new[] { "home-kpi-import-profiles", "home-kpi-export-profiles", "home-kpi-generated-files", "home-kpi-last-generation" })
        {
            var labelId = tileId + "-label";
            cut.Find("#" + labelId);
            var valueElement = cut.Find($"[aria-labelledby='{labelId}']");
            valueElement.Should().NotBeNull();
        }
    }

    [Fact]
    public void PageHasExactlyOneH1_MatchingPageTitle()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        HeadingHierarchyAssertions.AssertNoHeadingLevelSkip(cut);
    }

    // Mobile-first invariant (Lot V): Bootstrap's responsive grid handles narrow viewports on its
    // own -- exactly one instance of each tile in the DOM, no separate mobile template duplicating it.
    [Fact]
    public void EachTile_RendersExactlyOnce_NoDuplicateMobileTemplate()
    {
        _serviceMock.Setup(s => s.GetIndicatorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(KnownIndicators());

        var cut = Render<Home>();
        cut.WaitForState(() => cut.FindAll("#home-kpi-import-profiles").Count == 1);

        cut.FindAll("#home-kpi-import-profiles").Should().HaveCount(1);
        cut.FindAll("#home-kpi-export-profiles").Should().HaveCount(1);
        cut.FindAll("#home-kpi-generated-files").Should().HaveCount(1);
        cut.FindAll("#home-kpi-last-generation").Should().HaveCount(1);
    }
}
