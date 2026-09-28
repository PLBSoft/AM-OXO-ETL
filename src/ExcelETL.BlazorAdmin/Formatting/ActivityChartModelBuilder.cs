using System.Globalization;
using ExcelETL.Application.Home;
using ExcelETL.BlazorAdmin.Resources;
using Microsoft.Extensions.Localization;

namespace ExcelETL.BlazorAdmin.Formatting;

// Lot 089 (089.3): turns the 30 grouped days (lot 088, GenerationActivityBuilder) into what the
// Chart.js script draws. Every text is translated here, in the UI culture: the script holds none.
public static class ActivityChartModelBuilder
{
    public static ActivityChartModel Build(
        IReadOnlyList<DailyGenerationActivity> days, IStringLocalizer<BlazorAdminMessages> localizer)
    {
        var culture = CultureInfo.CurrentUICulture;

        return new ActivityChartModel(
            Labels: days.Select(d => d.Day.ToString("dd/MM", CultureInfo.InvariantCulture)).ToList(),
            TooltipTitles: days.Select(d => d.Day.ToString("dddd d MMMM yyyy", culture)).ToList(),
            // A day with no file shows no tooltip at all (the script filters it out), so only the
            // singular/plural split matters here, never a "0" wording.
            TotalLabels: days
                .Select(d => localizer[d.Total == 1 ? "Home_ActivityTotalSingular" : "Home_ActivityTotalPlural", d.Total].Value)
                .ToList(),
            Datasets:
            [
                new ActivityChartDataset(localizer["Home_ActivityLegendSuccess"], "success", days.Select(d => d.Success).ToList()),
                new ActivityChartDataset(localizer["Home_ActivityLegendWarning"], "warning", days.Select(d => d.Warning).ToList()),
                new ActivityChartDataset(localizer["Home_ActivityLegendRejected"], "rejected", days.Select(d => d.Rejected).ToList()),
            ]);
    }
}
