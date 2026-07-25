namespace ExcelETL.WebAPI.Contracts;

public sealed class ProcessOxoFileRequest
{
    // Lot 036.1: nullable, not Guid -- a totally-absent multipart field must be distinguishable
    // (400, "missing parameter") from a syntactically-valid Guid that matches no profile (404,
    // handled downstream by ImportProfileNotFoundException/ExportProfileNotFoundException). A
    // syntactically-invalid value (not a Guid at all) is already rejected by ASP.NET Core's own
    // model binding before the action runs, regardless of this property's nullability.
    public Guid? ImportProfileId { get; set; }

    public Guid? ExportProfileId { get; set; }

    public IFormFile File { get; set; } = null!;
}
