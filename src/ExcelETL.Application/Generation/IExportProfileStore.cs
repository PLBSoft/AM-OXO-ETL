using ExcelETL.Domain.Generation.Profile;

namespace ExcelETL.Application.Generation;

public interface IExportProfileStore
{
    Task<IReadOnlyList<ExportProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ExportProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Upsert, keyed by profile.Id -- same convention as IImportProfileStore.SaveAsync: a profile
    // reconstructed under an existing Id (see ExportProfile's Guid-taking constructor) replaces that
    // profile's content in full, including every nested sheet rule.
    Task SaveAsync(ExportProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
