using ExcelETL.Application.Exceptions;

namespace ExcelETL.Application.Generation;

public sealed class ExportProfileNotFoundException(Guid exportProfileId)
    : Exception($"Export profile '{exportProfileId}' was not found."), IHasApplicationErrorCode
{
    public Guid ExportProfileId { get; } = exportProfileId;

    public ApplicationErrorCode ErrorCode => ApplicationErrorCode.ExportProfileNotFound;

    public IReadOnlyList<object?> Args => [ExportProfileId];

    public string ResourceKey => ErrorCode.ToString();
}
