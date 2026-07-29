using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Pivot;

// A second, deliberate exception to the project's "no generic Result pattern" rule (the first being
// IdentityOperationResult) -- accumulating per-block errors while extraction continues on the next
// block isn't compatible with "throw a typed exception and stop". See
// docs/modele-domaine-import-profile-2026-07-16.md §3.
public sealed record ExtractionError
{
    public string Sheet { get; }
    public string BlockIdentifier { get; }
    public ExtractionErrorCode Code { get; }
    public string Message { get; }

    // The raw extracted value the error relates to, as structured data rather than interpolated only
    // into Message -- Lot 055 §55.3. Not renseigné by RequiredFieldMissing/UnparsableValue/
    // TacheMultipleTypeMismatch, which have no single "extracted value" to report.
    public string? ExtractedValue { get; }

    public ExtractionError(
        string sheet, string blockIdentifier, ExtractionErrorCode code, string message, string? extractedValue = null)
    {
        if (string.IsNullOrWhiteSpace(sheet))
        {
            throw new DomainValidationException(
                "Sheet must not be empty.", nameof(sheet), DomainErrorCode.ExtractionError_EmptySheet);
        }

        if (string.IsNullOrWhiteSpace(blockIdentifier))
        {
            throw new DomainValidationException(
                "Block identifier must not be empty.", nameof(blockIdentifier),
                DomainErrorCode.ExtractionError_EmptyBlockIdentifier);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainValidationException(
                "Message must not be empty.", nameof(message), DomainErrorCode.ExtractionError_EmptyMessage);
        }

        Sheet = sheet;
        BlockIdentifier = blockIdentifier;
        Code = code;
        Message = message;
        ExtractedValue = extractedValue;
    }
}
