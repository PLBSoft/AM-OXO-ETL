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
}
