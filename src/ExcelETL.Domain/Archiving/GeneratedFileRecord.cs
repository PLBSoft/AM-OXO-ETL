namespace ExcelETL.Domain.Archiving;

// Same status semantics already displayed client-side (Lot 033's batch badges): Rejected means
// ImportResult.Equipement is null (whole-file rejection, model doc §3.1), NonBlockingWarning means
// Equipement is non-null but ImportResult.HasErrors is true, Success means neither.
public enum GeneratedFileArchiveStatus
{
    Success,
    NonBlockingWarning,
    Rejected
}

// Deliberately permissive, unlike most Domain entities in this project: EquipementRepere/
// TargetFileName/TargetFilePath are all legitimately null when the pipeline rejects the source file
// before an Equipement is ever resolved (model doc §3.1) -- the archive still needs to record that
// the source file was received and rejected (client's own "proof the source data was corrupt" use
// case, see docs/tickets-tdd-lot-034-archivage-fichiers-generes-api.md), so there is no Domain
// invariant forbidding these null combinations the way EquipementPivot/ImportProfile etc. forbid
// blank required fields. SourceFileName/SourceFilePath still guard against a genuinely empty value
// via plain BCL exceptions (a developer invariant -- this record is only ever built internally by
// ProcessOxoFileService, never from a user-facing form, so it stays out of the i18n scope described
// in CLAUDE.md, same reasoning as ImportResult's own constructor).
public sealed class GeneratedFileRecord
{
    public Guid Id { get; }
    public DateTime GeneratedAtUtc { get; }
    public string? EquipementRepere { get; }
    public string SourceFileName { get; }
    public string SourceFilePath { get; }
    public string? TargetFileName { get; }
    public string? TargetFilePath { get; }
    public Guid ImportProfileId { get; }
    public Guid? ExportProfileId { get; }
    public GeneratedFileArchiveStatus Status { get; }

    public GeneratedFileRecord(
        Guid id,
        DateTime generatedAtUtc,
        string? equipementRepere,
        string sourceFileName,
        string sourceFilePath,
        string? targetFileName,
        string? targetFilePath,
        Guid importProfileId,
        Guid? exportProfileId,
        GeneratedFileArchiveStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        Id = id;
        GeneratedAtUtc = generatedAtUtc;
        EquipementRepere = equipementRepere;
        SourceFileName = sourceFileName;
        SourceFilePath = sourceFilePath;
        TargetFileName = targetFileName;
        TargetFilePath = targetFilePath;
        ImportProfileId = importProfileId;
        ExportProfileId = exportProfileId;
        Status = status;
    }
}
