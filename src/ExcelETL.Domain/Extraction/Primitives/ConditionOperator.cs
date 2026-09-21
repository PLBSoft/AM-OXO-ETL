namespace ExcelETL.Domain.Extraction.Primitives;

public enum ConditionOperator
{
    Equals,
    NotEquals,

    // Lot 084 (G2): the block field holds a value (trimmed non-empty) -- no comparison value.
    IsNotBlank
}
