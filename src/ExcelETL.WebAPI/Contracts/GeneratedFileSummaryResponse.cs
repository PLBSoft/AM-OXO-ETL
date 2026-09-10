namespace ExcelETL.WebAPI.Contracts;

// Deliberately excludes SourceFilePath/TargetFilePath (server-local filesystem paths -- no value
// to an M2M caller, and would leak internal server layout for no reason). SourceDownloadUrl/
// TargetDownloadUrl are the caller-facing indirection instead -- relative URLs onto
// GeneratedFilesController's own download routes, so the caller never has to construct the path
// itself. TargetDownloadUrl is null exactly when TargetFileName is (a Rejected record has no
// target to download). Username is optional, best-effort traceability of the M2M caller's own
// end-user -- null when not supplied at processing time, never defaulted to a literal placeholder
// here (the caller decides how to render an absent value).
public sealed record GeneratedFileSummaryResponse(
    Guid Id,
    DateTime GeneratedAtUtc,
    string? EquipementRepere,
    string SourceFileName,
    string? TargetFileName,
    Guid ImportProfileId,
    Guid? ExportProfileId,
    string Status,
    string SourceDownloadUrl,
    string? TargetDownloadUrl,
    string? Username);
