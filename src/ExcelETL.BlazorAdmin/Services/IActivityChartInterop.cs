using ExcelETL.BlazorAdmin.Formatting;

namespace ExcelETL.BlazorAdmin.Services;

/// <summary>
/// Lot 089 (089.3): draws the home page's activity chart through wwwroot/js/activityChart.js
/// (Chart.js). Only usable once the component's circuit is interactive -- callers gate on
/// <c>ComponentBase.RendererInfo.IsInteractive</c>, same convention as <see cref="ILocalTimeFormatter"/>.
/// </summary>
public interface IActivityChartInterop
{
    /// <summary>Draws the chart in the canvas, replacing any chart already drawn there.</summary>
    Task RenderAsync(string canvasId, ActivityChartModel model);

    /// <summary>Removes the chart drawn in the canvas, if any.</summary>
    Task DisposeChartAsync(string canvasId);
}
