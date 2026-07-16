using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public interface ITextTransformEvaluator
{
    // ErrorMessage is a plain string, not an ExtractionError -- the evaluator has no sheet/block
    // context to build one with. The caller (the repeating-block engine, Lot B3) owns that context
    // and wraps the message into an ExtractionError.
    (string? Value, string? ErrorMessage) Evaluate(
        TextTransform transform, string? rawValue, IReadOnlyDictionary<string, string> extractedFields);
}
