using System.Text.Json.Serialization;
using ExcelETL.Domain.Extraction.Primitives;

namespace ExcelETL.BlazorAdmin.Editing.Import;

// Mutable, behavior-free mirror of ConditionalPointRule (Domain).
public sealed class ConditionalPointRuleDraft : IDraftWithError
{
    public string ColonneName { get; set; } = string.Empty;
    public string SourceFieldName { get; set; } = string.Empty;
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equals;
    // Blank for the IsNotBlank operator (lot 084): the form hides and clears it.
    public string ComparisonValue { get; set; } = string.Empty;

    [JsonIgnore]
    public string? Error { get; set; }
}
