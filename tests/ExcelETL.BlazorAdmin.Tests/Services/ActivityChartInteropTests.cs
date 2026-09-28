using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Services;

// Lot 089 (089.3): the C# side only forwards to wwwroot/js/activityChart.js -- checks the function
// names and arguments, the drawing itself being Chart.js's.
public class ActivityChartInteropTests
{
    private static readonly ActivityChartModel Model = new(
        ["27/09"], ["dimanche 27 septembre 2026"], ["1 fichier"],
        [new ActivityChartDataset("Succès", "success", [1])]);

    [Fact]
    public async Task RenderAsync_CallsTheScriptsRender_WithTheCanvasIdAndTheModel()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        object?[]? arguments = null;
        jsRuntime
            .Setup(js => js.InvokeAsync<IJSVoidResult>("amOxoActivityChart.render", It.IsAny<object?[]?>()))
            .Callback<string, object?[]?>((_, args) => arguments = args)
            .Returns(new ValueTask<IJSVoidResult>(Mock.Of<IJSVoidResult>()));

        await new ActivityChartInterop(jsRuntime.Object).RenderAsync("home-activity-chart", Model);

        arguments.Should().NotBeNull();
        arguments![0].Should().Be("home-activity-chart");
        arguments[1].Should().BeSameAs(Model);
    }

    [Fact]
    public async Task DisposeChartAsync_CallsTheScriptsDispose_WithTheCanvasId()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        object?[]? arguments = null;
        jsRuntime
            .Setup(js => js.InvokeAsync<IJSVoidResult>("amOxoActivityChart.dispose", It.IsAny<object?[]?>()))
            .Callback<string, object?[]?>((_, args) => arguments = args)
            .Returns(new ValueTask<IJSVoidResult>(Mock.Of<IJSVoidResult>()));

        await new ActivityChartInterop(jsRuntime.Object).DisposeChartAsync("home-activity-chart");

        arguments.Should().Equal("home-activity-chart");
    }
}
