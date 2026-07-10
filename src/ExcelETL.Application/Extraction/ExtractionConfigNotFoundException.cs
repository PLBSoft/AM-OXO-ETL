namespace ExcelETL.Application.Extraction;

public sealed class ExtractionConfigNotFoundException(Guid extractionConfigId)
    : Exception($"Extraction config '{extractionConfigId}' was not found.")
{
    public Guid ExtractionConfigId { get; } = extractionConfigId;
}
