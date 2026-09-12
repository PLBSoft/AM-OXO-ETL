namespace ExcelETL.Domain.Archiving;

// A minimal snapshot of an ExtractionError, persisted alongside a GeneratedFileRecord so an
// archived run's non-blocking warnings (or, for a Rejected record, its blocking rejection
// reasons) stay consultable after the fact -- Lot 072. Deliberately not ExtractionError itself:
// that type lives in the pipeline's own Domain.Extraction.Pivot namespace and carries pipeline-
// specific meaning (ExtractionErrorCode), while this record is a plain, unvalidated copy built
// only internally from an already-validated ExtractionError -- same "not a user-facing boundary"
// reasoning as GeneratedFileRecord's own constructor.
public sealed record GeneratedFileWarning(
    string Sheet, string BlockIdentifier, string Code, string Message, string? ExtractedValue = null);
