using ExcelETL.Domain.Common;
using ExcelETL.Domain.Enums;
using ExcelETL.Domain.Exceptions;

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
            throw new DomainValidationException(
                "Source file name must not be empty.", nameof(sourceFileName),
                DomainErrorCode.ExtractionHistory_EmptySourceFileName);
        }

        JobTimestamp = jobTimestamp;
        SourceFileName = sourceFileName;
        Status = ExtractionStatus.Pending;
    }

    public void MarkCompleted(string storedFilePath)
    {
        if (string.IsNullOrWhiteSpace(storedFilePath))
        {
            throw new DomainValidationException(
                "Stored file path must not be empty.", nameof(storedFilePath),
                DomainErrorCode.ExtractionHistory_EmptyStoredFilePath);
        }

        if (Status is ExtractionStatus.Completed or ExtractionStatus.Failed)
        {
            throw new DomainRuleViolationException(
                $"Cannot complete an extraction history entry already in status '{Status}'.",
                DomainErrorCode.ExtractionHistory_CannotCompleteFromStatus,
                Status);
        }

        StoredFilePath = storedFilePath;
        Status = ExtractionStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status is ExtractionStatus.Completed or ExtractionStatus.Failed)
        {
            throw new DomainRuleViolationException(
                $"Cannot fail an extraction history entry already in status '{Status}'.",
                DomainErrorCode.ExtractionHistory_CannotFailFromStatus,
                Status);
        }

        Status = ExtractionStatus.Failed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}
