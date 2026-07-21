using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Extraction.Oxo;

public sealed class ImportProfileNotFoundException(Guid importProfileId)
    : Exception($"Import profile '{importProfileId}' was not found."), IHasApplicationErrorCode
{
    public Guid ImportProfileId { get; } = importProfileId;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.ImportProfileNotFound;

    public IReadOnlyList<object?> Args => [ImportProfileId];

    public string ResourceKey => ErrorCode.ToString();
}
