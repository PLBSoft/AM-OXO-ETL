using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.Application.Extraction.Oxo;

public sealed class ConditionalPointRuleEvaluator : IConditionalPointRuleEvaluator
{
    public (bool ShouldCreatePoint, string? WarningMessage) Evaluate(
        IReadOnlyList<ConditionalPointRule> rules, IReadOnlyDictionary<string, string> extractedFields)
    {
        if (rules.Count == 0 || rules.Any(rule => Matches(rule, extractedFields)))
        {
            return (true, null);
        }

        var firstRule = rules[0];
        var actualValue = extractedFields.GetValueOrDefault(firstRule.SourceFieldName);
        return (false,
            $"No configured condition on '{firstRule.SourceFieldName}' matched for Colonne '{firstRule.ColonneName}'; " +
            $"extracted value was '{actualValue}'.");
    }

    private static bool Matches(ConditionalPointRule rule, IReadOnlyDictionary<string, string> extractedFields)
    {
        if (!extractedFields.TryGetValue(rule.SourceFieldName, out var value))
        {
            throw new UnknownFieldReferenceException(rule.SourceFieldName);
        }

        // Trim + case-insensitive: real fixtures have trailing spaces ("SOUPAPE ") and mixed casing
        // vs. the base's confirmed values -- see spec §7. A genuine spelling difference (e.g.
        // "POINT DE FEU" vs "POINT FEU") is not normalized away by this and remains a legitimate
        // non-match, covered by the non-blocking warning policy.
        var isEqual = string.Equals(value.Trim(), rule.ComparisonValue.Trim(), StringComparison.OrdinalIgnoreCase);

        return rule.Operator switch
        {
            ConditionOperator.Equals => isEqual,
            ConditionOperator.NotEquals => !isEqual,
            _ => throw new NotSupportedException($"Unsupported condition operator '{rule.Operator}'.")
        };
    }
}
