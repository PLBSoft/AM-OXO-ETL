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

    // The M2M caller's own end-user, when it chooses to supply one (e.g. the legacy app's own
    // authenticated username) -- optional and last, not a required constructor parameter: this
    // is a best-effort traceability field, not a business invariant of the record, and making it
    // required would have forced every existing construction site (and every M2M caller not yet
    // updated to send it) to change at once. Null means "not supplied", not "unknown user" as a
    // literal string -- callers decide how to render that. Truncated (never rejected) at
    // MaxUsernameLength in the constructor -- this field is best-effort metadata, not a business
    // fact worth losing the WHOLE archive record over (source/target file writes included) if a
    // caller sends something oversized; the alternative (an unvalidated value reaching
    // EfGeneratedFileArchiveStore.SaveAsync against the nvarchar(200) column) throws deep inside a
    // catch-and-log-only block in ProcessOxoFileService.TryArchiveAsync, silently dropping the
    // entire record with no signal to the caller or an admin.
    public string? Username { get; }

    // Matches GeneratedFileRecordConfiguration's HasMaxLength(MaxUsernameLength) column mapping --
    // one source of truth for the two places (Domain, EF configuration) that must agree on it.
    public const int MaxUsernameLength = 200;

    // How many Isolements/Points/TachesMultiples the run this record archives actually produced --
    // read straight off ImportResult's own collections at archiving time (ProcessOxoFileService).
    // Always known, never null: ImportResult's constructor guards Isolements/Points/TachesMultiples
    // as non-null IReadOnlyList<T>s, so .Count is valid in every status, including Rejected (the
    // whole-file-rejection case produces empty, not null, lists -- see
    // docs/tickets/tickets-tdd-lot-071-compteurs-elements-historique-fichiers-generes.md, 071.0).
    // Optional, last in the constructor and defaulting to 0, for the same reason as Username: the
    // sole production call site always supplies real values, but making them required would have
    // forced a mechanical update of every one of this constructor's other (test-only) call sites.
    public int IsolementCount { get; }
    public int PointCount { get; }
    public int TacheMultipleCount { get; }

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
        GeneratedFileArchiveStatus status,
        string? username = null,
        int isolementCount = 0,
        int pointCount = 0,
        int tacheMultipleCount = 0)
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
        Username = username is { Length: > MaxUsernameLength } ? username[..MaxUsernameLength] : username;
        IsolementCount = isolementCount;
        PointCount = pointCount;
        TacheMultipleCount = tacheMultipleCount;
    }
}
