using ExcelETL.Domain.Exceptions;

namespace ExcelETL.Domain.Extraction.Primitives;

// A reference to another already-extracted field's value, combined by a Concat transform.
public sealed record FieldRef : ConcatPart
{
    public string FieldName { get; }

    public FieldRef(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new DomainValidationException(
                "Field name must not be empty.", nameof(fieldName), DomainErrorCode.FieldRef_EmptyFieldName);
        }

        FieldName = fieldName;
    }
}
