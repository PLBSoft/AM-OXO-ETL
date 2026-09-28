using ExcelETL.Application.Archiving;

namespace ExcelETL.Application.Home;

// Lot 054 (54.2): the immutable snapshot IHomeIndicatorsService returns. Lot 088 added
// RecentActivity (the "30 derniers jours" tile and the activity chart); the page groups it into
// local days itself, since only the page knows the browser's time zone (GenerationActivityBuilder).
// Any further indicator needs its own ticket, not an extension of this record by default.
public sealed record HomeIndicators(
    HomeIndicatorValue<int> ImportProfileCount,
    HomeIndicatorValue<int> ExportProfileCount,
    HomeIndicatorValue<int> GeneratedFileCount,
    HomeIndicatorValue<DateTime?> LastGenerationAtUtc,
    HomeIndicatorValue<IReadOnlyList<GeneratedFileActivityEntry>> RecentActivity);
