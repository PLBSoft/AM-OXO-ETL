namespace ExcelETL.WebAPI.Contracts;

public sealed class ProcessOxoFileRequest
{
    public Guid ImportProfileId { get; set; }

    public Guid ExportProfileId { get; set; }

    public IFormFile File { get; set; } = null!;
}
