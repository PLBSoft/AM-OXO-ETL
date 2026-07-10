namespace ExcelETL.WebAPI.Contracts;

public sealed class ProcessExcelFileRequest
{
    public Guid ExtractionConfigId { get; set; }

    public IFormFile File { get; set; } = null!;
}
