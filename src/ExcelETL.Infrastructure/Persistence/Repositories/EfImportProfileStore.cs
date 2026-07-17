using ExcelETL.Application.Extraction.Oxo;
using ExcelETL.Domain.Extraction.Profile;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.Infrastructure.Persistence.Repositories;

// Each method owns a short-lived DbContext created via the factory -- same rationale as
// ExtractionConfigRepository (Blazor Server's long-lived circuits can invoke handlers concurrently).
public class EfImportProfileStore(IDbContextFactory<ExcelEtlDbContext> dbContextFactory) : IImportProfileStore
{
    public async Task<IReadOnlyList<ImportProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ImportProfiles
            .OrderBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ImportProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ImportProfiles.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);
    }

    // A true upsert: profiles are edited by building a brand new ImportProfile instance (via its
    // Guid-taking constructor) under the Id of the profile it replaces -- see ImportProfile's own
    // comment on that constructor. SheetExtractionRule and everything nested under it have no
    // identity of their own (see SheetExtractionRule's doc comment), so there is no meaningful way to
    // diff old vs. new nested rows; the existing row and its whole owned graph are removed and the
    // new graph is inserted under the same Id instead. This is two round trips rather than one
    // SaveChangesAsync call, deliberately: tracking a Removed and an Added instance under the same
    // key at the same time throws in EF Core's change tracker, so the delete must be committed before
    // the insert is attempted.
    public async Task SaveAsync(ImportProfile profile, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.ImportProfiles.FirstOrDefaultAsync(p => p.Id == profile.Id, cancellationToken);
        if (existing is not null)
        {
            context.ImportProfiles.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        context.ImportProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await context.ImportProfiles.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (profile is null)
        {
            return;
        }

        context.ImportProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);
    }
}
