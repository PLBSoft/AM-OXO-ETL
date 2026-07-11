using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction;

// Derives from InvalidOperationException -- see SheetNotFoundInExtractionConfigException for why.
public sealed class ExtractionHistoryNotFoundException(Guid extractionHistoryId)
    : InvalidOperationException($"Extraction history '{extractionHistoryId}' was not found.")
{
    public Guid ExtractionHistoryId { get; } = extractionHistoryId;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.ExtractionHistoryNotFound;

    public IReadOnlyList<object?> Args => [ExtractionHistoryId];
}
