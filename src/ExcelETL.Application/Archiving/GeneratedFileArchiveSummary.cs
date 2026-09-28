namespace ExcelETL.Application.Archiving;

// Lot 054 (54.0/54.2): the aggregate IGeneratedFileArchiveStore.GetSummaryAsync returns for the home
// page's KPI tiles -- MostRecent is null exactly when Count is zero (no generated file has ever been
// archived), never a placeholder.
public sealed record GeneratedFileArchiveSummary(int Count, LastGeneratedFile? MostRecent)
{
    public DateTime? MostRecentGeneratedAtUtc => MostRecent?.GeneratedAtUtc;
}
