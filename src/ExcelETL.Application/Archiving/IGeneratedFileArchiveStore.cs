using ExcelETL.Domain.Archiving;

namespace ExcelETL.Application.Archiving;

// Mirrors IImportProfileStore/IExportProfileStore's shape, but append-only: a GeneratedFileRecord is
// never updated or deleted once written (no purge/retention policy in this lot, see the ticket).
public interface IGeneratedFileArchiveStore
{
    Task SaveAsync(GeneratedFileRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedFileRecord>> SearchAsync(
        string? equipementRepere, CancellationToken cancellationToken = default);

    Task<GeneratedFileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Lot 054 (54.0/54.2): a dedicated aggregate read, not GetAllAsync().Count -- unlike
    // ImportProfile/ExportProfile (a few dozen rows, in-memory counting is fine), this table's
    // volume grows unboundedly (no purge policy). Implemented as a real SQL aggregate in
    // Infrastructure, never as an in-memory LINQ query over every row.
    Task<GeneratedFileArchiveSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    // Lot 090: records whose files were never purged and that were generated before cutoffUtc.
    Task<IReadOnlyList<GeneratedFileRecord>> GetRecordsWithFilesOlderThanAsync(
        DateTime cutoffUtc, CancellationToken cancellationToken = default);

    // Lot 090: the only mutation this store allows -- records that their files were purged. The
    // record itself stays as history; unknown id is a no-op.
    Task MarkFilesPurgedAsync(Guid id, DateTime purgedAtUtc, CancellationToken cancellationToken = default);

    // Lot 088 (088.1): date and status of every file archived from fromUtc (inclusive) onwards,
    // projected in SQL -- the home page groups them into days itself, in the browser's time zone.
    Task<IReadOnlyList<GeneratedFileActivityEntry>> GetActivitySinceAsync(
        DateTime fromUtc, CancellationToken cancellationToken = default);
}
