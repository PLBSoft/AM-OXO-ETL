using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction.Oxo;

// A field name the extraction needs but the profile doesn't declare (an element sheet without
// Identification/TypeElement/repereEcho, a header template placeholder, a rule on an unknown field) is
// a profile/configuration mistake, not a per-row data problem -- so it throws instead of producing an
// ExtractionError. Derives from
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
