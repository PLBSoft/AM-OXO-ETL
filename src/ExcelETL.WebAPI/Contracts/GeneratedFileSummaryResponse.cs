namespace ExcelETL.WebAPI.Contracts;

// Deliberately excludes SourceFilePath/TargetFilePath (server-local filesystem paths -- no value
// to an M2M caller, and would leak internal server layout for no reason).
public sealed record GeneratedFileSummaryResponse(
    Guid Id,
    DateTime GeneratedAtUtc,
    string? EquipementRepere,
    string SourceFileName,
    string? TargetFileName,
    Guid ImportProfileId,
    Guid? ExportProfileId,
    string Status);
