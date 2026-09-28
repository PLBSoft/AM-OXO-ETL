using ExcelETL.BlazorAdmin.Formatting;
using Microsoft.JSInterop;

namespace ExcelETL.BlazorAdmin.Services;

public sealed class ActivityChartInterop(IJSRuntime jsRuntime) : IActivityChartInterop
{
    public Task RenderAsync(string canvasId, ActivityChartModel model) =>
        jsRuntime.InvokeVoidAsync("amOxoActivityChart.render", canvasId, model).AsTask();

    public Task DisposeChartAsync(string canvasId) =>
        jsRuntime.InvokeVoidAsync("amOxoActivityChart.dispose", canvasId).AsTask();
}
