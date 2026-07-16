using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction.Oxo;

// A Concat referencing a field name that was never extracted is a profile/configuration mistake
// (the SheetExtractionRule's field list doesn't line up with its TextTransforms), not a per-row
// data problem -- so it throws instead of producing an ExtractionError. Derives from
// InvalidOperationException following the existing precedent (see
// SheetNotFoundInExtractionConfigException) rather than a generic base type.
public sealed class UnknownFieldReferenceException(string fieldName)
    : InvalidOperationException($"Field '{fieldName}' was not found among the already-extracted fields."),
        IHasApplicationErrorCode
{
    public string FieldName { get; } = fieldName;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.UnknownFieldReference;

    public IReadOnlyList<object?> Args => [FieldName];

    public string ResourceKey => ErrorCode.ToString();
}
