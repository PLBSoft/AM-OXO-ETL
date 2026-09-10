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

    // Optional, best-effort traceability of the M2M caller's own end-user (e.g. the legacy app's
    // authenticated username) -- never validated/required, absence is not an error. See
    // GeneratedFileRecord.Username for the full rationale.
    public string? Username { get; set; }
}
