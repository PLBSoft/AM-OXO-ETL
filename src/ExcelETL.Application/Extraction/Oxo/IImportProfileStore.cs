using ExcelETL.Domain.Extraction.Profile;

namespace ExcelETL.Application.Extraction.Oxo;

public interface IImportProfileStore
{
    Task<IReadOnlyList<ImportProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ImportProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Upsert, keyed by profile.Id: a profile with a new Id is inserted, a profile reconstructed
    // under an existing Id (see ImportProfile's Guid-taking constructor) replaces that profile's
    // content in full, including every nested sheet rule -- this is how editing an existing profile
    // is persisted, not just how a new one is created.
    Task SaveAsync(ImportProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
