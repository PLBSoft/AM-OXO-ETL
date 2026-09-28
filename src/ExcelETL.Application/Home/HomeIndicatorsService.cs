using ExcelETL.Application.Archiving;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Home;

// Lot 054 (54.2): composes the three existing stores -- no EF Core/SQL knowledge here, no new
// persistence mechanism. Every read is isolated: one store failing never prevents the other
// indicators from being reported, and this method itself never throws (54.4's own requirement, since
// / is the post-login redirect target).
public class HomeIndicatorsService(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore,
    IGeneratedFileArchiveStore generatedFileArchiveStore,
    TimeProvider timeProvider,
    ILogger<HomeIndicatorsService> logger) : IHomeIndicatorsService
{
    public async Task<HomeIndicators> GetIndicatorsAsync(CancellationToken cancellationToken = default)
    {
        var importProfileCount = await ReadAsync(
            "import profile count",
            () => ReadImportProfileCountAsync(cancellationToken));

        var exportProfileCount = await ReadAsync(
            "export profile count",
            () => ReadExportProfileCountAsync(cancellationToken));

        var summary = await ReadAsync(
            "generated file summary",
            () => generatedFileArchiveStore.GetSummaryAsync(cancellationToken));

        var lastGenerationAtUtc = summary.State == HomeIndicatorState.Unavailable
            ? HomeIndicatorValue<DateTime?>.Unavailable()
            : summary.Value!.MostRecentGeneratedAtUtc is { } mostRecent
                ? HomeIndicatorValue<DateTime?>.Known(mostRecent)
                : HomeIndicatorValue<DateTime?>.Absent();

        // Lot 088 (088.3): one day beyond the displayed ones covers any browser time-zone offset
        // (up to 14 h); GenerationActivityBuilder drops what falls outside the local days afterwards.
        var recentActivityFromUtc = timeProvider.GetUtcNow().UtcDateTime
            .AddDays(-(GenerationActivityBuilder.DayCount + 1));
        var recentActivity = await ReadAsync(
            "recent activity",
            () => generatedFileArchiveStore.GetActivitySinceAsync(recentActivityFromUtc, cancellationToken));

        return new HomeIndicators(
            importProfileCount, exportProfileCount, lastGenerationAtUtc, recentActivity);
    }

    private async Task<int> ReadImportProfileCountAsync(CancellationToken cancellationToken) =>
        (await importProfileStore.GetAllAsync(cancellationToken)).Count;

    private async Task<int> ReadExportProfileCountAsync(CancellationToken cancellationToken) =>
        (await exportProfileStore.GetAllAsync(cancellationToken)).Count;

    private async Task<HomeIndicatorValue<T>> ReadAsync<T>(string indicatorName, Func<Task<T>> read)
    {
        try
        {
            return HomeIndicatorValue<T>.Known(await read());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the {IndicatorName} home indicator -- reporting it as unavailable", indicatorName);
            return HomeIndicatorValue<T>.Unavailable();
        }
    }
}
