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
}
