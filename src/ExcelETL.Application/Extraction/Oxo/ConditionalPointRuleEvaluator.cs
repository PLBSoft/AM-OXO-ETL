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

        return rule.Operator switch
        {
            ConditionOperator.Equals => value == rule.ComparisonValue,
            ConditionOperator.NotEquals => value != rule.ComparisonValue,
            _ => throw new NotSupportedException($"Unsupported condition operator '{rule.Operator}'.")
        };
    }
}
