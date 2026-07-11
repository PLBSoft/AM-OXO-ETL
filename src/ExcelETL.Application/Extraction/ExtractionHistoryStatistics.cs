namespace ExcelETL.Application.Extraction;

public sealed record ExtractionHistoryStatistics(
    int TotalJobs,
    int PendingJobs,
    int CompletedJobs,
    int FailedJobs,
    TimeSpan? AverageCompletedDuration);
