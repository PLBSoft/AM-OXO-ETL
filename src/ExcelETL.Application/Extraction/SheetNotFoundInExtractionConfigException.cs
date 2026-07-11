using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction;

// Derives from InvalidOperationException (not Exception) so it's still caught by the existing
// `catch (Exception ex) when (ex is ... or InvalidOperationException ...)` clauses in BlazorAdmin
// that were written against the plain InvalidOperationException this replaces.
public sealed class SheetNotFoundInExtractionConfigException(Guid extractionConfigId, Guid sheetId)
    : InvalidOperationException($"Sheet '{sheetId}' was not found in extraction config '{extractionConfigId}'."),
        IHasApplicationErrorCode
{
    public Guid ExtractionConfigId { get; } = extractionConfigId;

    public Guid SheetId { get; } = sheetId;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.SheetNotFoundInExtractionConfig;

    public IReadOnlyList<object?> Args => [SheetId, ExtractionConfigId];

    public string ResourceKey => ErrorCode.ToString();
}
