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
