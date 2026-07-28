using ExcelETL.Application.Archiving;
using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Application.Generation;
using Microsoft.Extensions.Logging;

namespace ExcelETL.Application.Home;

// Lot 054 (54.2): composes the three existing stores -- no EF Core/SQL knowledge here, no new
// persistence mechanism. Every read is isolated: one store failing never prevents the other three
// indicators from being reported, and this method itself never throws (54.4's own requirement, since
// / is the post-login redirect target).
public class HomeIndicatorsService(
    IImportProfileStore importProfileStore,
    IExportProfileStore exportProfileStore,
    IGeneratedFileArchiveStore generatedFileArchiveStore,
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

        // Both the generated-file count and the last-generation date come from one store call: a
        // GeneratedFileArchiveStore failure marks both indicators unavailable together, rather than
        // querying the store twice for two independent indicators backed by the same aggregate.
        GeneratedFileArchiveSummary? summary = null;
        var generatedFileCount = await ReadAsync("generated file count", async () =>
        {
            summary = await generatedFileArchiveStore.GetSummaryAsync(cancellationToken);
            return summary.Count;
        });

        var lastGenerationAtUtc = generatedFileCount.State == HomeIndicatorState.Unavailable
            ? HomeIndicatorValue<DateTime?>.Unavailable()
            : summary!.MostRecentGeneratedAtUtc is { } mostRecent
                ? HomeIndicatorValue<DateTime?>.Known(mostRecent)
                : HomeIndicatorValue<DateTime?>.Absent();

        return new HomeIndicators(importProfileCount, exportProfileCount, generatedFileCount, lastGenerationAtUtc);
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
