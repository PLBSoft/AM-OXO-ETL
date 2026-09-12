namespace ExcelETL.WebAPI.Contracts;

// Deliberately excludes SourceFilePath/TargetFilePath (server-local filesystem paths -- no value
// to an M2M caller, and would leak internal server layout for no reason). SourceDownloadUrl/
// TargetDownloadUrl are the caller-facing indirection instead -- relative URLs onto
// GeneratedFilesController's own download routes, so the caller never has to construct the path
// itself. TargetDownloadUrl is null exactly when TargetFileName is (a Rejected record has no
// target to download). Username is optional, best-effort traceability of the M2M caller's own
// end-user -- null when not supplied at processing time, never defaulted to a literal placeholder
// here (the caller decides how to render an absent value).
//
// IsolementCount/PointCount/TacheMultipleCount: how many elements the extraction that produced
// this record actually found. Always plain int, never nullable -- GeneratedFileRecord's own
// collections are always known (never null), including for a Rejected record, which legitimately
// reports 0 for all three rather than an absent value (see
// docs/tickets/tickets-tdd-lot-071-compteurs-elements-historique-fichiers-generes.md).
//
// WarningCount/Warnings (Lot 072): the same non-blocking warnings (or, for a Rejected record, the
// blocking rejection reasons) already snapshotted on GeneratedFileRecord.Warnings at archiving
// time -- same shape as the inline "errors" body OxoController already returns on a 422, so a
// single client-side deserialization type covers both. Always populated identically on Search
// and GetById -- one DTO shape, no separate "summary vs detail" version, since this archive's
// volume doesn't justify the extra complexity (same reasoning as every other field here).
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
    string? Username,
    int IsolementCount,
    int PointCount,
    int TacheMultipleCount,
    int WarningCount,
    IReadOnlyList<GeneratedFileWarningResponse> Warnings);
