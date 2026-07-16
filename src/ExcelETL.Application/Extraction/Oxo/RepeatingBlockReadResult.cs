using ExcelETL.Domain.Extraction.Pivot;

namespace ExcelETL.Application.Extraction.Oxo;

// Purpose-built output of one IRepeatingBlockReader.Read call -- same spirit as the pre-existing
// ExtractionResult DTO (Extraction/ExtractionResult.cs), not a generic Result<T> wrapper. Raw field
// values only (TextTransform has not run yet -- that's the caller's job, using
// ITextTransformEvaluator).
public sealed record RepeatingBlockReadResult(
    IReadOnlyList<IReadOnlyDictionary<string, string>> Blocks,
    IReadOnlyList<ExtractionError> Errors);
