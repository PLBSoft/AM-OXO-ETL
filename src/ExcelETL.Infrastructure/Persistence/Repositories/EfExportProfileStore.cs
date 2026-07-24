using ExcelETL.Application.Exceptions;
using ExcelETL.Application.Generation;
using ExcelETL.Domain.Generation.Profile;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

// Symmetric to EfImportProfileStore -- same short-lived-DbContext-per-method pattern, same
// two-round-trip upsert (a Removed and an Added instance under the same key can't be tracked
// simultaneously by EF Core's change tracker, confirmed empirically on the import side).
public class EfExportProfileStore(IDbContextFactory<ExcelEtlDbContext> dbContextFactory) : IExportProfileStore
{
    public async Task<IReadOnlyList<ExportProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExportProfiles
            .OrderBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExportProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExportProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
    }

    public async Task SaveAsync(ExportProfile profile, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // See EfImportProfileStore.SaveAsync for the rationale (normalized Trim + OrdinalIgnoreCase
        // check, own Id excluded, unique index is a defense-in-depth safety net only).
        var candidates = await context.ExportProfiles
            .Where(p => p.Id != profile.Id)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);
        var trimmedName = profile.Name.Trim();
        if (candidates.Any(name => string.Equals(name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProfileNameAlreadyExistsException(profile.Name);
        }

        var existing = await context.ExportProfiles.FirstOrDefaultAsync(p => p.Id == profile.Id, cancellationToken);
        if (existing is not null)
        {
            context.ExportProfiles.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        context.ExportProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await context.ExportProfiles.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (profile is null)
        {
            return;
        }

        context.ExportProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);
    }
}
