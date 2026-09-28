using System.Globalization;
using ExcelETL.Application.Home;
using ExcelETL.BlazorAdmin.Formatting;
using ExcelETL.BlazorAdmin.Resources;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Formatting;

// Lot 089 (089.3): everything the Chart.js script shows is prepared here, already translated -- the
// script itself holds no text. Real .resx localizer, same convention as DescriptionTestSupport.
public class ActivityChartModelBuilderTests
{
    private static readonly IStringLocalizer<BlazorAdminMessages> Localizer = new ServiceCollection()
        .AddLogging()
        .AddLocalization()
        .BuildServiceProvider()
        .GetRequiredService<IStringLocalizer<BlazorAdminMessages>>();

    private static IReadOnlyList<DailyGenerationActivity> Days(params (int DayOfSeptember, int Success, int Warning, int Rejected)[] filled) =>
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

    private static ActivityChartModel BuildIn(string culture, IReadOnlyList<DailyGenerationActivity> days)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return ActivityChartModelBuilder.Build(days, Localizer);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Build_HasOneLabelPerDay_InDayMonthFormat_OldestFirst()
    {
        var model = BuildIn("fr-FR", Days());

        model.Labels.Should().HaveCount(30);
        model.Labels[0].Should().Be("30/08");
        model.Labels[^1].Should().Be("28/09");
    }

    [Fact]
    public void Build_HasThreeSeries_SuccessThenWarningThenRejected_WithEachDaysCounts()
    {
        var model = BuildIn("fr-FR", Days((27, 3, 1, 2)));

        model.Datasets.Select(d => d.Status).Should().Equal("success", "warning", "rejected");
        model.Datasets.Select(d => d.Values.Count).Should().OnlyContain(count => count == 30);
        model.Datasets.Select(d => d.Values[28]).Should().Equal(3, 1, 2);
        model.Datasets.Select(d => d.Values[27]).Should().Equal(0, 0, 0);
    }

    [Fact]
    public void Build_InFrench_NamesTheSeries_AndWritesFullDatesAndTotals()
    {
        var model = BuildIn("fr-FR", Days((27, 3, 1, 1), (26, 1, 0, 0)));

        model.Datasets.Select(d => d.Label).Should().Equal("Succès", "Avec avertissements", "Rejetés");
        model.TooltipTitles[28].Should().Be("dimanche 27 septembre 2026");
        model.TotalLabels[28].Should().Be("5 fichiers");
        model.TotalLabels[27].Should().Be("1 fichier");
    }

    [Fact]
    public void Build_InEnglish_NamesTheSeries_AndWritesFullDatesAndTotals()
    {
        var model = BuildIn("en-US", Days((27, 3, 1, 1), (26, 1, 0, 0)));

        model.Datasets.Select(d => d.Label).Should().Equal("Successful", "With warnings", "Rejected");
        model.TooltipTitles[28].Should().Be("Sunday 27 September 2026");
        model.TotalLabels[28].Should().Be("5 files");
        model.TotalLabels[27].Should().Be("1 file");
    }
}
