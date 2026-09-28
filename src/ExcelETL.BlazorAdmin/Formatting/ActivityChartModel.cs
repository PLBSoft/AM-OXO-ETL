namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 089 (089.3): what wwwroot/js/activityChart.js draws, already translated. Serialized to the
// script in camelCase (labels, tooltipTitles, totalLabels, datasets) by Blazor's JS interop.
public sealed record ActivityChartModel(
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> TooltipTitles,
    IReadOnlyList<string> TotalLabels,
    IReadOnlyList<ActivityChartDataset> Datasets);

// Status is "success", "warning" or "rejected": the script maps it to the theme colour.
public sealed record ActivityChartDataset(string Label, string Status, IReadOnlyList<int> Values);
