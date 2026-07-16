using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// Guards whether a Point is created for a given already-extracted field value. An empty rule list
// for a sheet means "always create the Point" -- see docs/modele-domaine-import-profile-2026-07-16.md §1.4.
public sealed record ConditionalPointRule
{
    public string SourceFieldName { get; }
    public ConditionOperator Operator { get; }
    public string ComparisonValue { get; }
    public string ColonneName { get; }

    public ConditionalPointRule(string sourceFieldName, ConditionOperator @operator, string comparisonValue, string colonneName)
    {
        if (string.IsNullOrWhiteSpace(sourceFieldName))
        {
            throw new DomainValidationException(
                "Source field name must not be empty.", nameof(sourceFieldName),
                DomainErrorCode.ConditionalPointRule_EmptySourceFieldName);
        }

        if (string.IsNullOrWhiteSpace(comparisonValue))
        {
            throw new DomainValidationException(
                "Comparison value must not be empty.", nameof(comparisonValue),
                DomainErrorCode.ConditionalPointRule_EmptyComparisonValue);
        }

        if (string.IsNullOrWhiteSpace(colonneName))
        {
            throw new DomainValidationException(
                "Colonne name must not be empty.", nameof(colonneName),
                DomainErrorCode.ConditionalPointRule_EmptyColonneName);
        }

        SourceFieldName = sourceFieldName;
        Operator = @operator;
        ComparisonValue = comparisonValue;
        ColonneName = colonneName;
    }
}
