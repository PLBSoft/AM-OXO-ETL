using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IConditionalPointRuleEvaluator
{
    // WarningMessage is a plain string, not an ExtractionError, for the same reason as
    // ITextTransformEvaluator: no sheet/block context here to build one with. A non-null
    // WarningMessage is non-blocking (per the model doc §3.2) -- the rule set just didn't match, the
    // caller still processes the rest of the block normally, it just skips creating this one Point.
    (bool ShouldCreatePoint, string? WarningMessage) Evaluate(
        IReadOnlyList<ConditionalPointRule> rules, IReadOnlyDictionary<string, string> extractedFields);
}
