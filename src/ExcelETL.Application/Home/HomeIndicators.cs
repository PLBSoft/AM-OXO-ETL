namespace ExcelETL.Application.Home;

// Lot 054 (54.2): the immutable snapshot IHomeIndicatorsService returns. Exactly four indicators,
// per the ticket's own explicit "epure" constraint -- a fifth requires a new ticket, not an
// extension of this record.
public sealed record HomeIndicators(
    HomeIndicatorValue<int> ImportProfileCount,
    HomeIndicatorValue<int> ExportProfileCount,
    HomeIndicatorValue<int> GeneratedFileCount,
    HomeIndicatorValue<DateTime?> LastGenerationAtUtc);
