using ExcelETL.Domain.Common;
using ExcelETL.Domain.Enums;

namespace ExcelETL.Domain.Entities;

public class ExtractionHistory : Entity
{
    public DateTimeOffset JobTimestamp { get; }
    public string SourceFileName { get; }
    public string? StoredFilePath { get; private set; }
    public ExtractionStatus Status { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public TimeSpan? Duration => CompletedAtUtc.HasValue ? CompletedAtUtc.Value - JobTimestamp : null;

    public ExtractionHistory(DateTimeOffset jobTimestamp, string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source file name must not be empty.", nameof(sourceFileName));
        }

        JobTimestamp = jobTimestamp;
        SourceFileName = sourceFileName;
        Status = ExtractionStatus.Pending;
    }

    public void MarkCompleted(string storedFilePath)
    {
        if (string.IsNullOrWhiteSpace(storedFilePath))
        {
            throw new ArgumentException("Stored file path must not be empty.", nameof(storedFilePath));
        }

        if (Status is ExtractionStatus.Completed or ExtractionStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot complete an extraction history entry already in status '{Status}'.");
        }

        StoredFilePath = storedFilePath;
        Status = ExtractionStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status is ExtractionStatus.Completed or ExtractionStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot fail an extraction history entry already in status '{Status}'.");
        }

        Status = ExtractionStatus.Failed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}
